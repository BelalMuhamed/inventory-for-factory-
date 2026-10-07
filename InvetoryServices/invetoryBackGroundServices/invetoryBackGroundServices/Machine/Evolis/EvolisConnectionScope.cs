using Evolis;

namespace invetoryBackGroundServices.Machine.Evolis
{
    /// <summary>
    /// Owns one <see cref="Connection"/> for the duration of a single operation (Evolis
    /// Integration Plan §8: open-per-operation, never a long-lived pool — see
    /// <see cref="EvolisConnectionFactory"/>'s doc comment for why). Disposing releases the
    /// reservation (if one was taken) before closing, unconditionally — plan rule §13.4 #5.
    /// </summary>
    public sealed class EvolisConnectionScope : System.IDisposable
    {
        private readonly bool _reserved;

        /// <summary>The underlying SDK connection, opened and configured by <see cref="EvolisConnectionFactory"/>.</summary>
        public Connection Connection { get; }

        /// <summary>True when <c>Reserve()</c> was requested and actually succeeded (best-effort — see <see cref="EvolisConnectionFactory"/>'s remarks).</summary>
        public bool IsReserved => _reserved;

        internal EvolisConnectionScope(Connection connection, bool reserved)
        {
            Connection = connection;
            _reserved = reserved;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_reserved)
            {
                Connection.Release();
            }

            Connection.Close();
        }
    }
}
