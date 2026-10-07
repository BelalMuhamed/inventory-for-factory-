using ApplicationLayer.Contracts;
using DomainLayer.Entities;
using Microsoft.AspNetCore.Identity;

namespace InfrastructureLayer.Security
{
    /// <summary>
    /// <see cref="IPasswordHasher"/> implementation backed by ASP.NET Core Identity's
    /// <see cref="PasswordHasher{TUser}"/>, which uses PBKDF2 (HMAC-SHA256, per-password salt,
    /// configurable iterations). The generic type parameter is irrelevant to the algorithm, so a
    /// lightweight marker is used.
    /// </summary>
    public sealed class Pbkdf2PasswordHasher : IPasswordHasher
    {
        private static readonly Tenant Marker = new();
        private readonly PasswordHasher<Tenant> _inner = new();

        /// <inheritdoc />
        public string Hash(string password) => _inner.HashPassword(Marker, password);

        /// <inheritdoc />
        public bool Verify(string hash, string password)
        {
            PasswordVerificationResult result = _inner.VerifyHashedPassword(Marker, hash, password);
            return result is PasswordVerificationResult.Success
                or PasswordVerificationResult.SuccessRehashNeeded;
        }
    }
}
