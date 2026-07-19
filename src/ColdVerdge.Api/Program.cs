using ColdVerdge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("GameDatabase")
    ?? throw new InvalidOperationException(
        "Connection string 'GameDatabase' was not found.");

builder.Services.AddDbContext<GameDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.MapGet(
    "/api/health",
    async (GameDbContext dbContext, CancellationToken cancellationToken) =>
    {
        var databaseAvailable =
            await dbContext.Database.CanConnectAsync(cancellationToken);

        if (!databaseAvailable)
        {
            return Results.Problem(
                title: "Database unavailable",
                detail: "The API could not connect to PostgreSQL.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(new
        {
            status = "ok",
            database = "connected",
            utcTime = DateTimeOffset.UtcNow
        });
    });

app.Run();