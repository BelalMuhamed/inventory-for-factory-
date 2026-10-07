namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Computes the deterministic identity/dedup fingerprint for a card PAN (PAN Storage
    /// Redesign). Keeps the concrete algorithm (HMAC-SHA256, per-tenant derived key) in the
    /// infrastructure layer so the application layer depends only on this contract.
    /// </summary>
    public interface IPanFingerprintGenerator
    {
        /// <summary>
        /// Computes the deterministic HMAC-SHA256 fingerprint of <paramref name="normalizedPan"/>
        /// for <paramref name="tenantId"/>. The same tenant and the same PAN always yield the
        /// same 32 bytes; the same PAN under a different tenant yields different bytes. Never
        /// throws on well-formed input.
        /// </summary>
        /// <param name="tenantId">Owning tenant id — selects the derived HMAC key.</param>
        /// <param name="normalizedPan">
        /// The full PAN, already normalized via <c>BatchFileFormat.NormalizePan</c>. This method
        /// does not normalize its input — callers must normalize first so the same physical card
        /// always fingerprints identically regardless of incidental formatting.
        /// </param>
        /// <returns>A 32-byte HMAC-SHA256 fingerprint. Never displayed, logged, or returned by any API.</returns>
        byte[] Fingerprint(long tenantId, string normalizedPan);
    }
}
