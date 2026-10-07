namespace ApplicationLayer.Options
{
    /// <summary>
    /// Settings for the plain-text error/exception log files, bound from the <c>"LogFile"</c>
    /// section.
    /// <para>
    /// <b>Revision:</b> replaces <c>LogEncryptionOptions</c> — these files are no longer
    /// encrypted (explicit decision: readability was worth more here than at-rest protection for
    /// this content), so there is no password/salt to supply any more, and no fail-fast startup
    /// check for one.
    /// </para>
    /// </summary>
    public sealed class LogFileOptions
    {
        /// <summary>Configuration section name.</summary>
        public const string SectionName = "LogFile";

        /// <summary>Directory for the log files. Defaults to <c>logs</c> under the content root.</summary>
        public string Directory { get; set; } = "logs";

        /// <summary>Error-log file name (Warning+ without an exception).</summary>
        public string ErrorFileName { get; set; } = "errors.log";

        /// <summary>Exception-log file name (entries carrying an exception).</summary>
        public string ExceptionFileName { get; set; } = "exceptions.log";
    }
}
