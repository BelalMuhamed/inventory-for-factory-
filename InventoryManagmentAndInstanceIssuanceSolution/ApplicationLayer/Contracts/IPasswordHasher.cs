namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Abstraction over password hashing and verification. Keeps the concrete algorithm
    /// (PBKDF2, via ASP.NET Core Identity's hasher) in the infrastructure layer so the
    /// application layer depends only on this contract.
    /// </summary>
    public interface IPasswordHasher
    {
        /// <summary>Produces a salted hash for the supplied plaintext password.</summary>
        /// <param name="password">Plaintext password to hash.</param>
        /// <returns>An encoded hash suitable for persistence.</returns>
        string Hash(string password);

        /// <summary>
        /// Verifies a plaintext password against a stored hash.
        /// </summary>
        /// <param name="hash">The stored password hash.</param>
        /// <param name="password">The plaintext password to verify.</param>
        /// <returns><c>true</c> when the password matches the hash; otherwise <c>false</c>.</returns>
        bool Verify(string hash, string password);
    }
}
