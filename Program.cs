using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Middleware (ORDER MATTERS)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

// 🔐 Require Azure AD Login (App Service Authentication)
app.Use(async (context, next) =>
{
    // Allow anonymous health endpoint (needed for Azure probes)
    if (context.Request.Path.StartsWithSegments("/api/health"))
    {
        await next();
        return;
    }

    // If user NOT logged in → redirect to Azure AD login
    if (!context.Request.Headers.ContainsKey("X-MS-CLIENT-PRINCIPAL"))
    {
        context.Response.Redirect("/.auth/login/aad");
        return;
    }

    await next();
});


// ================= ROOT ENDPOINT =================
app.MapGet("/", (HttpContext context) =>
{
    string userEmail = "Unknown";

    if (context.Request.Headers.TryGetValue("X-MS-CLIENT-PRINCIPAL", out var header))
    {
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header!));
        using var doc = JsonDocument.Parse(decoded);
        var claims = doc.RootElement.GetProperty("claims");

        foreach (var claim in claims.EnumerateArray())
        {
            if (claim.GetProperty("typ").GetString() == "preferred_username")
            {
                userEmail = claim.GetProperty("val").GetString() ?? "Unknown";
                break;
            }
        }
    }

    return Results.Ok(new
    {
        message = "Sample API running securely with Azure AD + Key Vault 🚀",
        loggedInUser = userEmail,
        endpoints = new[]
        {
            "/weatherforecast",
            "/api/health (public)",
            "/env (secured)"
        }
    });
});


// ================= WEATHER =================
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild",
    "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast(
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();

    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();


// ================= HEALTH (PUBLIC) =================
app.MapGet("/api/health", () =>
{
    return Results.Ok(new
    {
        status = "Healthy",
        environment = app.Environment.EnvironmentName,
        timestamp = DateTime.UtcNow
    });
})
.WithName("HealthCheck")
.WithOpenApi();


// ================= KEY VAULT SECRET (SECURED) =================
app.MapGet("/env", () =>
{
    var secret = Environment.GetEnvironmentVariable("MySecret");

    return Results.Ok(new
    {
        MySecret = string.IsNullOrEmpty(secret) ? "NOT FOUND" : secret
    });
})
.WithName("ReadKeyVaultSecret")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
