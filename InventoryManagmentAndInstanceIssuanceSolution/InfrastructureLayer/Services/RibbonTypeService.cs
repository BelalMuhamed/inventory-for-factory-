using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Printing;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;

namespace InfrastructureLayer.Services
{
    /// <inheritdoc cref="IRibbonTypeService"/>
    public sealed class RibbonTypeService : IRibbonTypeService
    {
        private readonly IUnitOfWork _unitOfWork;

        public RibbonTypeService(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        /// <inheritdoc />
        public async Task<Result<IReadOnlyList<RibbonTypeResponse>>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<RibbonType> ribbonTypes = await _unitOfWork.RibbonTypes.GetAllAsync(cancellationToken);
            IReadOnlyList<RibbonTypeResponse> data = ribbonTypes
                .Select(r => new RibbonTypeResponse(r.Id, r.Name, r.SdkCode))
                .ToList();

            return Result.Success(data);
        }
    }
}
