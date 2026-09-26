using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Weaver.Api.Mcp;
using Weaver.Api.Middleware;
using Weaver.Domain;
using Weaver.Infrastructure;
using Weaver.Infrastructure.Auth;
using Weaver.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Enums serialize as their string name (e.g. "Human"), not the underlying
// int — self-describing on the wire, and this is what UserDto.Kind's
// frontend parsing (and any future enum DTO field) expects.
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<WeaverDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Weaver")));

builder.Services.AddHealthChecks().AddDbContextCheck<WeaverDbContext>();

builder.Services
    .AddScoped<IWorkItemService, WorkItemService>()
    .AddScoped<IAuthService, AuthService>()
    .AddScoped<IUserService, UserService>()
    .AddScoped<ICommentService, CommentService>()
    .AddScoped<IWorkItemLinkService, WorkItemLinkService>()
    .AddScoped<IBoardService, BoardService>()
    .AddScoped<IStatusService, StatusService>()
    .AddScoped<IWorkItemLayerService, WorkItemLayerService>();

// CommentTools reads the calling user's id off the current request the same way
// CommentsController does (User.GetUserId()) — MCP tools don't get a ControllerBase's
// User property for free, so they resolve it via the accessor instead.
builder.Services.AddHttpContextAccessor();

// MCP tool classes are discovered via [McpServerToolType] from this assembly (Weaver.Api,
// the calling assembly) — see the Mcp/ folder. Stateless mode means each MCP call is just a
// normal request in the existing ASP.NET Core pipeline: same DI scope (so Weaver.DbContext-backed
// services resolve exactly as they do for a controller), same JWT bearer auth (below), same
// port. No separate transport/process/auth story to maintain.
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithToolsFromAssembly(serializerOptions: McpJsonSerializerOptions.Default);
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();
if (string.IsNullOrWhiteSpace(jwtOptions?.SigningKey))
{
    throw new InvalidOperationException(
        "Jwt:SigningKey is not configured. Set it via the Jwt__SigningKey environment variable (see .env.example).");
}
// HS256 needs a key of at least 256 bits. A shorter one (e.g. a placeholder
// copied from .env.example) would otherwise only fail at the first login.
if (Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
{
    throw new InvalidOperationException(
        "Jwt:SigningKey must be at least 32 bytes. Generate one with `openssl rand -base64 48`.");
}
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<WeaverDbContext>().Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseForwardedHeaders();

app.UseMiddleware<ApiExceptionMiddleware>();

app.UseCors(corsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// For compose healthchecks and startup ordering. Not proxied by nginx, so
// only reachable from inside the compose network.
app.MapHealthChecks("/health").AllowAnonymous();

// Inherits the same [Authorize] fallback policy as every controller above (no
// [AllowAnonymous]-equivalent opt-out here) — an MCP client authenticates with a bearer
// access token the same way any other API client does (POST /api/auth/login).
app.MapMcp("/mcp");

app.Run();
