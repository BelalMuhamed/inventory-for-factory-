
using Hangfire;
using Hangfire.MemoryStorage;
using invetoryBackGroundServices.Common;
using invetoryBackGroundServices.Enums;
using invetoryBackGroundServices.Machine;
using invetoryBackGroundServices.Machine.Abstractions;
using invetoryBackGroundServices.Machine.Evolis;
using invetoryBackGroundServices.Machine.Orchestration;
using invetoryBackGroundServices.Machine.Rendering;
using invetoryBackGroundServices.Middleware;
using invetoryBackGroundServices.Options;
using invetoryBackGroundServices.Outbox;
using invetoryBackGroundServices.Resources.Localization;
using invetoryBackGroundServices.Security;
using invetoryBackGroundServices.Services;
using MATICA_S3300e.LAN;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;

namespace invetoryBackGroundServices
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddWindowsService(options =>
            {
                options.ServiceName = "InvetoryServices";
            });


            builder.Services.AddControllers(options =>
            {
                // Centralized error-message localization for every action result, same mechanism
                // as the Inventory API's filter of the same name.
                options.Filters.Add<Filters.LocalizeErrorResultFilter>();
            }).AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            // Resource files live in Resources/Localization, so the localizer must look there:
            // IStringLocalizer<Messages> then resolves Messages.resx / Messages.ar.resx by error code.
            builder.Services.AddLocalization(options => options.ResourcesPath = "Resources/Localization");

            builder.Services.Configure<RequestLocalizationOptions>(options =>
            {
                // Same two cultures as the Inventory API, so both services answer a given
                // Accept-Language identically.
                var supported = new[] { new CultureInfo("en"), new CultureInfo("ar") };
                options.DefaultRequestCulture = new RequestCulture("en");
                options.SupportedCultures = supported;
                options.SupportedUICultures = supported;

                options.RequestCultureProviders = new IRequestCultureProvider[]
                {
                    new QueryStringRequestCultureProvider(),
                    new AcceptLanguageHeaderRequestCultureProvider()
                };
            });

            // Replaces the previous AllowAnyOrigin policy. Origins come from configuration so
            // they differ per environment without a code change; an empty list fails startup
            // rather than silently reverting to permitting everything.
            CorsPolicyOptions corsOptions =
                builder.Configuration.GetSection(CorsPolicyOptions.SectionName).Get<CorsPolicyOptions>() ?? new CorsPolicyOptions();
            EnsureCorsOriginsConfigured(corsOptions);

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins(corsOptions.AllowedOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Matica Print Flow: strongly-typed configuration (Options pattern), replacing the old
            // Configuration["WebAPI"] string-indexer / static API_Handle.Init(...) pattern.
            builder.Services.AddOptions<InventoryApiOptions>()
                .Bind(builder.Configuration.GetSection(InventoryApiOptions.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "InventoryApi BaseUrl is required.")
                .ValidateOnStart();

            builder.Services.AddOptions<PrintAgentAuthOptions>()
                .Bind(builder.Configuration.GetSection(PrintAgentAuthOptions.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey), "PrintAgentAuth SigningKey is required.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "PrintAgentAuth Issuer is required.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "PrintAgentAuth Audience is required.")
                .ValidateOnStart();

            EnsurePrintAgentSigningKeyPresent(builder.Configuration);

            // Matica Print Flow: validates the short-lived Print Agent token Angular hands this
            // service. A single scheme is enough here (unlike the Inventory API, which also needs
            // its own separate tenant/admin scheme) - this service only ever validates print-agent
            // tokens, nothing else.
            PrintAgentAuthOptions printAgentAuth =
                builder.Configuration.GetSection(PrintAgentAuthOptions.SectionName).Get<PrintAgentAuthOptions>()
                ?? new PrintAgentAuthOptions();

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = printAgentAuth.Issuer,
                        ValidAudience = printAgentAuth.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(printAgentAuth.SigningKey))
                    };

                    // Without these, a missing/invalid/expired token or a token that fails the
                    // PrintAgentOnly policy's claim check never reaches a controller action, so
                    // the standard ApiResponse<T> envelope was never being written at all - the
                    // caller just saw an empty 401/403.
                    options.Events = new JwtBearerEvents
                    {
                        OnChallenge = async context =>
                        {
                            // Fires for no token, a malformed token, an expired token, or one
                            // signed with the wrong key. HandleResponse() stops the default
                            // handler from also writing its own (empty) response afterward.
                            context.HandleResponse();
                            var localizer = context.HttpContext.RequestServices
                                .GetRequiredService<IStringLocalizer<Messages>>();
                            await ErrorResponseWriter.WriteAsync(
                                context.HttpContext.Response, MachineErrors.Unauthenticated(), localizer);
                        },
                        OnForbidden = async context =>
                        {
                            // Fires when the token validated but failed the PrintAgentOnly
                            // policy's claim check - structurally valid, but not a Print Agent
                            // token (missing the purpose claim).
                            var localizer = context.HttpContext.RequestServices
                                .GetRequiredService<IStringLocalizer<Messages>>();
                            await ErrorResponseWriter.WriteAsync(
                                context.HttpContext.Response, MachineErrors.Forbidden(), localizer);
                        }
                    };
                });
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy(PrintAgentAuthPolicy.Name, policy =>
                    policy.RequireClaim(PrintAgentClaims.Purpose, PrintAgentClaims.PurposeValue));
            });

            // Logger fix (prior review, Critical): a single shared instance for the process, not
            // Matica Print Flow, file-encryption phase: shared AES-GCM encryption for anything
            // this service persists to disk that needs confidentiality - log lines and Outbox
            // files alike, one service rather than one implementation per caller. Registered
            // before Logger below, since Logger's own factory needs to resolve it.
            builder.Services.AddOptions<FileEncryptionOptions>()
                .Bind(builder.Configuration.GetSection(FileEncryptionOptions.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.Key), "FileEncryption Key is required.")
                .Validate(o =>
                {
                    try { return Convert.FromBase64String(o.Key).Length == 32; }
                    catch (FormatException) { return false; }
                }, "FileEncryption Key must be base64-encoded and decode to exactly 32 bytes (AES-256).")
                .ValidateOnStart();
            builder.Services.AddSingleton<IFileEncryptionService, AesGcmFileEncryptionService>();

            // one constructed per request. Same constructor arguments the old per-request field
            // initializer on MachineController used - only the lifetime changes. This also fixes
            // the log-file race (the old per-request Mutex never actually serialized concurrent
            // requests against each other) and stops the log-directory scan-and-delete from
            // running on every single request instead of once at startup.
            builder.Services.AddSingleton(sp => new Logger(
                sp.GetRequiredService<IFileEncryptionService>(),
                Assembly.GetExecutingAssembly().GetName().Name + "_LOG",
                Path.Combine(AppContext.BaseDirectory, "AppLog"),
                true,
                true));

            // Legacy-cleanup phase: AddScoped<MachineConnectionClass/MachineInfoJSON/ActionClass/
            // CardData>() registrations removed - MachineController's constructor was their only
            // consumer, and Print no longer needs them since migrating to IMaticaCommandClient.
            // The classes MachineInfoJSON/MachineResponse/etc. still exist (they're the data
            // model the new async layer deserializes into); only the DI wiring for the four
            // request-scoped mutable-state types the legacy synchronous layer needed is gone.

            // Reliability plan, Phase 7 + Matica Print Flow, reconciliation-credential phase:
            // file-based outbox + Hangfire (memory storage, per approved decision - the outbox
            // file is already the durable part, so a persisted job store would be redundant
            // weight for a single-machine branch service) running a 30-minute recurring sweep
            // plus a startup scan. The job authenticates each run with its own freshly-minted
            // reconciliation service token (see IReconciliationTokenProvider below) rather than
            // any token stored per-entry - the original design reused each entry's Print Agent
            // token from its print attempt, which was always expired by the time a scheduled
            // sweep ran; this closes that gap for both the scheduled sweep and the startup scan.
            builder.Services.AddOptions<OutboxOptions>()
                .Bind(builder.Configuration.GetSection(OutboxOptions.SectionName))
                .Validate(o => o.ReconciliationIntervalMinutes > 0, "Outbox ReconciliationIntervalMinutes must be greater than zero.")
                .ValidateOnStart();
            builder.Services.AddSingleton<IOutboxStore, FileOutboxStore>();
            builder.Services.AddScoped<OutboxReconciliationJob>();

            builder.Services.AddHangfire(config => config.UseMemoryStorage());
            builder.Services.AddHangfireServer();

            // Matica Print Flow, reconciliation-credential phase: the Printer Agent's own standing
            // credential, exchanged for a fresh access token once per reconciliation run. Same
            // typed-HttpClient-not-per-call pattern as IPrintFlowClient, same base address (the
            // token endpoint lives on the same Inventory API).
            builder.Services.AddOptions<ReconciliationCredentialOptions>()
                .Bind(builder.Configuration.GetSection(ReconciliationCredentialOptions.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.ClientId), "ReconciliationCredential ClientId is required.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.ClientSecret), "ReconciliationCredential ClientSecret is required.")
                .ValidateOnStart();
            builder.Services.AddHttpClient<IReconciliationTokenProvider, ReconciliationTokenProvider>((sp, client) =>
            {
                InventoryApiOptions options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<InventoryApiOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
            });

            // Machine communication timeout: backend configuration, not per-request data.
            builder.Services.AddOptions<MachineCommunicationOptions>()
                .Bind(builder.Configuration.GetSection(MachineCommunicationOptions.SectionName))
                .Validate(o => o.TimeoutSeconds > 0, "MachineCommunication TimeoutSeconds must be greater than zero.")
                .ValidateOnStart();

            // Async machine communication, replacing the blocking HttpWebRequest pattern. The
            // transport is deliberately vendor-neutral; MaticaCommandClient holds everything
            // Matica-specific, so another device family means a new command client rather than
            // changes to communication code.
            builder.Services.AddHttpClient<IMachineTransport, MachineHttpTransport>()
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    // Retained deliberately: the machine presents a self-signed certificate on the
                    // LAN, exactly as the pre-existing httpPOST/httpPOSTGetInfoJson assume. The
                    // earlier review's recommendation to pin this specific device's certificate
                    // instead of accepting any still stands as a follow-up - changing it here
                    // would alter behavior this phase is meant to hold constant.
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                });

            builder.Services.AddScoped<IMaticaCommandClient, MaticaCommandClient>();

            // Matica Print Flow: typed HttpClient (not a new HttpClient() per call, unlike the old
            // API_HttpClient this replaces) for the two backend calls to the Inventory API.
            builder.Services.AddHttpClient<IPrintFlowClient, PrintFlowClient>((sp, client) =>
            {
                InventoryApiOptions options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<InventoryApiOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
            });

            // Evolis Integration Plan, Phase 3: SDK plumbing, discovery, self-registration. No
            // hardware-dependent validation here - every EvolisOptions field has a sensible
            // default (confirmed against real connected hardware during this phase).
            builder.Services.AddOptions<EvolisOptions>()
                .Bind(builder.Configuration.GetSection(EvolisOptions.SectionName));

            // Stateless and SDK-call-only - safe as singletons, same reasoning as
            // Pbkdf2PasswordHasher's precedent in the Inventory API.
            builder.Services.AddSingleton<EvolisConnectionFactory>();
            builder.Services.AddSingleton<EvolisDeviceDiscovery>();

            // Keyed by UsingPrinterType, matching the Inventory API's own printer-family
            // convention (plan §4.6) - a future Matica adapter joins this same registration under
            // UsingPrinterType.Matica once its inline orchestration migrates (deferred, Q-27).
            builder.Services.AddKeyedScoped<ICardPrinterAdapter, EvolisPrinterAdapter>(UsingPrinterType.Evolis);

            // Evolis Integration Plan, Phase 4: SkiaSharp card rendering. Stateless - safe as a
            // singleton, same reasoning as EvolisConnectionFactory/EvolisDeviceDiscovery above.
            builder.Services.AddSingleton<ICardRenderer, SkiaCardRenderer>();

            // Evolis Integration Plan, Phase 5: Agent-side print flow. PrinterJobGate is a
            // singleton deliberately - its ConcurrentDictionary<string, SemaphoreSlim> is the
            // shared state serializing jobs per device name across every request, so a
            // scoped/per-request instance would defeat its entire purpose. The rest are stateless
            // (PanExtractor, EvolisRibbonCompatibilityChecker, EvolisPrintSettingsMapper), same
            // reasoning as the Phase 3/4 singletons above.
            builder.Services.AddSingleton<IPrinterJobGate, PrinterJobGate>();
            builder.Services.AddSingleton<IPanExtractor, PanExtractor>();
            builder.Services.AddSingleton<EvolisRibbonCompatibilityChecker>();
            builder.Services.AddSingleton<EvolisPrintSettingsMapper>();
            builder.Services.AddScoped<IEvolisCardJobOrchestrator, EvolisCardJobOrchestrator>();

            builder.Services.AddHttpClient<IEvolisSupervisorRegistrationClient, EvolisSupervisorRegistrationClient>((sp, client) =>
            {
                InventoryApiOptions options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<InventoryApiOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
            });

            // Singleton registered once, then exposed as the IHostedService - so
            // EvolisMachineController can call RegisterNowAsync directly on the same instance
            // Microsoft.Extensions.Hosting drives at startup, instead of two separate instances.
            builder.Services.AddSingleton<EvolisSupervisorRegistrar>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<EvolisSupervisorRegistrar>());

            // Native SDK trace, configured before any Evolis.* call - plan §8 acceptance
            // criteria ("native SDK log file is produced when NativeLogPath is configured").
            EvolisOptions evolisOptions =
                builder.Configuration.GetSection(EvolisOptions.SectionName).Get<EvolisOptions>() ?? new EvolisOptions();
            if (!string.IsNullOrWhiteSpace(evolisOptions.NativeLogPath))
            {
                Evolis.Evolis.SetLogPath(evolisOptions.NativeLogPath);
            }
            if (Enum.TryParse(evolisOptions.NativeLogLevel, ignoreCase: true, out Evolis.LogLevel nativeLogLevel))
            {
                Evolis.Evolis.SetLogLevel(nativeLogLevel);
            }

            builder.WebHost.UseUrls("http://localhost:8403");


            var app = builder.Build();

            // Outermost, so it catches anything thrown further down the pipeline.
            app.UseMiddleware<GlobalExceptionMiddleware>();

            // Before the endpoints, so the culture is resolved by the time either the error
            // filter or the exception middleware needs to localize a message.
            app.UseRequestLocalization();

            app.UseCors();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseRouting();

            // Matica Print Flow: authentication must run before authorization so the principal is
            // established first. UseAuthorization() alone (as before) was a no-op - no
            // authentication scheme was ever registered, so nothing was actually being checked.
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            // Reliability plan, Phase 7: run the reconciliation sweep once immediately (a crash-
            // and-restart has a real chance of landing inside the Print Agent token's 5-minute
            // window, unlike the scheduled sweep below), then on a recurring 30-minute schedule.
            // Blocking here is deliberate and bounded - there is normally nothing, or very few
            // entries, in the outbox; this is not meant to be a long-running operation.
            using (IServiceScope startupScope = app.Services.CreateScope())
            {
                OutboxReconciliationJob startupJob =
                    startupScope.ServiceProvider.GetRequiredService<OutboxReconciliationJob>();
                await startupJob.RunAsync();
            }

            // The static RecurringJob.AddOrUpdate helper relies on the legacy global
            // JobStorage.Current, which AddHangfire(...)'s DI-based registration never sets -
            // that mismatch is exactly what "Current JobStorage instance has not been
            // initialized yet" means. IRecurringJobManager, resolved from the same service
            // provider AddHangfire populated, uses the storage that was actually configured
            // instead of a global that nothing here ever assigns.
            IRecurringJobManager recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();
            int reconciliationIntervalMinutes =
                app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<OutboxOptions>>().Value.ReconciliationIntervalMinutes;
            recurringJobManager.AddOrUpdate<OutboxReconciliationJob>(
                "outbox-reconciliation", job => job.RunAsync(CancellationToken.None),
                Cron.MinuteInterval(reconciliationIntervalMinutes));

            await app.RunAsync();
        }

        private static void EnsureCorsOriginsConfigured(CorsPolicyOptions corsOptions)
        {
            if (corsOptions.AllowedOrigins is null || corsOptions.AllowedOrigins.Length == 0)
            {
                throw new InvalidOperationException(
                    $"No CORS origins are configured. Set '{CorsPolicyOptions.SectionName}:" +
                    $"{nameof(CorsPolicyOptions.AllowedOrigins)}' to the exact origin(s) allowed to call " +
                    "this service (for example the Angular app's origin). This deliberately fails startup " +
                    "rather than falling back to allowing any origin, since this service can physically " +
                    "move and emboss cards.");
            }
        }

        private static void EnsurePrintAgentSigningKeyPresent(IConfiguration configuration)
        {
            string? signingKey = configuration[
                $"{PrintAgentAuthOptions.SectionName}:{nameof(PrintAgentAuthOptions.SigningKey)}"];
            if (string.IsNullOrWhiteSpace(signingKey))
            {
                throw new InvalidOperationException(
                    "Print Agent token signing key is not configured. Set " +
                    $"'{PrintAgentAuthOptions.SectionName}:{nameof(PrintAgentAuthOptions.SigningKey)}' via " +
                    "user-secrets (development) or an environment variable (production). It must be the exact " +
                    "same value as the Inventory API's 'PrintAgentToken:SigningKey' - see " +
                    "PrintAgentAuthOptions's doc comment for why.");
            }
        }
    }
}

