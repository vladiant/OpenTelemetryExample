
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using MongoDB.EntityFrameworkCore.Extensions;

namespace DotnetTelemetryPlayground.ApiService.Modules.Weather.Infrastructure.Db;

public class WeatherDbContext : DbContext
{
    private readonly IMongoDatabase _MongoDB;

    public IMongoDatabase MongoDB => _MongoDB;

    public DbSet<Domain.Models.WeatherForecast> WeatherForecast { get; init; }

    public WeatherDbContext(DbContextOptions options, IMongoDatabase mongoDB)
        : base(options)
    {
        // Initialize the MongoDB client
        _MongoDB = mongoDB;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // call parent
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Domain.Models.WeatherForecast>(builder =>
        {
            builder.Property(e => e.ForDate)
                            .HasElementName("ForDate");
            builder.HasIndex(e => e.ForDate, "ix_wf_date")
                            .IsUnique(true);

            builder.ToCollection("weatherForecast");
        });
    }
}
