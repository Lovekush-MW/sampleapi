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

// Endpoints
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


// 🔹 NEW ENDPOINT — reads Key Vault secret from App Settings
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
