using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InfrastructureLayer.Data
{
    /// <summary>
    /// Applies pending migrations and seeds the bootstrap <see cref="SystemAdmin"/> on first run.
    /// Idempotent: the admin row is inserted only when no system admin exists, so repeated startups
    /// are safe. The bootstrap password is read from configuration (user-secrets / environment) and
    /// hashed before storage — it is never persisted in plaintext and never hardcoded in a migration.
    /// </summary>
    public static class DbSeeder
    {
        /// <summary>Configuration section holding the bootstrap admin credentials.</summary>
        public const string SectionName = "SeedAdmin";

        /// <summary>
        /// Migrates the database and seeds the bootstrap admin. Call once at application startup.
        /// </summary>
        /// <param name="serviceProvider">The root service provider to resolve a scoped context from.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        public static async Task MigrateAndSeedAsync(
            IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            using IServiceScope scope = serviceProvider.CreateScope();
            IServiceProvider services = scope.ServiceProvider;

            var context = services.GetRequiredService<AppDbContext>();
            var hasher = services.GetRequiredService<IPasswordHasher>();
            var configuration = services.GetRequiredService<IConfiguration>();
            ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbSeeder).FullName!);

            await context.Database.MigrateAsync(cancellationToken);
            await SeedSystemAdminAsync(context, hasher, configuration, logger, cancellationToken);
        }

        private static async Task SeedSystemAdminAsync(
            AppDbContext context,
            IPasswordHasher hasher,
            IConfiguration configuration,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            bool anyAdminExists = await context.SystemAdmins.IgnoreQueryFilters()
                .AnyAsync(cancellationToken);
            if (anyAdminExists)
            {
                return;
            }

            IConfigurationSection section = configuration.GetSection(SectionName);
            string? username = section["Username"];
            string? password = section["Password"];

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning(
                    "No system admin exists and '{Section}:Username'/'{Section}:Password' are not configured. " +
                    "Skipping bootstrap admin seed; set them via user-secrets or environment variables to seed on next run.",
                    SectionName, SectionName);
                return;
            }

            var admin = new SystemAdmin
            {
                Username = username,
                PasswordHash = hasher.Hash(password),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.SystemAdmins.Add(admin);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Seeded bootstrap system admin '{Username}'.", username);
        }
    }
}
