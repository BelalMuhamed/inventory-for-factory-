namespace ApplicationLayer.Options
{
    /// <summary>
    /// Settings for print-configuration image uploads, bound from the <c>"PrintImages"</c>
    /// section.
    /// </summary>
    public sealed class PrintImageOptions
    {
        /// <summary>Configuration section name.</summary>
        public const string SectionName = "PrintImages";

        /// <summary>
        /// Physical root directory images are written under, resolved relative to the
        /// application's content root (<see cref="Microsoft.Extensions.Hosting.IHostEnvironment.ContentRootPath"/>,
        /// the same base Program.cs already resolves <c>LogFileOptions.Directory</c>
        /// against) when not already absolute — see
        /// <c>LocalDiskPrintImageStorage.ResolvePhysicalRoot</c>. Each tenant gets its own
        /// subdirectory beneath this root, named after their (sanitized) username — e.g.
        /// <c>{RootPath}/acme-corp/student-card-front.png</c> — not their numeric id.
        /// <para>
        /// <b>Revision note:</b> images are no longer served as static files (no public URL
        /// exists for this content anymore); every retrieval goes through the authenticated
        /// <c>GET /api/print-images/{id}</c> endpoint, which enforces tenant ownership
        /// server-side before streaming a single byte. There is deliberately no
        /// <c>PublicBaseUrl</c> setting any more.
        /// </para>
        /// </summary>
        public string RootPath { get; set; } = "uploads/products";

        /// <summary>Maximum accepted upload size in bytes. Defaults to 5 MB.</summary>
        public long MaxSizeBytes { get; set; } = 5 * 1024 * 1024;

        /// <summary>
        /// Allowed file extensions (lowercase, with leading dot). Extension is one input to the
        /// upload check; the file's actual content is independently verified via magic-byte
        /// signature sniffing — an allowed extension alone never admits a file.
        /// </summary>
        public string[] AllowedExtensions { get; set; } = { ".png", ".jpg", ".jpeg" };
    }
}
