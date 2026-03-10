using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

// ================= SERVICES =================

// Enable Application Insights ONLY if connection string exists
var aiConnection = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

if (!string.IsNullOrEmpty(aiConnection))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// Enable Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// Enable In-Memory Cache
builder.Services.AddMemoryCache();


// ================= AZURE AD AUTH =================

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("admin"));
});


var app = builder.Build();


// ================= IIS / REVERSE PROXY SUPPORT =================

var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto
};

forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardedOptions);


// ================= MIDDLEWARE =================

app.UseSwagger();
app.UseSwaggerUI();

app.UseResponseCompression();

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


// Request Logging
app.Use(async (context, next) =>
{
    Console.WriteLine($"Request: {context.Request.Method} {context.Request.Path}");
    await next();
});


app.UseAuthentication();
app.UseAuthorization();


// ================= ROOT =================

app.MapGet("/", () =>
{
    return Results.Ok(new
    {
        message = "Sample API running 🚀",
        endpoints = new[]
        {
            "/weatherforecast (Admin Only)",
            "/env (Admin Only)",
            "/api/health (Public)"
        }
    });
});


// ================= WEATHER =================

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
            )).ToArray();

        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

        cache.Set("weather_data", forecast, cacheOptions);
    }

    return forecast;
})
.RequireAuthorization("AdminOnly");


// ================= HEALTH =================

app.MapGet("/api/health", () =>
{
    return Results.Ok(new
    {
        status = "Healthy",
        timestamp = DateTime.UtcNow
    });
});


// ================= ENV =================

app.MapGet("/env", () =>
{
    var secret = Environment.GetEnvironmentVariable("MySecret");

    return Results.Ok(new
    {
        MySecret = string.IsNullOrEmpty(secret) ? "NOT FOUND" : secret
    });
})
.RequireAuthorization("AdminOnly");


if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.Run();


record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}