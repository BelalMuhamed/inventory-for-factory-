namespace DomainLayer.Enums
{
    /// <summary>
    /// SDK <c>Connection</c> open mode (Evolis Integration Plan Q-29). Persisted as
    /// <c>tinyint</c> via <c>HasConversion&lt;byte&gt;()</c>.
    /// <para>
    /// Default is <see cref="SUPERVISED"/>, not <see cref="DIRECT"/> — confirmed by Phase 0
    /// hardware testing against a live EPS-supervised deployment (see plan §17, R-1/R-2
    /// resolutions): <c>DIRECT</c> risks a native fast-fail when Evolis Premium Suite is
    /// actively supervising the printer, while <c>SUPERVISED</c>, combined with selecting the
    /// device entry where <c>Device.isSupervised == true</c>, opened cleanly and every
    /// subsequent operation (<c>Reserve</c>, <c>SetErrorManagement</c>, mag read, print) behaved
    /// correctly.
    /// </para>
    /// </summary>
    public enum EvolisOpenMode : byte
    {
        /// <summary>Let the SDK choose based on what it detects on the machine.</summary>
        AUTO = 0,

        /// <summary>Open directly, bypassing EPS supervision. Confirmed unsafe on a supervised machine — see remarks.</summary>
        DIRECT = 1,

        /// <summary>Open through EPS supervision. Confirmed working default — see remarks.</summary>
        SUPERVISED = 2
    }
}
