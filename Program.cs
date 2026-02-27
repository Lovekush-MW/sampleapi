using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.Identity.Web;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

// ================= SERVICES =================

// ✅ Application Insights
builder.Services.AddApplicationInsightsTelemetry();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ✅ Enable Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// ✅ Enable In-Memory Caching
builder.Services.AddMemoryCache();

// Add Azure AD JWT authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.Configure<JwtBearerOptions>(
    JwtBearerDefaults.AuthenticationScheme,
    options =>
    {
        options.TokenValidationParameters.ValidAudience =
            builder.Configuration["AzureAd:Audience"];

        options.TokenValidationParameters.ValidIssuer =
            $"https://login.microsoftonline.com/{builder.Configuration["AzureAd:TenantId"]}/v2.0";
    });

// Add Role based authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("admin"));
});

var app = builder.Build();

// ================= FOR AZURE HTTPS REDIRECT FIX =================
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto
};

forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardedOptions);
// ================================================================


// ================= MIDDLEWARE =================

// ✅ Enable Swagger in Azure also
app.UseSwagger();
app.UseSwaggerUI();

// ✅ Enable Compression Middleware
app.UseResponseCompression();

// Force HTTPS
app.UseHttpsRedirection();

// Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    await next();
});

// ✅ Request Logging Middleware (helps in App Insights)
app.Use(async (context, next) =>
{
    Console.WriteLine($"Request: {context.Request.Method} {context.Request.Path}");
    await next();
});

app.UseAuthentication();
app.UseAuthorization();


// ================= ROOT (PUBLIC) =================
app.MapGet("/", () =>
{
    return Results.Ok(new
    {
        message = "Sample API running 🚀",
        endpoints = new[]
        {
            "/weatherforecast (Admin only)",
            "/env (Admin only)",
            "/api/health (public)"
        }
    });
});


// ================= WEATHER (ADMIN ONLY) WITH CACHING =================
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild",
    "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", (IMemoryCache cache) =>
{
    if (!cache.TryGetValue("weather_data", out WeatherForecast[]? forecast))
    {
        forecast = Enumerable.Range(1, 5).Select(index =>
            new WeatherForecast(
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(index)),
                Random.Shared.Next(-20, 55),
                summaries[Random.Shared.Next(summaries.Length)]
            ))
            .ToArray();

        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

        cache.Set("weather_data", forecast, cacheOptions);
    }

    return forecast;
})
.RequireAuthorization("AdminOnly");


// ================= HEALTH (PUBLIC) =================
app.MapGet("/api/health", () =>
{
    return Results.Ok(new
    {
        status = "Healthy",
        timestamp = DateTime.UtcNow
    });
});


// ================= ENV SECRET (ADMIN ONLY) =================
app.MapGet("/env", () =>
{
    var secret = Environment.GetEnvironmentVariable("MySecret");

    return Results.Ok(new
    {
        MySecret = string.IsNullOrEmpty(secret) ? "NOT FOUND" : secret
    });
})
.RequireAuthorization("AdminOnly");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}