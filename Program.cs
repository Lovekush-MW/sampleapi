using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// ================= SERVICES =================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Azure AD JWT authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization();

var app = builder.Build();

// ================= MIDDLEWARE =================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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
            "/weatherforecast (secured)",
            "/env (secured)",
            "/api/health (public)"
        }
    });
});

// ================= WEATHER (SECURED) =================
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild",
    "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast(
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();

    return forecast;
})
.RequireAuthorization(); // <- Secured

// ================= HEALTH (PUBLIC) =================
app.MapGet("/api/health", () =>
{
    return Results.Ok(new
    {
        status = "Healthy",
        timestamp = DateTime.UtcNow
    });
});

// ================= ENV SECRET (SECURED) =================
app.MapGet("/env", () =>
{
    var secret = Environment.GetEnvironmentVariable("MySecret");

    return Results.Ok(new
    {
        MySecret = string.IsNullOrEmpty(secret) ? "NOT FOUND" : secret
    });
})
.RequireAuthorization(); // <- Secured

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
