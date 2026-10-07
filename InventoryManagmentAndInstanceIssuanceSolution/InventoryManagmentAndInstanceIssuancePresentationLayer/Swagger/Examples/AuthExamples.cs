using System.Collections.Generic;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger.Examples
{
    /// <summary>
    /// Swagger examples for <c>AuthController</c> (<c>api/auth/*</c>). Bodies mirror
    /// <c>AuthDtos.cs</c> and the outcomes actually returned by <c>AuthService</c>
    /// (InfrastructureLayer/Services/AuthService.cs) and <c>AuthErrors</c>
    /// (ApplicationLayer/Errors/AuthErrors.cs) — there is no separate error code for a bad
    /// tenant login vs. a bad admin login; both use <c>Auth.InvalidCredentials</c>.
    /// </summary>
    internal static class AuthExamples
    {
        private const string AccessTokenSample =
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
            "eyJ1c2VybmFtZSI6ImFjbWUtYmFuayIsInRlbmFudElkIjoiNDIiLCJpc1N5c3RlbUFkbWluIjoiZmFsc2UiLCJleHAiOjE3ODY1NTUyMDB9." +
            "3f5a8c1e6b9d2f4a7c0e3b6d9f2a5c8e1b4d7f0a3c6e9b2d5f8a1c4e7b0d3f6a";

        public static IReadOnlyDictionary<EndpointKey, EndpointExampleSet> Build() =>
            new Dictionary<EndpointKey, EndpointExampleSet>
            {
                [new EndpointKey("AuthController", "LoginTenant")] = LoginTenant(),
                [new EndpointKey("AuthController", "LoginAdmin")] = LoginAdmin(),
                [new EndpointKey("AuthController", "Refresh")] = Refresh(),
                [new EndpointKey("AuthController", "Logout")] = Logout(),
                [new EndpointKey("AuthController", "Me")] = Me()
            };

        private static EndpointExampleSet LoginTenant() => new EndpointExampleSetBuilder()
            .Request("tenantCredentials", "A tenant logging in with its username and password.",
                new { username = "acme-bank", password = "P@ssw0rd!23" })
            .Response(200, "success", "Login succeeded; a fresh access/refresh token pair.",
                new
                {
                    success = true,
                    data = new
                    {
                        accessToken = AccessTokenSample,
                        accessTokenExpiresAt = "2026-08-12T16:00:00Z",
                        refreshToken = "Kj8mQ2xN0pLr6vTs4wYb1cZaFhGdEeUu3iOoPpAqBbCc=",
                        refreshTokenExpiresAt = "2026-08-19T08:00:00Z"
                    },
                    error = (object?)null
                })
            .Response(401, "invalidCredentials", "Wrong username/password, or the tenant is inactive.",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new
                    {
                        code = "Auth.InvalidCredentials",
                        message = "Invalid username or password.",
                        category = "Unauthorized"
                    }
                })
            .Response(422, "missingField", "A required field was omitted. Exact key casing/wording " +
                "comes from ASP.NET Core's default model binder; this shows the envelope shape.",
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

        private static EndpointExampleSet LoginAdmin() => new EndpointExampleSetBuilder()
            .Request("adminCredentials", "The bootstrap system admin logging in.",
                new { username = "sysadmin", password = "Adm!nSecret456" })
            .Response(200, "success", "Login succeeded; the JWT carries isSystemAdmin=true and no tenantId claim.",
                new
                {
                    success = true,
                    data = new
                    {
                        accessToken = AccessTokenSample,
                        accessTokenExpiresAt = "2026-08-12T16:00:00Z",
                        refreshToken = "Wq4nR7yM1oKp5uTr3vXa0bYcGiHfDdVv2jNnOoBpAaCc=",
                        refreshTokenExpiresAt = "2026-08-19T08:00:00Z"
                    },
                    error = (object?)null
                })
            .Response(401, "invalidCredentials", "Wrong username/password, or the admin is inactive " +
                "(same Auth.InvalidCredentials code as a failed tenant login).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new
                    {
                        code = "Auth.InvalidCredentials",
                        message = "Invalid username or password.",
                        category = "Unauthorized"
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
                            ["Username"] = new[] { "The Username field is required." }
                        }
                    }
                })
            .Build();

        private static EndpointExampleSet Refresh() => new EndpointExampleSetBuilder()
            .Request("refresh", "Exchanging a still-valid refresh token for a new pair.",
                new { refreshToken = "Kj8mQ2xN0pLr6vTs4wYb1cZaFhGdEeUu3iOoPpAqBbCc=" })
            .Response(200, "success", "Rotation succeeded; the old refresh token is now revoked " +
                "and this new one replaces it.",
                new
                {
                    success = true,
                    data = new
                    {
                        accessToken = AccessTokenSample,
                        accessTokenExpiresAt = "2026-08-12T20:00:00Z",
                        refreshToken = "Nn3pQ8xW2oJr7vZs5wYc1dAbHiGfEeUu4jOoPpBqCaDd=",
                        refreshTokenExpiresAt = "2026-08-19T12:00:00Z"
                    },
                    error = (object?)null
                })
            .Response(401, "invalidRefreshToken", "The token is unknown, expired, already-rotated, " +
                "or revoked (e.g. after logout).",
                new
                {
                    success = false,
                    data = (object?)null,
                    error = new
                    {
                        code = "Auth.InvalidRefreshToken",
                        message = "The refresh token is invalid or expired.",
                        category = "Unauthorized"
                    }
                })
            .Response(422, "missingField", "The refresh token was omitted from the body.",
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
                            ["RefreshToken"] = new[] { "The RefreshToken field is required." }
                        }
                    }
                })
            .Build();

        private static EndpointExampleSet Logout() => new EndpointExampleSetBuilder()
            .Request("logout", "Revoking the refresh token issued at login. Requires " +
                "Authorization: Bearer <accessToken> as well — see the endpoint's security requirement.",
                new { refreshToken = "Kj8mQ2xN0pLr6vTs4wYb1cZaFhGdEeUu3iOoPpAqBbCc=" })
            .Response(200, "success", "Logout is idempotent: succeeds whether or not the token " +
                "was still active.",
                new { success = true, data = (object?)null, error = (object?)null })
            .Response(422, "missingField", "The refresh token was omitted from the body.",
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
                            ["RefreshToken"] = new[] { "The RefreshToken field is required." }
                        }
                    }
                })
            .Build();

        private static EndpointExampleSet Me() => new EndpointExampleSetBuilder()
            .Response(200, "tenant", "Caller is an authenticated tenant.",
                new
                {
                    success = true,
                    data = new { username = "acme-bank", isSystemAdmin = false },
                    error = (object?)null
                })
            .Response(200, "systemAdmin", "Caller is the bootstrap system admin.",
                new
                {
                    success = true,
                    data = new { username = "sysadmin", isSystemAdmin = true },
                    error = (object?)null
                })
            .Build();
    }
}
