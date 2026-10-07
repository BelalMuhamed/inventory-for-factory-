namespace invetoryBackGroundServices.Options
{
    /// <summary>
    /// Settings for communicating with the physical card machine, bound from the
    /// <c>"MachineCommunication"</c> section.
    /// <para>
    /// Backend configuration rather than request data: a timeout is a property of the deployment
    /// environment, not something a caller should be able to set per request. This also resolves
    /// a pre-existing inconsistency - the old <c>httpPOSTGetInfoJson</c> set a 10-second timeout
    /// while <c>httpPOST</c> had its timeout line commented out entirely, silently falling back
    /// to the .NET default of roughly 100 seconds.
    /// </para>
    /// </summary>
    public sealed class MachineCommunicationOptions
    {
        /// <summary>Configuration section name these options bind from.</summary>
        public const string SectionName = "MachineCommunication";

        /// <summary>
        /// How long to wait for the machine to answer a single command, in seconds. Defaults to
        /// 10, matching the only timeout the pre-existing code actually set.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// Fixed port for <c>GetInfoJson</c> ("info.json"), separate from every other command's
        /// caller-supplied port. Defaults to the port the pre-existing (pre-async-migration) code
        /// hardcoded for this exact call.
        /// <para>
        /// A dedicated configuration value with a fixed default, deliberately not a hardcoded
        /// literal in code — every other port/timeout/URL in this service is configurable for the
        /// same reason a magic number here would be the one inconsistent exception. It was briefly
        /// made request-driven like every other command's port (approved decision, reconciliation-
        /// credential phase) on the theory that the info and action ports were the same value; that
        /// turned out to be wrong in practice — the info port must stay fixed independently of
        /// whatever the action port is doing, including during an in-progress App Service Update,
        /// which the shared-port behavior was actively interfering with.
        /// </para>
        /// </summary>
        public string InfoPort { get; set; } = "33201";
    }

    /// <summary>
    /// Cross-origin settings, bound from the <c>"Cors"</c> section. Replaces the previous
    /// <c>AllowAnyOrigin</c> policy, which allowed any page loaded in any browser that could
    /// reach this service to invoke commands that physically move and emboss cards.
    /// </summary>
    public sealed class CorsPolicyOptions
    {
        /// <summary>Configuration section name these options bind from.</summary>
        public const string SectionName = "Cors";

        /// <summary>
        /// Exact origins permitted to call this service (scheme + host + port, no trailing
        /// slash), e.g. <c>https://inventory.example.com</c>. Must be non-empty; startup fails
        /// otherwise rather than silently falling back to permitting everything.
        /// </summary>
        public string[] AllowedOrigins { get; set; } = [];
    }

    /// <summary>
    /// Settings for the file-based outbox (reliability plan, Phase 7), bound from the
    /// <c>"Outbox"</c> section.
    /// </summary>
    public sealed class OutboxOptions
    {
        /// <summary>Configuration section name these options bind from.</summary>
        public const string SectionName = "Outbox";

        /// <summary>
        /// Directory pending outbox entries are written to. Defaults to an <c>Outbox</c> folder
        /// next to the executable when not configured.
        /// </summary>
        public string Directory { get; set; } = string.Empty;

        /// <summary>
        /// How often the reconciliation job runs, in minutes. Converted to a Hangfire schedule via
        /// <c>Cron.MinuteInterval(n)</c> at startup - deliberately not a raw cron expression: the
        /// actual need here has only ever been "how often," never a more complex schedule, and a
        /// plain integer is trivially validated and impossible to misconfigure into an invalid
        /// cron string. Defaults to 30, matching the value this was hardcoded to before.
        /// </summary>
        public int ReconciliationIntervalMinutes { get; set; } = 30;
    }

    /// <summary>
    /// Settings for the shared AES-GCM file encryption service (used by log lines and Outbox
    /// files alike), bound from the <c>"FileEncryption"</c> section.
    /// </summary>
    public sealed class FileEncryptionOptions
    {
        /// <summary>Configuration section name these options bind from.</summary>
        public const string SectionName = "FileEncryption";

        /// <summary>
        /// Base64-encoded AES-256 key (32 raw bytes once decoded). Supplied via user-secrets
        /// (development) or an environment variable (production); never committed to
        /// <c>appsettings.json</c> - same convention as <c>PrintAgentAuth:SigningKey</c> and
        /// <c>ReconciliationCredential:ClientSecret</c>. A dedicated key for this purpose alone,
        /// not reused from either of those - same one-key-per-purpose reasoning applied everywhere
        /// else in this service.
        /// </summary>
        public string Key { get; set; } = string.Empty;
    }

    /// <summary>
    /// The Printer Agent's own standing credential for the background outbox reconciliation job
    /// (Matica Print Flow, reconciliation-credential phase), bound from the
    /// <c>"ReconciliationCredential"</c> section. Exchanged for a short-lived access token via
    /// <c>POST api/auth/service-token</c> once per reconciliation run - never reused from, and
    /// never confused with, any user-delegated Print Agent token.
    /// </summary>
    public sealed class ReconciliationCredentialOptions
    {
        /// <summary>Configuration section name these options bind from.</summary>
        public const string SectionName = "ReconciliationCredential";

        /// <summary>
        /// The service account's public identifier (a GUID, not a secret) - safe to keep in
        /// <c>appsettings.json</c>, matching the Inventory API's own <c>ClientId</c> field.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// The service account's secret. Supplied via user-secrets (development) or an
        /// environment variable (production); never committed to <c>appsettings.json</c>, same
        /// convention as every other secret in this service.
        /// </summary>
        public string ClientSecret { get; set; } = string.Empty;
    }
}
