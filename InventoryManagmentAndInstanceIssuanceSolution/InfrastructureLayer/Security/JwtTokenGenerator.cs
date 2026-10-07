using ApplicationLayer.Contracts;
using ApplicationLayer.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
// InfrastructureLayer/Security/JwtTokenGenerator.cs  (claim assembly — key parts)
namespace InfrastructureLayer.Security
{
    public sealed class JwtTokenGenerator : IJwtTokenGenerator
    {
        private readonly JwtOptions _jwtOptions;

        public JwtTokenGenerator(IOptions<JwtOptions> jwtOptions)
        {
            _jwtOptions = jwtOptions?.Value ?? throw new ArgumentNullException(nameof(jwtOptions));
        }

        /// <summary>Claim carrying the principal's username (tenant or system admin).</summary>
        public const string UsernameClaim = "username";

        /// <summary>Claim flagging a system-admin token.</summary>
        public const string IsSystemAdminClaim = "isSystemAdmin";

        public long? TenantId => throw new NotImplementedException();

        /// <inheritdoc />
        public AccessToken CreateForTenant(string username) =>
            CreateToken(username, isSystemAdmin: false);

     

        private AccessToken CreateToken(string username, bool isSystemAdmin)
        {
            DateTime expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes);

            var claims = new[]
            {
                new Claim(UsernameClaim, username),
                new Claim(IsSystemAdminClaim, isSystemAdmin ? "true" : "false"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: creds);

            string jwt = new JwtSecurityTokenHandler().WriteToken(token);
            return new AccessToken(jwt, expiresAt);
        }
        /// <summary>Claim carrying the authenticated tenant's id (tenant tokens only).</summary>
        public const string TenantIdClaim = "tenantId";

        public AccessToken CreateForTenant(string username, long tenantId) =>
            CreateToken(username, isSystemAdmin: false, tenantId: tenantId);

        public AccessToken CreateForSystemAdmin(string username) =>
            CreateToken(username, isSystemAdmin: true, tenantId: null);

        private AccessToken CreateToken(string username, bool isSystemAdmin, long? tenantId)
        {
            DateTime expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes);

            var claims = new List<Claim>
    {
        new(UsernameClaim, username),
        new(IsSystemAdminClaim, isSystemAdmin ? "true" : "false"),
        new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };

            if (tenantId.HasValue)
            {
                claims.Add(new Claim(TenantIdClaim, tenantId.Value.ToString(CultureInfo.InvariantCulture)));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: creds);

            return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }
    }
}