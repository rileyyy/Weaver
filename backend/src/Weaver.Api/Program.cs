using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Weaver.Api.Attachments;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Api.Background;
using Weaver.Api.Json;
using Weaver.Api.Mcp;
using Weaver.Api.Middleware;
using Weaver.Api.RateLimiting;
using Weaver.Api.Startup;
using Weaver.Application;
using Weaver.Application.Attachments;
using Weaver.Application.Auth;
using Weaver.Infrastructure;
using Weaver.Infrastructure.Attachments;
using Weaver.Infrastructure.Auth;

var builder = WebApplication.CreateBuilder(args);

// Enums serialize as their string name (e.g. "Human"), not the underlying
// int — self-describing on the wire, and this is what UserDto.Kind's
// frontend parsing (and any future enum DTO field) expects.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(new OptionalJsonConverterFactory());
    });
builder.Services.AddOpenApi(options =>
{
    options.AddSchemaTransformer<OptionalOpenApiSchemaTransformer>();
    options.AddDocumentTransformer<OptionalOpenApiSchemaTransformer>();
});
builder.Services.AddProblemDetails();
builder.Services.AddAuthRateLimiting(builder.Configuration);

var connectionString = builder.Configuration.GetConnectionString("Weaver");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:Weaver is not configured. Set it via the ConnectionStrings__Weaver environment variable.");
}
builder.Services
    .AddWeaverApplication()
    .AddWeaverInfrastructure(connectionString);

builder.Services.AddHealthChecks().AddDbContextCheck<WeaverDbContext>();

builder.Services.AddHostedService<RecurrenceGenerationWorker>();
builder.Services.AddHostedService<TrashPurgeWorker>();

// The services read who is calling from ICallerContext. MCP write tools also resolve the
// concrete HttpCallerContext, to hand it the actor label they take as an argument.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<HttpCallerContext>();
builder.Services.AddScoped<ICallerContext>(services => services.GetRequiredService<HttpCallerContext>());
builder.Services.AddScoped<WorkItemDtoMapper>();
builder.Services.AddSingleton<MultipartFileReader>();

// A read-only API key may not write (ADR-0004). Both transports apply Caller.EnsureCanWrite to
// every write by default, so a new endpoint or tool is covered without opting in: REST to every
// non-GET action, MCP to every tool not marked ReadOnly.
builder.Services.Configure<MvcOptions>(options => options.Filters.Add<WriteAccessFilter>());

// MCP tool classes are discovered via [McpServerToolType] from this assembly (Weaver.Api,
// the calling assembly), and the agent reference via [McpServerResourceType] — see the Mcp/
// folder. Stateless mode means each MCP call is just a
// normal request in the existing ASP.NET Core pipeline: same DI scope (so Weaver.DbContext-backed
// services resolve exactly as they do for a controller), same JWT bearer auth (below), same
// port. No separate transport/process/auth story to maintain.
builder.Services.AddMcpServer(options => options.ServerInstructions = AgentGuidance.ServerInstructions)
    .WithHttpTransport(options => options.Stateless = true)
    .WithToolsFromAssembly(serializerOptions: McpJsonSerializerOptions.Default)
    .WithResourcesFromAssembly()
    .WithRequestFilters(filters => filters.AddCallToolFilter(WriteAccessToolFilter.Apply));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
JwtSigningKeyPolicy.EnsureUsable(jwtOptions.SigningKey, builder.Environment.IsDevelopment());
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var firstAdminOptions = builder.Configuration.GetSection(FirstAdminOptions.SectionName).Get<FirstAdminOptions>() ?? new FirstAdminOptions();
FirstAdminPasswordPolicy.EnsureUsable(firstAdminOptions.Password, builder.Environment.IsDevelopment());
builder.Services.Configure<FirstAdminOptions>(builder.Configuration.GetSection(FirstAdminOptions.SectionName));
var attachmentsSection = builder.Configuration.GetSection(AttachmentOptions.SectionName);
(attachmentsSection.Get<AttachmentOptions>() ?? new AttachmentOptions()).EnsureUsable();
builder.Services.Configure<AttachmentOptions>(attachmentsSection);
// A relative path (as in development) is under the app, not wherever it was started from.
builder.Services.PostConfigure<AttachmentOptions>(options =>
    options.Path = Path.GetFullPath(options.Path, builder.Environment.ContentRootPath));

// Humans send an access token and agents an API key (ADR-0004), both as a bearer value, since
// that's the header every MCP client can set. The key's prefix picks the scheme, so each
// credential is only ever checked by its own handler.
const string bearerOrApiKeyScheme = "BearerOrApiKey";
builder.Services
    .AddAuthentication(bearerOrApiKeyScheme)
    .AddPolicyScheme(bearerOrApiKeyScheme, displayName: null, options =>
        options.ForwardDefaultSelector = context =>
            ApiKeySecret.LooksLikeKey(ApiKeyAuthenticationHandler.BearerValue(context.Request))
                ? ApiKeyAuthenticationHandler.SchemeName
                : JwtBearerDefaults.AuthenticationScheme)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null)
    .AddJwtBearer(options =>
    {
        // Without this, the handler silently remaps standard claim types
        // (e.g. "sub") to legacy XML-namespace URIs on the resulting
        // ClaimsPrincipal, so a plain JwtRegisteredClaimNames.Sub lookup
        // (see ClaimsPrincipalExtensions.GetUserId) would never match.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
// Secure by default: every endpoint requires a valid access token unless it
// opts out with [AllowAnonymous] (as AuthController's own endpoints do) —
// safer than annotating each controller individually, since a new
// controller is protected automatically rather than by remembering to add
// [Authorize] to it.
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// Narrowed once auth existed (Milestone 10) — previously any origin was
// allowed since there were no credentials to protect. No default: a
// deployment that forgets to configure this should fail to allow cross-
// origin calls at all, not silently allow everyone.
const string corsPolicy = "ConfiguredOrigins";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy(corsPolicy, policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader()));

// In production the API is only reachable through Caddy -> nginx on the
// compose network (no published port), whose container IPs aren't fixed, so
// the proxies are trusted by position rather than by address.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 2;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

var migrationMode = StartupMigrationPolicy.Decide(args, app.Configuration);
if (migrationMode != StartupMigrationMode.Skip)
{
    await app.Services.MigrateWeaverDatabaseAsync();
}

if (migrationMode == StartupMigrationMode.MigrateAndExit)
{
    return;
}

app.Services.EnsureAttachmentsDirectory();
await app.Services.EnsureFirstAdminAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseForwardedHeaders();

// After forwarded headers, so the limiter partitions by the real client IP.
app.UseRateLimiter();

// Anything ApiExceptionMiddleware doesn't recognise is a bug: logged, and answered with a
// problem-details 500 rather than an empty body.
app.UseExceptionHandler();
app.UseMiddleware<ApiExceptionMiddleware>();

app.UseCors(corsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// For compose healthchecks and startup ordering. Not proxied by nginx, so
// only reachable from inside the compose network.
app.MapHealthChecks("/health").AllowAnonymous();

// Inherits the same [Authorize] fallback policy as every controller above (no
// [AllowAnonymous]-equivalent opt-out here). An agent's MCP client authenticates with its API
// key; a bearer access token (POST /api/auth/login) works too.
app.MapMcp("/mcp");

app.Run();

// Makes the entry point visible to WebApplicationFactory<Program> in the
// integration tests.
public partial class Program;
