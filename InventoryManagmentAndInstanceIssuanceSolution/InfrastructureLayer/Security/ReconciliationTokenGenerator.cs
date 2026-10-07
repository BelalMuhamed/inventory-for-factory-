using System;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ApplicationLayer.Contracts;
using ApplicationLayer.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InfrastructureLayer.Security
{
    /// <summary>
    /// Issues short-lived tokens for the Matica Printer Agent's background outbox reconciliation
    /// job. Mirrors <see cref="PrintAgentTokenGenerator"/>'s pattern exactly — a dedicated key, a
    /// dedicated scheme, a narrow claim set — but for a service account rather than a
    /// user-delegated print attempt.
    /// </summary>
    public sealed class ReconciliationTokenGenerator : IReconciliationTokenGenerator
    {
        /// <summary>Name of the ASP.NET Core authentication scheme this token is validated under.</summary>
        public const string AuthenticationScheme = "ReconciliationService";

        /// <summary>Claim carrying the tenant id the token is scoped to.</summary>
        public const string TenantIdClaim = "tenantId";

        /// <summary>Claim carrying the branch id the token is scoped to.</summary>
        public const string BranchIdClaim = "branchId";

        /// <summary>
        /// Shared claim type with <see cref="PrintAgentTokenGenerator.PurposeClaim"/> — both token
        /// types use the same claim type with a different value, so <c>print-result</c> can accept
        /// either via one policy that lists both allowed values (<c>RequireClaim</c> already
        /// supports multiple allowed values natively; no custom authorization handler needed).
        /// </summary>
        public const string PurposeClaim = PrintAgentTokenGenerator.PurposeClaim;

        /// <summary>The only valid <see cref="PurposeClaim"/> value for this token type.</summary>
        public const string PurposeValue = "reconciliation";

        private readonly ReconciliationTokenOptions _options;

        /// <summary>Creates the generator from its bound options.</summary>
        public ReconciliationTokenGenerator(IOptions<ReconciliationTokenOptions> options)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc />
        public ReconciliationAccessToken Create(long tenantId, long branchId)
        {
            DateTime expiresAt = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);

            var claims = new[]
            {
                new Claim(TenantIdClaim, tenantId.ToString(CultureInfo.InvariantCulture)),
                new Claim(BranchIdClaim, branchId.ToString(CultureInfo.InvariantCulture)),
                new Claim(PurposeClaim, PurposeValue),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: creds);

            string jwt = new JwtSecurityTokenHandler().WriteToken(token);
            return new ReconciliationAccessToken(jwt, expiresAt);
        }
    }
}
