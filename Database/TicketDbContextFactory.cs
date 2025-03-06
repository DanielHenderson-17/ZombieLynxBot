using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

public class TicketDbContextFactory : IDesignTimeDbContextFactory<TicketDbContext>
{
    public TicketDbContext CreateDbContext(string[] args)
    {
        // ✅ Load connection string from botconfig.json
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("botconfig.json")
            .Build();

        var connectionString = config["TicketsDb:ConnectionString"];
        var provider = config["TicketsDb:Provider"];

        if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(provider))
        {
            throw new Exception("❌ Connection string or provider is missing from botconfig.json");
        }

        var optionsBuilder = new DbContextOptionsBuilder<TicketDbContext>();

        if (provider == "Postgres")
        {
            optionsBuilder.UseNpgsql(connectionString);
        }
        else
        {
            throw new Exception($"❌ Unsupported database provider: {provider}");
        }

        return new TicketDbContext(connectionString, provider);
    }
}
