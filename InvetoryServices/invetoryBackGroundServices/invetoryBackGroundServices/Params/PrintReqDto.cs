namespace invetoryBackGroundServices.Params
{
    /// <summary>
    /// Matica Print Flow: redesigned from the original shape. <c>token</c> is gone - the caller's
    /// credential is now the Print Agent token in the standard <c>Authorization</c> header,
    /// validated by <c>[Authorize]</c> before this ever reaches the controller. <c>productName</c>/
    /// <c>branchName</c> are replaced with <see cref="ProductId"/>/<see cref="BranchId"/> (longs) -
    /// Angular already knows these as ids from its own product/branch browsing elsewhere, and the
    /// Inventory API's own name filters are substring matches, not exact, so ids avoid an
    /// ambiguity this flow doesn't need to accept. <c>printedFace</c> is gone entirely - Matica
    /// print configuration has no per-face concept (confirmed against the Inventory API's own
    /// domain model: only the Evolis print-config DTO has a PrintedFace field), so it was never
    /// meaningful here and introducing it now would only manufacture a parity this device doesn't
    /// have. Print-layout fields (<see cref="Font"/>/<see cref="Cpi"/>/<see cref="OffsetX"/>/
    /// <see cref="OffsetY"/>) are supplied directly instead of being looked up by this service -
    /// Angular already fetches them from the Inventory API's own
    /// <c>GET /api/products/{id}/print-config</c> as part of normal product browsing.
    /// <c>UserName</c> is gone too, for the same reason <c>token</c> is: the identifier now
    /// recorded for the local batch line is the caller's tenant id, and that's already an
    /// authenticated claim on the Print Agent token itself (<c>PrintAgentClaims.TenantId</c>) -
    /// no reason to also accept it as a caller-supplied request field that could disagree with
    /// what the token actually says, the same reasoning already applied to <see cref="BranchId"/>
    /// via <c>IsOutsideTokenScope</c>.
    /// </summary>
    public class PrintReqDto
    {
        public required string CardHolderName { get; set; }
        public required string MachineIp { get; set; }
        public required string Port { get; set; }
        public required int FeederId { get; set; }
        public required int HopperId { get; set; }
        public required long ProductId { get; set; }
        public required long BranchId { get; set; }
        public required int Font { get; set; }
        public required int Cpi { get; set; }
        public required int OffsetX { get; set; }
        public required int OffsetY { get; set; }

        /// <summary>
        /// Printer-level tipper settings (Matica Print Flow, tipper-parameter phase), sourced from
        /// the Inventory API's <c>MaticaPrinterConfiguration</c> - Angular fetches them the same
        /// way it already fetches <see cref="Font"/>/<see cref="Cpi"/>/<see cref="OffsetX"/>/
        /// <see cref="OffsetY"/> and passes them through here. Optional, defaulting to 0: a
        /// printer that has never had these configured behaves exactly as it did before this
        /// phase existed (every Emboss command sent 0 for all four, unconditionally).
        /// </summary>
        public int TipperTemperature { get; set; }
        public int TipperPressure { get; set; }
        public int TipperConsumption { get; set; }
        public int TipperTime { get; set; }
    }
}
