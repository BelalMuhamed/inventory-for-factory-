using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    /// <summary>EF Core implementation of <see cref="IRefreshTokenRepo"/>.</summary>
    public sealed class RefreshTokenRepo : GenericRepo<RefreshToken, long>, IRefreshTokenRepo
    {
        /// <summary>Creates the repository over the supplied context.</summary>
        /// <param name="context">The shared <see cref="AppDbContext"/>.</param>
        public RefreshTokenRepo(AppDbContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
            => await Set.FirstOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken);
    }
}
