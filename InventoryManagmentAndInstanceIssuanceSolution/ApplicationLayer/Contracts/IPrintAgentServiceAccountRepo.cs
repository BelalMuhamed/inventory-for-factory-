using System;
using System.Threading;
using System.Threading.Tasks;
using DomainLayer.Entities;

namespace ApplicationLayer.Contracts
{
    /// <summary>Data-access contract for <see cref="PrintAgentServiceAccount"/>.</summary>
    public interface IPrintAgentServiceAccountRepo : IGenericRepo<PrintAgentServiceAccount, long>
    {
        /// <summary>Finds an account by its public client id — the lookup used at token-mint time.</summary>
        Task<PrintAgentServiceAccount?> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default);
    }
}
