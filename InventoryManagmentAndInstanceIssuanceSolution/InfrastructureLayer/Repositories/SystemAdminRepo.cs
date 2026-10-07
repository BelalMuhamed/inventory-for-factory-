using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    /// <summary>EF Core implementation of <see cref="ISystemAdminRepo"/>.</summary>
    public sealed class SystemAdminRepo : GenericRepo<SystemAdmin, long>, ISystemAdminRepo
    {
        /// <summary>Creates the repository over the supplied context.</summary>
        /// <param name="context">The shared <see cref="AppDbContext"/>.</param>
        public SystemAdminRepo(AppDbContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public async Task<SystemAdmin?> GetActiveByUsernameAsync(string username, CancellationToken cancellationToken = default)
            => await Set.FirstOrDefaultAsync(
                a => a.Username == username && a.IsActive,
                cancellationToken);
    }
}
