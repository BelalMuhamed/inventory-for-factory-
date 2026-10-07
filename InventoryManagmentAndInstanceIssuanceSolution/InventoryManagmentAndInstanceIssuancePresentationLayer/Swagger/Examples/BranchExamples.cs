using System.Collections.Generic;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger.Examples
{
    /// <summary>
    /// Swagger examples for <c>BranchController</c> (<c>api/branches/*</c>). Bodies mirror
    /// <c>BranchDtos.cs</c> and the outcomes actually returned by <c>BranchService</c>
    /// (InfrastructureLayer/Services/BranchService.cs) and <c>BranchErrors</c>
    /// (ApplicationLayer/Errors/BranchErrors.cs).
    /// </summary>
    internal static class BranchExamples
    {
        private static readonly object SampleBranch = new
        {
            id = 7,
            tenantId = 42,
            name = "Downtown Branch",
            location = "12 Tahrir Square, Cairo",
            isActive = true,
            isDeleted = false,
            createdAt = "2026-02-01T09:00:00Z",
            updatedAt = (string?)null,
            deletedAt = (string?)null
        };

        public static IReadOnlyDictionary<EndpointKey, EndpointExampleSet> Build() =>
            new Dictionary<EndpointKey, EndpointExampleSet>
            {
                [new EndpointKey("BranchController", "GetAll")] = GetAll(),
                [new EndpointKey("BranchController", "GetById")] = GetById(),
                [new EndpointKey("BranchController", "Create")] = Create(),
                [new EndpointKey("BranchController", "Update")] = Update(),
                [new EndpointKey("BranchController", "Delete")] = Delete(),
                [new EndpointKey("BranchController", "Restore")] = Restore(),
                [new EndpointKey("BranchController", "Activate")] = Activate(),
                [new EndpointKey("BranchController", "Deactivate")] = Deactivate()
            };

        private static EndpointExampleSet GetAll() => new EndpointExampleSetBuilder()
            .Response(200, "page", "First page of branches for the caller's tenant.",
                new
                {
                    success = true,
                    data = new
                    {
                        data = new[] { SampleBranch },
                        pageNumber = 1,
                        pageSize = 20,
                        totalCount = 1,
                        totalPages = 1,
                        hasNextPage = false,
                        hasPreviousPage = false
                    },
                    error = (object?)null
                })
            .Build();

        private static EndpointExampleSet GetById() => new EndpointExampleSetBuilder()
            .Response(200, "found", "An active branch.",
                new { success = true, data = SampleBranch, error = (object?)null })
            .Response(404, "notFound", "No branch exists with that id (or it belongs to another tenant).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Branch.NotFound", message = "No branch was found with id 999.", category = "NotFound" }
                })
            .Build();

        private static EndpointExampleSet Create() => new EndpointExampleSetBuilder()
            .Request("tenantCaller", "A tenant creating one of its own branches — TenantId is ignored even if sent.",
                new { name = "Downtown Branch", location = "12 Tahrir Square, Cairo", isActive = true })
            .Request("systemAdminCaller", "A system admin creating a branch on behalf of a specific tenant.",
                new { name = "Downtown Branch", location = "12 Tahrir Square, Cairo", isActive = true, tenantId = 42 })
            .Response(200, "success", "The created branch.",
                new { success = true, data = SampleBranch, error = (object?)null })
            .Response(401, "actorNotResolved", "Rare edge case: the token passed authentication but " +
                "the acting tenant/admin identity couldn't be resolved from its claims. Not an " +
                "expected client-facing scenario — see the controller-level 401 doc for the far " +
                "more common empty-body case (no/invalid token).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Branch.ActorNotResolved", message = "The acting principal could not be resolved.", category = "Unauthorized" }
                })
            .Response(409, "nameTaken", "A branch with this name already exists for the tenant.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Branch.NameAlreadyExists", message = "A branch named 'Downtown Branch' already exists for this tenant.", category = "Conflict" }
                })
            .Response(422, "targetTenantNotFound", "A system-admin caller supplied a tenantId that doesn't exist.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Branch.TargetTenantNotFound", message = "No tenant exists with id 999.", category = "Validation" }
                })
            .Build();

        private static EndpointExampleSet Update() => new EndpointExampleSetBuilder()
            .Request("rename", "Renaming a branch and updating its location.",
                new { name = "Downtown Branch (Main)", location = "12 Tahrir Square, Cairo", isActive = true })
            .Response(200, "success", "The updated branch.",
                new { success = true, data = SampleBranch, error = (object?)null })
            .Response(404, "notFound", "No branch exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Branch.NotFound", message = "No branch was found with id 999.", category = "NotFound" }
                })
            .Response(409, "nameTaken", "The new name is already used by a different branch for this tenant.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Branch.NameAlreadyExists", message = "A branch named 'Downtown Branch (Main)' already exists for this tenant.", category = "Conflict" }
                })
            .Build();

        private static EndpointExampleSet Delete() => new EndpointExampleSetBuilder()
            .Response(200, "success", "The branch was soft-deleted; the payload is null.",
                new { success = true, data = (object?)null, error = (object?)null })
            .Response(404, "notFound", "No branch exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Branch.NotFound", message = "No branch was found with id 999.", category = "NotFound" }
                })
            .Response(409, "hasStock", "The branch holds non-zero stock — transfer or dispose of it first (API §4.5).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Branch.HasStock", message = "Branch 7 holds non-zero stock and cannot be deleted. Transfer or dispose of it first.", category = "Conflict" }
                })
            .Response(409, "hasInProgressTransfer", "The branch is the source or target of a transfer that hasn't settled yet.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Branch.HasInProgressTransfer", message = "Branch 7 has a transfer in progress and cannot be deleted until it is settled.", category = "Conflict" }
                })
            .Build();

        private static EndpointExampleSet Restore() => new EndpointExampleSetBuilder()
            .Response(200, "success", "The branch was restored; the payload is null.",
                new { success = true, data = (object?)null, error = (object?)null })
            .Response(404, "notFound", "No branch exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Branch.NotFound", message = "No branch was found with id 999.", category = "NotFound" }
                })
            .Response(409, "notDeleted", "The branch is not currently deleted, so it can't be restored.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Branch.NotDeleted", message = "Branch 7 is not deleted.", category = "Conflict" }
                })
            .Build();

        private static EndpointExampleSet Activate() => new EndpointExampleSetBuilder()
            .Response(200, "success", "The branch is now active. Idempotent — calling this on an already-active branch also returns 200.",
                new { success = true, data = SampleBranch, error = (object?)null })
            .Response(404, "notFound", "No branch exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Branch.NotFound", message = "No branch was found with id 999.", category = "NotFound" }
                })
            .Build();

        private static EndpointExampleSet Deactivate() => new EndpointExampleSetBuilder()
            .Response(200, "success", "The branch is now inactive. Idempotent.",
                new { success = true, data = SampleBranch, error = (object?)null })
            .Response(404, "notFound", "No branch exists with that id.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = "Branch.NotFound", message = "No branch was found with id 999.", category = "NotFound" }
                })
            .Build();
    }
}
