using System;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    /// <summary>EF Core repository for <see cref="PrintAgentServiceAccount"/>.</summary>
    public sealed class PrintAgentServiceAccountRepo : GenericRepo<PrintAgentServiceAccount, long>, IPrintAgentServiceAccountRepo
    {
        public PrintAgentServiceAccountRepo(AppDbContext context) : base(context) { }

        /// <inheritdoc />
        public Task<PrintAgentServiceAccount?> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default) =>
            Set.SingleOrDefaultAsync(a => a.ClientId == clientId, cancellationToken);
    }
}
