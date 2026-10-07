// InfrastructureLayer/Services/TenantService.cs
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Tenants;
using ApplicationLayer.Errors;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// Tenant management service (API Spec §4.2). Business outcomes are returned as
    /// <see cref="Result"/> values; the unit of work commits each operation as a single
    /// transaction. Uniqueness is checked across all rows (including soft-deleted) per the
    /// agreed rule that a deleted tenant's code/username stays reserved.
    /// </summary>
    public sealed class TenantService : ITenantService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;

        /// <summary>Creates the service with its collaborators (constructor injection only).</summary>
        public TenantService(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
        }

        /// <inheritdoc />
        public async Task<Result<PaginatedResponse<TenantResponse>>> GetAllAsync(
            TenantListFilter filter, CancellationToken cancellationToken = default)
        {
            (IReadOnlyList<Tenant> items, int totalCount) =
                await _unitOfWork.Tenants.GetPagedAsync(filter, cancellationToken);

            IReadOnlyList<TenantResponse> data = items.Select(Map).ToList();

            PaginatedResponse<TenantResponse> page =
                PaginatedResponse<TenantResponse>.Create(data, filter.Page, filter.PageSize, totalCount);

            return page;
        }

        /// <inheritdoc />
        public async Task<Result<TenantResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            Tenant? tenant = await _unitOfWork.Tenants.GetByIdIncludingDeletedAsync(id, cancellationToken);

            return tenant is null
                ? Result.Failure<TenantResponse>(TenantErrors.NotFound(id))
                : Map(tenant);
        }

        /// <inheritdoc />
        public async Task<Result<TenantResponse>> CreateAsync(
            CreateTenantRequest request, CancellationToken cancellationToken = default)
        {
            if (await _unitOfWork.Tenants.UsernameExistsAsync(request.Username, null, cancellationToken))
            {
                return Result.Failure<TenantResponse>(TenantErrors.UsernameAlreadyExists(request.Username));
            }

            if (await _unitOfWork.Tenants.CodeExistsAsync(request.Code, null, cancellationToken))
            {
                return Result.Failure<TenantResponse>(TenantErrors.CodeAlreadyExists(request.Code));
            }

            var tenant = new Tenant
            {
                Username = request.Username,
                Code = request.Code,
                IsActive = request.IsActive,
                PasswordHash = _passwordHasher.Hash(request.Password)
            };

            await _unitOfWork.Tenants.AddAsync(tenant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Map(tenant);
        }

        /// <inheritdoc />
        public async Task<Result<TenantResponse>> UpdateAsync(
            long id, UpdateTenantRequest request, CancellationToken cancellationToken = default)
        {
            Tenant? tenant = await _unitOfWork.Tenants.GetByIdIncludingDeletedAsync(id, cancellationToken);
            if (tenant is null)
            {
                return Result.Failure<TenantResponse>(TenantErrors.NotFound(id));
            }

            if (await _unitOfWork.Tenants.UsernameExistsAsync(request.Username, id, cancellationToken))
            {
                return Result.Failure<TenantResponse>(TenantErrors.UsernameAlreadyExists(request.Username));
            }

            if (await _unitOfWork.Tenants.CodeExistsAsync(request.Code, id, cancellationToken))
            {
                return Result.Failure<TenantResponse>(TenantErrors.CodeAlreadyExists(request.Code));
            }

            tenant.Username = request.Username;
            tenant.Code = request.Code;
            tenant.IsActive = request.IsActive;

            _unitOfWork.Tenants.Update(tenant);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Map(tenant);
        }

        /// <inheritdoc />
        public async Task<Result> ChangePasswordAsync(
            long id, ChangeTenantPasswordRequest request, CancellationToken cancellationToken = default)
        {
            Tenant? tenant = await _unitOfWork.Tenants.GetByIdIncludingDeletedAsync(id, cancellationToken);
            if (tenant is null)
            {
                return Result.Failure(TenantErrors.NotFound(id));
            }

            tenant.PasswordHash = _passwordHasher.Hash(request.NewPassword);

            _unitOfWork.Tenants.Update(tenant);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }

        public async Task<Result> SoftDeleteAsync(long id, string actorUsername, CancellationToken cancellationToken = default)
        {
            Tenant? tenant = await _unitOfWork.Tenants.GetByIdIncludingDeletedAsync(id, cancellationToken);
            if (tenant is null)
            {
                return Result.Failure(TenantErrors.NotFound(id));
            }

            if (tenant.IsDeleted)
            {
                return Result.Failure(TenantErrors.AlreadyDeleted(id));
            }

            // The delete endpoint is system-admin-only, so the actor is resolved from the SystemAdmins table.
            SystemAdmin? actor = await _unitOfWork.SystemAdmins.GetActiveByUsernameAsync(actorUsername, cancellationToken);
            if (actor is null)
            {
                return Result.Failure(TenantErrors.ActorNotResolved());
            }

            tenant.IsDeleted = true;
            tenant.DeletedAt = System.DateTime.UtcNow;
            tenant.DeletedBy = actor.Id;

            _unitOfWork.Tenants.Update(tenant);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        /// <inheritdoc />
        public async Task<Result> RestoreAsync(long id, CancellationToken cancellationToken = default)
        {
            Tenant? tenant = await _unitOfWork.Tenants.GetByIdIncludingDeletedAsync(id, cancellationToken);
            if (tenant is null)
            {
                return Result.Failure(TenantErrors.NotFound(id));
            }

            if (!tenant.IsDeleted)
            {
                return Result.Failure(TenantErrors.NotDeleted(id));
            }

            tenant.IsDeleted = false;
            tenant.DeletedAt = null;
            tenant.DeletedBy = null;

            _unitOfWork.Tenants.Update(tenant);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }

        // Projects a tenant to its DTO. The password hash is never exposed.
        private static TenantResponse Map(Tenant tenant) => new(
            tenant.Id,
            tenant.Username,
            tenant.Code,
            tenant.IsActive,
            tenant.IsDeleted,
            tenant.CreatedAt,
            tenant.UpdatedAt,
            tenant.DeletedAt);
    }
}