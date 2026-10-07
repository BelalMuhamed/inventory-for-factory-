namespace DomainLayer.Enums
{
    /// <summary>
    /// SDK <c>Connection.SetErrorManagement</c> value (Evolis Integration Plan Q-29). Persisted
    /// as <c>tinyint</c> via <c>HasConversion&lt;byte&gt;()</c>.
    /// <para>
    /// Default is <see cref="SOFTWARE"/>. Phase 0 hardware testing found this is only honoured
    /// when the connection was opened against the correctly <c>isSupervised == true</c> device
    /// entry — against an unsupervised entry, <c>SetErrorManagement(SOFTWARE)</c> silently had no
    /// effect and <c>GetErrorManagement</c> reported <c>UNKNOWN</c> regardless of what was
    /// requested. See plan §17, R-1 resolution.
    /// </para>
    /// </summary>
    public enum EvolisErrorManagement : byte
    {
        /// <summary>Let the printer's own firmware handle error conditions.</summary>
        PRINTER = 0,

        /// <summary>Let this application/EPS handle error conditions in software. Default — see remarks.</summary>
        SOFTWARE = 1
    }
}
