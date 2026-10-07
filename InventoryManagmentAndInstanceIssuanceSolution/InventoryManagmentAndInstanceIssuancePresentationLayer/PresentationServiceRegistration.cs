using ApplicationLayer.Contracts;
using ApplicationLayer.Options;
using InfrastructureLayer.Security;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.Text;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer
{
    /// <summary>
    /// Registers presentation-layer concerns: JWT bearer authentication bound to
    /// <see cref="JwtOptions"/>, request localization (en/ar), and the current-principal accessor.
    /// </summary>
    public static class PresentationServiceRegistration
    {
        /// <summary>Adds presentation dependencies and authentication.</summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">App configuration (JWT section).</param>
        public static IServiceCollection AddPresentation(
            this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentTenant, CurrentTenant>();

            // Resource files live in Resources/Localization, so the localizer must look there:
            // IStringLocalizer<Messages> then resolves Messages.resx / Messages.ar.resx by error code.
            services.AddLocalization(options => options.ResourcesPath = "Resources/Localization");

            services.Configure<RequestLocalizationOptions>(options =>
            {
                var supported = new[] { new CultureInfo("en"), new CultureInfo("ar") };
                options.DefaultRequestCulture = new RequestCulture("en");
                options.SupportedCultures = supported;
                options.SupportedUICultures = supported;
                // Order: explicit ?culture= wins, then the Accept-Language header.
                options.RequestCultureProviders = new IRequestCultureProvider[]
                {
                    new QueryStringRequestCultureProvider(),
                    new AcceptLanguageHeaderRequestCultureProvider()
                };
            });

            JwtOptions jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

            // Matica Print Flow: a second, dedicated key/audience for the Printer Agent's
            // short-lived token — deliberately never validated against the shared tenant/admin
            // key above. See PrintAgentTokenGenerator's doc comment for why this separation
            // matters (a compromised branch machine must never be able to forge a real session).
            PrintAgentTokenOptions printAgentJwt =
                configuration.GetSection(PrintAgentTokenOptions.SectionName).Get<PrintAgentTokenOptions>()
                ?? new PrintAgentTokenOptions();

            // Matica Print Flow, reconciliation-credential phase: a third dedicated key for the
            // background reconciliation job's service token — same isolation reasoning as the
            // print-agent-token scheme, one level further: this key authorizes exactly one thing
            // (recording a print result during reconciliation), nothing a print-agent token or a
            // tenant session can already do.
            ReconciliationTokenOptions reconciliationJwt =
                configuration.GetSection(ReconciliationTokenOptions.SectionName).Get<ReconciliationTokenOptions>()
                ?? new ReconciliationTokenOptions();

            services
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
                        ValidIssuer = jwt.Issuer,
                        ValidAudience = jwt.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                        NameClaimType = JwtTokenGenerator.UsernameClaim // User.Identity.Name resolves to username
                    };
                })
                .AddJwtBearer(PrintAgentTokenGenerator.AuthenticationScheme, options =>
                {
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = printAgentJwt.Issuer,
                        ValidAudience = printAgentJwt.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(printAgentJwt.SigningKey)),
                        NameClaimType = PrintAgentTokenGenerator.PrinterIdClaim
                    };
                })
                .AddJwtBearer(ReconciliationTokenGenerator.AuthenticationScheme, options =>
                {
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = reconciliationJwt.Issuer,
                        ValidAudience = reconciliationJwt.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(reconciliationJwt.SigningKey)),
                        NameClaimType = ReconciliationTokenGenerator.BranchIdClaim
                    };
                });

            return services;
        }
    }
}