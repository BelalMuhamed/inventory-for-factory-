using System.Threading;
using System.Threading.Tasks;
using DomainLayer.Entities;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Repository for persisted <see cref="RefreshToken"/> rows backing refresh-token rotation
    /// and logout. Tokens are looked up by their stored hash, never by raw value.
    /// </summary>
    public interface IRefreshTokenRepo : IGenericRepo<RefreshToken, long>
    {
        /// <summary>
        /// Finds a refresh token by its stored hash, regardless of revocation or expiry state.
        /// Liveness is evaluated by the caller via <see cref="RefreshToken.IsActive"/>.
        /// </summary>
        /// <param name="tokenHash">Hash of the opaque refresh-token value.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        /// <returns>The matching token row, or <c>null</c> when none exists.</returns>
        Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    }
}
