using ApplicationLayer.Options;
using InfrastructureLayer;
using InfrastructureLayer.Data;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Filters;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Middleware;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Security;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using System.Reflection;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);


            LogFileOptions logOpts =
                builder.Configuration.GetSection(LogFileOptions.SectionName).Get<LogFileOptions>()
                ?? new LogFileOptions();

            string logDir = Path.Combine(builder.Environment.ContentRootPath, logOpts.Directory);
            if (!Directory.Exists(logDir)) { Directory.CreateDirectory(logDir); }
            var formatter = new CompactJsonFormatter();

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                // Exception file: anything carrying an exception. Plain text (revision: no
                // longer encrypted - see LogFileOptions).
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Exception is not null)
                    .WriteTo.File(formatter, Path.Combine(logDir, logOpts.ExceptionFileName)))
                // Error file: Warning+ with no exception.
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Exception is null && e.Level >= LogEventLevel.Error)
                    .WriteTo.File(formatter, Path.Combine(logDir, logOpts.ErrorFileName)))
                .CreateLogger();

            builder.Host.UseSerilog();
            try
            {
               

                // Add services to the container.

            // Program.cs — replace builder.Services.AddControllers();
            builder.Services.AddControllers(options =>
            {
                options.Filters.Add<LocalizeErrorResultFilter>();
            });            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            // Replace the default [ApiController] 400 model-validation response with our 422 envelope.
            builder.Services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = ValidationResponseFactory.Build;
            });
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(op =>
            {
                op.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "calimly Inventory Management & Card Issuance API",
                    Version = "v1",
                    Description =
                        "Multi-tenant inventory management and card issuance platform: card SKUs, " +
                        "physical card instances, stock, batch uploads, card-file generation, " +
                        "transfers, disposals, branch requests, and printing configuration. " +
                        "Every endpoint responds with the same envelope - " +
                        "{ success, data, error } - on both success and failure; see each " +
                        "endpoint's response examples for the shape of 'error' on that path."
                });

                op.OperationFilter<AcceptLanguageHeaderOperationFilter>();

                // Swagger enhancement (Phase S1): attaches the named, multi-scenario examples
                // registered in ExampleCatalog to each operation's request/response bodies.
                op.OperationFilter<ExamplesOperationFilter>();

                // Swagger enhancement (Phase S1): documents each enum's numeric wire values,
                // since no JsonStringEnumConverter is registered (Docs/PROJECT_KNOWLEDGE.md section 12).
                op.SchemaFilter<EnumSchemaDescriptionsFilter>();

                // PresentationServiceRegistration.AddPresentation - inside services.AddSwaggerGen(options => { ... })
                op.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,   // paste the raw token; Swagger adds the "Bearer " prefix
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Paste your JWT access token below (without the 'Bearer ' prefix)."
                });

                    op.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
    {
        new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        },
        Array.Empty<string>()
    }
    });

                // Swagger enhancement (Phase S1): surfaces every layer's <summary>/<param>/
                // <response> XML doc comments in Swagger UI. GenerateDocumentationFile is now
                // enabled on all three projects (Presentation already had it; DomainLayer and
                // ApplicationLayer were added alongside this change) - without this call, none
                // of those comments ever reached the generated spec, regardless of how complete
                // they were in source.
                foreach (string xmlFile in new[]
                {
                    $"{Assembly.GetExecutingAssembly().GetName().Name}.xml",
                    "ApplicationLayer.xml",
                    "DomainLayer.xml"
                })
                {
                    string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                    if (File.Exists(xmlPath))
                    {
                        op.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
                    }
                }
            });

                // Fail fast if the JWT signing key is missing: a misconfigured secret should surface
                // as a clear startup error, not as confusing 401s at request time.
                EnsureJwtSigningKeyPresent(builder.Configuration);

                // Matica Print Flow: same fail-fast posture for the Print Agent token's own,
                // deliberately separate signing key (PrintAgentTokenGenerator's doc comment
                // explains why it must never be the same value as the JWT key above).
                EnsurePrintAgentSigningKeyPresent(builder.Configuration);

                // Onion composition: inner layers are wired before the presentation concerns that depend on them.
                builder.Services.AddInfrastructure(builder.Configuration);
                builder.Services.AddPresentation(builder.Configuration);
                // In Program.cs, where services are configured:
                builder.Services.AddAuthorization(AuthorizationPolicies.Register);
                var app = builder.Build();

                // Apply migrations and seed the bootstrap system admin on startup (idempotent).
                await DbSeeder.MigrateAndSeedAsync(app.Services);

                // Global exception handling must sit first so it wraps the entire downstream pipeline.
                app.UseMiddleware<GlobalExceptionMiddleware>();


                app.UseSwagger();
                app.UseSwaggerUI();


                app.UseHttpsRedirection();

                // Revision, "Print Images & Product Print Configuration" change request: the
                // unauthenticated static-file mount that used to serve print images here is removed.
                // Folders are now named after the tenant's username and files keep their original
                // names (points 2/3 of that change request), which makes physical paths guessable in
                // a way the previous GUID-named/tenant-id-foldered scheme wasn't - serving them
                // without an auth check was no longer safe. Every retrieval now goes through
                // GET /api/print-images/{id} (PrintImagesController), which checks tenant ownership
                // server-side before streaming a single byte.

                // Authentication must run before authorization so the principal is established first.
                app.UseAuthentication();
                app.UseAuthorization();

                // Program.cs — add the middleware BEFORE app.MapControllers() (and before auth is fine too)
                app.UseRequestLocalization();
                app.MapControllers();

                await app.RunAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly during startup or execution.");
                throw;
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }
        }

        private static void EnsureJwtSigningKeyPresent(IConfiguration configuration)
        {
            string? signingKey = configuration[$"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)}"];
            if (string.IsNullOrWhiteSpace(signingKey))
            {
                throw new InvalidOperationException(
                    $"JWT signing key is not configured. Set '{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)}' " +
                    "via user-secrets (development) or an environment variable (production). " +
                    "It must never be committed to appsettings.json.");
            }
        }

        private static void EnsurePrintAgentSigningKeyPresent(IConfiguration configuration)
        {
            string? signingKey = configuration[
                $"{PrintAgentTokenOptions.SectionName}:{nameof(PrintAgentTokenOptions.SigningKey)}"];
            if (string.IsNullOrWhiteSpace(signingKey))
            {
                throw new InvalidOperationException(
                    "Print Agent token signing key is not configured. Set " +
                    $"'{PrintAgentTokenOptions.SectionName}:{nameof(PrintAgentTokenOptions.SigningKey)}' via " +
                    "user-secrets (development) or an environment variable (production), and keep it distinct " +
                    $"from '{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)}' â€” see " +
                    "PrintAgentTokenGenerator's doc comment for why that separation matters.");
            }
        }
    }
}
