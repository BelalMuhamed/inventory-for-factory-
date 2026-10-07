using System.Threading;
using System.Threading.Tasks;
using DomainLayer.Entities;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Repository for the bootstrap <see cref="SystemAdmin"/> account (API Spec §2.4).
    /// </summary>
    public interface ISystemAdminRepo : IGenericRepo<SystemAdmin, long>
    {
        /// <summary>
        /// Finds an active, non-deleted system administrator by username.
        /// </summary>
        /// <param name="username">The administrator login username.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        /// <returns>The matching administrator, or <c>null</c> when none is found or it is inactive.</returns>
        Task<SystemAdmin?> GetActiveByUsernameAsync(string username, CancellationToken cancellationToken = default);
    }
}
