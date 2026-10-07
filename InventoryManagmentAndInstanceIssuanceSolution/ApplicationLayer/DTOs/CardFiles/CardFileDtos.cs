using System.Collections.Generic;
using ApplicationLayer.CardFiles;

namespace ApplicationLayer.DTOs.CardFiles
{
    /// <summary>
    /// A single card to place in the generated file.
    /// </summary>
    /// <param name="ClearPan">
    /// Full PAN in the clear. This is the only place in the platform where a complete PAN crosses
    /// the API boundary — it is never logged, never persisted, and never echoed back in any
    /// response, masked or otherwise.
    /// </param>
    /// <param name="ProductName">Product name, matched case-insensitively against the target tenant's catalog.</param>
    /// <param name="BranchName">Branch name, matched case-insensitively against the target tenant's branches.</param>
    public sealed record CardFileEntry(string ClearPan, string ProductName, string BranchName);

    /// <summary>
    /// Request payload for <c>POST /api/card-files</c> (Card File Generation, Phase 9). Issued by
    /// the system admin on behalf of a tenant, which is why the tenant is a parameter here rather
    /// than resolved from the caller's token as it is everywhere else.
    /// </summary>
    /// <param name="TenantId">Tenant the file is being generated for; selects the encryption key.</param>
    /// <param name="Cards">Cards to include. All-or-nothing: one rejected card fails the request.</param>
    public sealed record CardFileGenerationRequest(long TenantId, IReadOnlyList<CardFileEntry> Cards);

    /// <summary>
    /// A card that failed validation, used to build the 422 detail map and for logging. Carries
    /// only the masked PAN.
    /// </summary>
    /// <param name="Index">Zero-based position in the request's <c>cards</c> array.</param>
    /// <param name="MaskedPan">Masked PAN, via <c>PanMasker</c>.</param>
    /// <param name="Reason">Why the card was rejected.</param>
    public sealed record RejectedCardEntry(int Index, string MaskedPan, CardRejectionReason Reason);

    /// <summary>
    /// Result of a successful generation (Card File Generation, Phase 9). Nothing is persisted to
    /// produce this — no <c>Batch</c>, no <c>ProductItem</c>, no <c>Stock</c>. Those are written
    /// by the tenant when they upload the file.
    /// </summary>
    /// <param name="FileName">
    /// Suggested file name, always ending in <c>.dat</c> so it passes the upload endpoint's
    /// extension guard unchanged.
    /// </param>
    /// <param name="FileMac">
    /// Uppercase SHA-256 hex of the plaintext — the same value the tenant's upload will compute
    /// after decrypting. Hand it over out-of-band so delivery can be verified, and so support can
    /// correlate a tenant's duplicate-file 409 back to a specific generated file.
    /// </param>
    /// <param name="CardCount">Cards written to the file.</param>
    /// <param name="ExpectedRowCount">
    /// Row count the tenant must declare on upload. Equal to <paramref name="CardCount"/> by
    /// construction; returned separately because supplying it wrongly is the single most likely
    /// operator error in the whole hand-off.
    /// </param>
    /// <param name="FileSizeBytes">Size of the encrypted file in bytes.</param>
    /// <param name="FileContent">
    /// The encrypted <c>.dat</c> file's raw bytes. Streamed back to the caller as the HTTP
    /// response body (<c>application/octet-stream</c>) rather than embedded in a JSON payload, so
    /// this carries the plain byte array — no base64 encoding.
    /// </param>
    public sealed record CardFileGenerationResult(
        string FileName,
        string FileMac,
        int    CardCount,
        int    ExpectedRowCount,
        long   FileSizeBytes,
        byte[] FileContent);
}
