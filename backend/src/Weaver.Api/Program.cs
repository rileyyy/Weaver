using Microsoft.EntityFrameworkCore;
using Weaver.Api.Middleware;
using Weaver.Infrastructure;
using Weaver.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<WeaverDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Weaver")));

builder.Services.AddScoped<IWorkItemService, WorkItemService>();

// No auth exists yet (Milestone 10), so there are no credentials to protect;
// once auth lands, this should narrow to configured, credentialed origins.
const string corsPolicy = "AllowAnyOriginNoCredentials";
builder.Services.AddCors(options =>
    options.AddPolicy(corsPolicy, policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

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

app.UseAuthorization();

app.MapControllers();

app.Run();
