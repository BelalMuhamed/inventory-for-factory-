using System.Collections.Generic;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger.Examples
{
    /// <summary>
    /// Swagger examples for <c>TenantsController</c> (<c>api/tenants/*</c>). Bodies mirror
    /// <c>TenantDtos.cs</c> and the outcomes actually returned by <c>TenantService</c>
    /// (InfrastructureLayer/Services/TenantService.cs) and <c>TenantErrors</c>
    /// (ApplicationLayer/Errors/TenantErrors.cs). Every action here also requires a
    /// system-admin bearer token (<c>AuthorizationPolicies.SystemAdminOnly</c>) — see the
    /// 401/403 notes on the controller itself for why those two codes carry no example body on
    /// this module.
    /// </summary>
    internal static class TenantExamples
    {
        private static readonly object SampleTenant = new
        {
            id = 42,
            username = "acme-bank",
            code = "acme-bank",
            isActive = true,
            isDeleted = false,
            createdAt = "2026-01-15T09:30:00Z",
            updatedAt = (string?)null,
            deletedAt = (string?)null
        };

        private static readonly object SampleDeletedTenant = new
        {
            id = 43,
            username = "legacy-bank",
            code = "legacy-bank",
            isActive = false,
            isDeleted = true,
            createdAt = "2025-11-02T10:00:00Z",
            updatedAt = "2026-02-01T08:00:00Z",
            deletedAt = "2026-06-30T17:45:00Z"
        };

        public static IReadOnlyDictionary<EndpointKey, EndpointExampleSet> Build() =>
            new Dictionary<EndpointKey, EndpointExampleSet>
            {
                [new EndpointKey("TenantsController", "GetAll")] = GetAll(),
                [new EndpointKey("TenantsController", "GetById")] = GetById(),
                [new EndpointKey("TenantsController", "Create")] = Create(),
                [new EndpointKey("TenantsController", "Update")] = Update(),
                [new EndpointKey("TenantsController", "ChangePassword")] = ChangePassword(),
                [new EndpointKey("TenantsController", "SoftDelete")] = SoftDelete(),
                [new EndpointKey("TenantsController", "Restore")] = Restore()
            };

        private static EndpointExampleSet GetAll() => new EndpointExampleSetBuilder()
            .Response(200, "page", "First page of tenants, default page size.",
                new
                {
                    success = true,
                    data = new
                    {
                        data = new[] { SampleTenant, SampleDeletedTenant },
                        pageNumber = 1,
                        pageSize = 20,
                        totalCount = 2,
                        totalPages = 1,
                        hasNextPage = false,
                        hasPreviousPage = false
                    },
                    error = (object?)null
                })
            .Response(422, "badQueryValue", "A query parameter couldn't be bound (e.g. " +
                "?page=abc). Exact key casing/wording comes from ASP.NET Core's default query " +
                "binder; this shows the envelope shape.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new
                    {
                        code = "Validation.Failed",
                        message = "One or more validation errors occurred.",
                        category = "Validation",
                        validationErrors = new Dictionary<string, string[]>
                        {
                            ["Page"] = new[] { "The value 'abc' is not valid for Page." }
                        }
                    }
                })
            .Build();

        private static EndpointExampleSet GetById() => new EndpointExampleSetBuilder()
            .Response(200, "found", "An active tenant.", new
            {
                success = true,
                data = SampleTenant,
                error = (object?)null
            })
            .Response(404, "notFound", "No tenant exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new
                    {
                        code = "Tenant.NotFound",
                        message = "No tenant was found with id 999.",
                        category = "NotFound"
                    }
                })
            .Build();

        private static EndpointExampleSet Create() => new EndpointExampleSetBuilder()
            .Request("newTenant", "Onboarding a new bank tenant, active immediately.",
                new { username = "acme-bank", code = "acme-bank", password = "P@ssw0rd!23", isActive = true })
            .Request("newInactiveTenant", "Creating a tenant that starts deactivated (e.g. " +
                "pending onboarding paperwork) — activate it later via a separate call.",
                new { username = "acme-bank", code = "acme-bank", password = "P@ssw0rd!23", isActive = false })
            .Response(200, "success", "The created tenant.",
                new { success = true, data = SampleTenant, error = (object?)null })
            .Response(409, "usernameTaken", "The username is already used by another tenant, " +
                "including a soft-deleted one — deleted tenants' identifiers stay reserved.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new
                    {
                        code = "Tenant.UsernameAlreadyExists",
                        message = "A tenant with username 'acme-bank' already exists.",
                        category = "Conflict"
                    }
                })
            .Response(409, "codeTaken", "The code is already used by another tenant, including a soft-deleted one.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new
                    {
                        code = "Tenant.CodeAlreadyExists",
                        message = "A tenant with code 'acme-bank' already exists.",
                        category = "Conflict"
                    }
                })
            .Response(422, "missingField", "A required field was omitted.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new
                    {
                        code = "Validation.Failed",
                        message = "One or more validation errors occurred.",
                        category = "Validation",
                        validationErrors = new Dictionary<string, string[]>
                        {
                            ["Password"] = new[] { "The Password field is required." }
                        }
                    }
                })
            .Build();

        private static EndpointExampleSet Update() => new EndpointExampleSetBuilder()
            .Request("rename", "Renaming a tenant and flipping it active — password is untouched " +
                "(changed only via PUT /{id}/password).",
                new { username = "acme-bank-renamed", code = "acme-bank", isActive = true })
            .Response(200, "success", "The updated tenant.",
                new { success = true, data = SampleTenant, error = (object?)null })
            .Response(404, "notFound", "No tenant exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new
                    {
                        code = "Tenant.NotFound",
                        message = "No tenant was found with id 999.",
                        category = "NotFound"
                    }
                })
            .Response(409, "usernameTaken", "The new username is already used by a different tenant.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new
                    {
                        code = "Tenant.UsernameAlreadyExists",
                        message = "A tenant with username 'acme-bank-renamed' already exists.",
                        category = "Conflict"
                    }
                })
            .Response(422, "missingField", "A required field was omitted.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new
                    {
                        code = "Validation.Failed",
                        message = "One or more validation errors occurred.",
                        category = "Validation",
                        validationErrors = new Dictionary<string, string[]>
                        {
                            ["Code"] = new[] { "The Code field is required." }
                        }
                    }
                })
            .Build();

        private static EndpointExampleSet ChangePassword() => new EndpointExampleSetBuilder()
            .Request("newPassword", "Resetting a tenant's password. No current-password " +
                "confirmation is required — the system-admin token itself is the authorization.",
                new { newPassword = "N3wStr0ngP@ss!" })
            .Response(200, "success", "The password was changed; the payload is null.",
                new { success = true, data = (object?)null, error = (object?)null })
            .Response(404, "notFound", "No tenant exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new
                    {
                        code = "Tenant.NotFound",
                        message = "No tenant was found with id 999.",
                        category = "NotFound"
                    }
                })
            .Response(422, "missingField", "The new password was omitted.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new
                    {
                        code = "Validation.Failed",
                        message = "One or more validation errors occurred.",
                        category = "Validation",
                        validationErrors = new Dictionary<string, string[]>
                        {
                            ["NewPassword"] = new[] { "The NewPassword field is required." }
                        }
                    }
                })
            .Build();

        private static EndpointExampleSet SoftDelete() => new EndpointExampleSetBuilder()
            .Response(200, "success", "The tenant was soft-deleted; the payload is null.",
                new { success = true, data = (object?)null, error = (object?)null })
            .Response(401, "actorNotResolved", "Rare edge case: the bearer token passed the " +
                "system-admin policy check, but the acting admin's identity couldn't be resolved " +
                "from it. A defensive check, not an expected client-facing scenario — see the " +
                "controller-level 401 doc for the far more common empty-body case (no/invalid token).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new
                    {
                        code = "Tenant.ActorNotResolved",
                        message = "The acting administrator could not be resolved.",
                        category = "Unauthorized"
                    }
                })
            .Response(404, "notFound", "No tenant exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new
                    {
                        code = "Tenant.NotFound",
                        message = "No tenant was found with id 999.",
                        category = "NotFound"
                    }
                })
            .Response(409, "alreadyDeleted", "The tenant is already soft-deleted.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new
                    {
                        code = "Tenant.AlreadyDeleted",
                        message = "Tenant 43 is already deleted.",
                        category = "Conflict"
                    }
                })
            .Build();

        private static EndpointExampleSet Restore() => new EndpointExampleSetBuilder()
            .Response(200, "success", "The tenant was restored; the payload is null.",
                new { success = true, data = (object?)null, error = (object?)null })
            .Response(404, "notFound", "No tenant exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new
                    {
                        code = "Tenant.NotFound",
                        message = "No tenant was found with id 999.",
                        category = "NotFound"
                    }
                })
            .Response(409, "notDeleted", "The tenant is not currently deleted, so it can't be restored.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new
                    {
                        code = "Tenant.NotDeleted",
                        message = "Tenant 42 is not deleted.",
                        category = "Conflict"
                    }
                })
            .Build();
    }
}
