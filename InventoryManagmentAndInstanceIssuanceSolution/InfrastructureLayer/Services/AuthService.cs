using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Auth;
using ApplicationLayer.Errors;
using ApplicationLayer.Options;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// Authentication service implementing tenant/admin login, refresh-token rotation, and
    /// logout. Credential and token failures are returned as
    /// <see cref="ErrorCategory.Unauthorized"/> results, not exceptions. User-facing messages are
    /// localized via <see cref="IStringLocalizer"/> (Messages_en/Messages_ar).
    /// </summary>
    public sealed class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtTokenGenerator _tokenGenerator;
        private readonly IAuditLogger _auditLogger;
        private readonly JwtOptions _jwtOptions;
        private readonly ICurrentTenant _currentTenant;
        private readonly IPrintAgentTokenGenerator _printAgentTokenGenerator;
        private readonly IReconciliationTokenGenerator _reconciliationTokenGenerator;

        /// <summary>Creates the service with its collaborators (constructor injection only).</summary>
        public AuthService(
            IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher,
            IJwtTokenGenerator tokenGenerator
          ,
            IOptions<JwtOptions> jwtOptions,IAuditLogger auditLogger,
            ICurrentTenant currentTenant,
            IPrintAgentTokenGenerator printAgentTokenGenerator,
            IReconciliationTokenGenerator reconciliationTokenGenerator)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
            _tokenGenerator = tokenGenerator;
           
            _auditLogger = auditLogger;
            _jwtOptions = jwtOptions.Value;
            _currentTenant = currentTenant;
            _printAgentTokenGenerator = printAgentTokenGenerator;
            _reconciliationTokenGenerator = reconciliationTokenGenerator;
        }

        /// <inheritdoc />
        public async Task<Result<AuthResponse>> LoginTenantAsync(
            TenantLoginRequest request, CancellationToken cancellationToken = default)
        {
            Tenant? tenant = await _unitOfWork.Tenants
                .GetActiveByUsernameAsync(request.Username, cancellationToken);

            if (tenant is null || !_passwordHasher.Verify(tenant.PasswordHash, request.Password))
            {
                return Result.Failure<AuthResponse>(AuthErrors.InvalidCredentials());
            }

          
            AccessToken access = _tokenGenerator.CreateForTenant(tenant.Username, tenant.Id);
            AuthResponse response = await IssueWithRefreshAsync(
                access, request.Username, isSystemAdmin: false, tenantId: tenant.Id, cancellationToken);
            await _auditLogger.LogLoginAsync(tenant.Username, isSystemAdmin: false, tenantId: tenant.Id, cancellationToken);

            // Audit hook (Login) is raised by the SaveChanges interceptor / service hook in the
            // cross-cutting milestone; the refresh-token insert above provides its commit point.
            return response;
        }

        /// <inheritdoc />
        public async Task<Result<AuthResponse>> LoginSystemAdminAsync(
            AdminLoginRequest request, CancellationToken cancellationToken = default)
        {
            SystemAdmin? admin = await _unitOfWork.SystemAdmins
                .GetActiveByUsernameAsync(request.Username, cancellationToken);

            if (admin is null || !_passwordHasher.Verify(admin.PasswordHash, request.Password))
            {
                return Result.Failure<AuthResponse>(AuthErrors.InvalidCredentials());
            }

            AccessToken access = _tokenGenerator.CreateForSystemAdmin(admin.Username);
            AuthResponse response = await IssueWithRefreshAsync(
     access, request.Username, isSystemAdmin: true, tenantId: null, cancellationToken);

            await _auditLogger.LogLoginAsync(admin.Username, isSystemAdmin: true, tenantId: null, cancellationToken);


            return response;
        }

        /// <inheritdoc />
        public async Task<Result<AuthResponse>> RefreshAsync(
            RefreshRequest request, CancellationToken cancellationToken = default)
        {
            string presentedHash = HashToken(request.RefreshToken);
            RefreshToken? stored = await _unitOfWork.RefreshTokens
                .GetByHashAsync(presentedHash, cancellationToken);

            if (stored is null || !stored.IsActive(DateTime.UtcNow))
            {
                return Result.Failure<AuthResponse>(AuthErrors.InvalidRefreshToken());
            }

            // A tenant token minted before this column existed has no TenantId; force a fresh login
            // rather than mint a token without the tenantId claim (same posture as the IsSystemAdmin fix).
            if (!stored.IsSystemAdmin && stored.TenantId is null)
            {
                return Result.Failure<AuthResponse>(AuthErrors.InvalidRefreshToken());
            }

            AccessToken access = stored.IsSystemAdmin
                ? _tokenGenerator.CreateForSystemAdmin(stored.userName)
                : _tokenGenerator.CreateForTenant(stored.userName, stored.TenantId!.Value);

            (RefreshToken successor, string rawRefresh, DateTime refreshExpiry) =
                BuildRefreshToken(stored.userName, stored.IsSystemAdmin, stored.TenantId);

            stored.RevokedAt = DateTime.UtcNow;
            stored.ReplacedByTokenHash = successor.TokenHash;
            _unitOfWork.RefreshTokens.Update(stored);
            await _unitOfWork.RefreshTokens.AddAsync(successor, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new AuthResponse(access.Token, access.ExpiresAt, rawRefresh, refreshExpiry);
        }

        /// <inheritdoc />
        public async Task<Result> LogoutAsync(
            LogoutRequest request, CancellationToken cancellationToken = default)
        {
            string presentedHash = HashToken(request.RefreshToken);
            RefreshToken? stored = await _unitOfWork.RefreshTokens
                .GetByHashAsync(presentedHash, cancellationToken);

            // Idempotent: revoke if live; otherwise succeed silently so logout cannot be used to
            // probe whether a token exists.
            if (stored is not null && stored.RevokedAt is null)
            {
                stored.RevokedAt = DateTime.UtcNow;
                _unitOfWork.RefreshTokens.Update(stored);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result.Success();
        }

        /// <inheritdoc />
        public async Task<Result<PrintAgentTokenResponse>> CreatePrintAgentTokenAsync(
            CreatePrintAgentTokenRequest request, CancellationToken cancellationToken = default)
        {
            // A Print Agent token is always scoped to one tenant's branch/printer; a system-admin
            // caller has no tenant context to scope it to, so it is rejected outright rather than
            // guessed at (no assumption about "which tenant" is ever made here).
            if (_currentTenant.IsSystemAdmin || _currentTenant.TenantId is not long tenantId)
            {
                return Result.Failure<PrintAgentTokenResponse>(AuthErrors.PrintAgentTokenRequiresTenant());
            }

            Branch? branch = await _unitOfWork.Branches.GetByIdAsync(request.BranchId, cancellationToken);
            if (branch is null || branch.TenantId != tenantId)
            {
                return Result.Failure<PrintAgentTokenResponse>(AuthErrors.PrintAgentBranchNotFound());
            }

            Printer? printer = await _unitOfWork.Printers.GetByIdAsync(request.PrinterId, cancellationToken);
            if (printer is null || printer.TenantId != tenantId || printer.BranchId != request.BranchId)
            {
                return Result.Failure<PrintAgentTokenResponse>(AuthErrors.PrintAgentPrinterNotFound());
            }

            PrintAgentAccessToken token = _printAgentTokenGenerator.Create(tenantId, request.BranchId, request.PrinterId);
            return Result.Success(new PrintAgentTokenResponse(token.Token, token.ExpiresAt));
        }

        /// <inheritdoc />
        public async Task<Result<ServiceTokenResponse>> CreateServiceTokenAsync(
            ServiceTokenRequest request, CancellationToken cancellationToken = default)
        {
            PrintAgentServiceAccount? account =
                await _unitOfWork.ServiceAccounts.GetByClientIdAsync(request.ClientId, cancellationToken);

            // One code whether the client id doesn't exist or the secret is wrong - the same
            // no-existence-leak discipline as tenant/admin login, so this response never reveals
            // whether a given client id is even provisioned.
            if (account is null || !_passwordHasher.Verify(account.ClientSecretHash, request.ClientSecret))
            {
                return Result.Failure<ServiceTokenResponse>(AuthErrors.ServiceCredentialInvalid());
            }

            if (account.RevokedAt is not null)
            {
                return Result.Failure<ServiceTokenResponse>(AuthErrors.ServiceCredentialRevoked());
            }

            account.LastUsedAt = DateTime.UtcNow;
            _unitOfWork.ServiceAccounts.Update(account);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            ReconciliationAccessToken token = _reconciliationTokenGenerator.Create(account.TenantId, account.BranchId);
            return Result.Success(new ServiceTokenResponse(token.Token, token.ExpiresAt));
        }

        /// <inheritdoc />
        public async Task<Result<CreateServiceAccountResponse>> CreateServiceAccountAsync(
            CreateServiceAccountRequest request, CancellationToken cancellationToken = default)
        {
            // Authorization (system-admin only) is enforced by the SystemAdminOnly policy at the
            // controller - not re-checked here, so it isn't duplicated across every method that
            // policy already gates.
            Branch? branch = await _unitOfWork.Branches.GetByIdAsync(request.BranchId, cancellationToken);
            if (branch is null || branch.TenantId != request.TenantId)
            {
                return Result.Failure<CreateServiceAccountResponse>(AuthErrors.ServiceAccountBranchNotFound());
            }

            // The raw secret exists only in this method's local scope and the response returned
            // to the caller - never persisted, never logged.
            string rawSecret = GenerateOpaqueToken();
            var account = new PrintAgentServiceAccount
            {
                TenantId = request.TenantId,
                BranchId = request.BranchId,
                ClientId = Guid.NewGuid(),
                ClientSecretHash = _passwordHasher.Hash(rawSecret),
                Label = request.Label,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.ServiceAccounts.AddAsync(account, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(new CreateServiceAccountResponse(account.Id, account.ClientId, rawSecret, account.Label));
        }

        /// <inheritdoc />
        public async Task<Result> RevokeServiceAccountAsync(long id, CancellationToken cancellationToken = default)
        {
            PrintAgentServiceAccount? account = await _unitOfWork.ServiceAccounts.GetByIdAsync(id, cancellationToken);
            if (account is null)
            {
                return Result.Failure(AuthErrors.ServiceAccountNotFound());
            }

            // Idempotent: revoking an already-revoked account still succeeds, same posture as logout.
            if (account.RevokedAt is null)
            {
                account.RevokedAt = DateTime.UtcNow;
                _unitOfWork.ServiceAccounts.Update(account);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result.Success();
        }

        private async Task<AuthResponse> IssueWithRefreshAsync(
      AccessToken access, string userName, bool isSystemAdmin, long? tenantId, CancellationToken cancellationToken)
        {
            (RefreshToken token, string rawRefresh, DateTime refreshExpiry) =
                BuildRefreshToken(userName, isSystemAdmin, tenantId);

            await _unitOfWork.RefreshTokens.AddAsync(token, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new AuthResponse(access.Token, access.ExpiresAt, rawRefresh, refreshExpiry);
        }

        private (RefreshToken Entity, string Raw, DateTime ExpiresAt) BuildRefreshToken(
     string userName, bool isSystemAdmin, long? tenantId)
        {
            string raw = GenerateOpaqueToken();
            DateTime now = DateTime.UtcNow;
            DateTime expiresAt = now.AddDays(_jwtOptions.RefreshTokenDays);

            var entity = new RefreshToken
            {
                userName = userName,
                IsSystemAdmin = isSystemAdmin,
                TenantId = tenantId,
                TokenHash = HashToken(raw),
                CreatedAt = now,
                ExpiresAt = expiresAt
            };
            return (entity, raw, expiresAt);
        }

        private static string GenerateOpaqueToken()
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes);
        }

        private static string HashToken(string rawToken)
        {
            byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
            return Convert.ToHexString(hash);
        }

      
    }
}
