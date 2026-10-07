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
    /// Issues short-lived, narrowly-scoped tokens for the Matica Printer Agent (Matica Print Flow,
    /// backend-validation phase). Deliberately a separate class from <see cref="JwtTokenGenerator"/>
    /// — not a bloated addition to it — since minting a tenant/admin session token and minting a
    /// five-minute device-scoped token are different responsibilities with different signing keys
    /// (<see cref="PrintAgentTokenOptions"/>, not the shared <see cref="JwtOptions"/> key).
    /// <para>
    /// A branch machine running the Printer Agent only ever needs to validate <em>this</em> token
    /// locally, using only this dedicated key — it never needs, and never receives, the key that
    /// signs full tenant/admin sessions.
    /// </para>
    /// </summary>
    public sealed class PrintAgentTokenGenerator : IPrintAgentTokenGenerator
    {
        /// <summary>Name of the ASP.NET Core authentication scheme this token is validated under.</summary>
        public const string AuthenticationScheme = "PrintAgent";

        /// <summary>Claim carrying the tenant id the token is scoped to.</summary>
        public const string TenantIdClaim = "tenantId";

        /// <summary>Claim carrying the branch id the token is scoped to.</summary>
        public const string BranchIdClaim = "branchId";

        /// <summary>Claim carrying the printer id the token is scoped to.</summary>
        public const string PrinterIdClaim = "printerId";

        /// <summary>
        /// Fixed claim identifying this token as a Print Agent token, checked by the
        /// <c>PrintAgentOnly</c> authorization policy as a defense-in-depth check alongside the
        /// dedicated signing key and audience — belt-and-braces, not load-bearing on its own.
        /// </summary>
        public const string PurposeClaim = "purpose";

        /// <summary>The only valid <see cref="PurposeClaim"/> value.</summary>
        public const string PurposeValue = "print-agent";

        private readonly PrintAgentTokenOptions _options;

        /// <summary>Creates the generator from its bound options.</summary>
        public PrintAgentTokenGenerator(IOptions<PrintAgentTokenOptions> options)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc />
        public PrintAgentAccessToken Create(long tenantId, long branchId, long printerId)
        {
            DateTime expiresAt = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);

            var claims = new[]
            {
                new Claim(TenantIdClaim, tenantId.ToString(CultureInfo.InvariantCulture)),
                new Claim(BranchIdClaim, branchId.ToString(CultureInfo.InvariantCulture)),
                new Claim(PrinterIdClaim, printerId.ToString(CultureInfo.InvariantCulture)),
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
            return new PrintAgentAccessToken(jwt, expiresAt);
        }
    }
}
