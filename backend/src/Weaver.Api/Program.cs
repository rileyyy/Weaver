using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Weaver.Api.Middleware;
using Weaver.Domain;
using Weaver.Infrastructure;
using Weaver.Infrastructure.Auth;
using Weaver.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Enums serialize as their string name (e.g. "Human"), not the underlying
// int — self-describing on the wire, and this is what UserDto.Kind's
// frontend parsing (and any future enum DTO field) expects.
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<WeaverDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Weaver")));

builder.Services.AddScoped<IWorkItemService, WorkItemService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IWorkItemLinkService, WorkItemLinkService>();
builder.Services.AddScoped<IBoardService, BoardService>();
builder.Services.AddScoped<IStatusService, StatusService>();
builder.Services.AddScoped<IWorkItemLayerService, WorkItemLayerService>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();
if (string.IsNullOrWhiteSpace(jwtOptions?.SigningKey))
{
    throw new InvalidOperationException(
        "Jwt:SigningKey is not configured. Set it via the Jwt__SigningKey environment variable (see .env.example).");
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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<WeaverDbContext>().Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ApiExceptionMiddleware>();

app.UseHttpsRedirection();

app.UseCors(corsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
