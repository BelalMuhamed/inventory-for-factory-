using Evolis;
using invetoryBackGroundServices.Common;
using invetoryBackGroundServices.Machine.Abstractions;

namespace invetoryBackGroundServices.Machine.Evolis
{
    /// <summary>
    /// Translates <c>Evolis.ReturnCode</c> and <c>Evolis.Status</c> into this service's
    /// vendor-neutral shapes (Evolis Integration Plan §13.2/§13.3) — the only place either
    /// `Evolis.*` type is read for this purpose. Built from the real enum values confirmed by
    /// reflecting over the installed <c>Evolis.SDK</c> 9.4.1 assembly, not the plan's draft list
    /// (the SDK has far more members than the plan enumerated — e.g. an entire <c>HTTP_*</c>/
    /// <c>SVC_*</c>/<c>SANDBOX_*</c>/<c>SMART_*</c>/<c>CARD_*</c>/<c>LAM_*</c> family the plan
    /// never mentions). Only the codes the plan's own §13.2 table explicitly names are given a
    /// specific category below; every other real value — including all of the families just
    /// listed — deliberately falls through to <see cref="EvolisErrors.Internal"/> via the
    /// exhaustive switch's discard pattern, per plan rule §13.4 #7 ("an unmapped SDK code
    /// degrades to Internal + log, it never throws an unhandled exception").
    /// </summary>
    public static class EvolisStatusMapper
    {
        /// <summary>Maps one <see cref="ReturnCode"/> to the vendor-neutral error this service reports.</summary>
        public static MachineError FromReturnCode(ReturnCode code) => code switch
        {
            ReturnCode.ETIMEOUT or ReturnCode.SESSION_ETIMEOUT
                => EvolisErrors.Timeout(code.ToString()),

            ReturnCode.SESSION_EBUSY or ReturnCode.PRINTER_EBUSY
                => EvolisErrors.Busy(code.ToString()),

            ReturnCode.PRINTER_ENOCOM or ReturnCode.PRINTER_NETWORK_ERROR
                => EvolisErrors.CommunicationFailure(code.ToString()),

            ReturnCode.PRINTER_NETWORK_ECERT
                => EvolisErrors.CertificateError(code.ToString()),

            ReturnCode.ENEEDACTION or ReturnCode.PRINT_NEEDACTION
                => EvolisErrors.NeedsAction(code.ToString()),

            ReturnCode.PRINT_EMECHANICAL
                => EvolisErrors.MechanicalFailure(code.ToString()),

            ReturnCode.PRINT_EUNKNOWNRIBBON
                => EvolisErrors.UnknownRibbon(code.ToString()),

            ReturnCode.MAG_EBLANK
                => EvolisErrors.BlankTrack(code.ToString()),

            ReturnCode.MAG_EDATA or ReturnCode.MAG_ERROR
                => EvolisErrors.MagneticError(code.ToString()),

            ReturnCode.USER_EINVALID or ReturnCode.USER_ETIMEOUT or ReturnCode.USER_EUNAUTHORIZED
                or ReturnCode.USER_EUNIDENTIFIED or ReturnCode.USER_ERROR
                or ReturnCode.SYSTEM_EUNAUTHORIZED_PRINTER
                => EvolisErrors.Forbidden(code.ToString()),

            ReturnCode.ECANCELLED
                => EvolisErrors.Cancelled(),

            // PRINT_ENOIMAGE / PRINT_EDATA / PRINT_WSETTING (a bug in what we sent),
            // EPARAMS / EUNSUPPORTED / EDISABLED / SESSION_DISABLED (a bug or misconfiguration),
            // and every other real ReturnCode this switch doesn't name above — see the class
            // doc comment for why those all collapse here rather than being individually guessed.
            _ => EvolisErrors.Internal(code.ToString())
        };

        /// <summary>
        /// Derives a coarse <see cref="PrinterState"/> from a successfully-read <see cref="Status"/>.
        /// Checked via <see cref="Status.IsOn(Status.Flag)"/> — the current (non-deprecated) API;
        /// the four split flag enums (<c>CfgFlag</c>/<c>InfFlag</c>/<c>WarFlag</c>/<c>ErrFlag</c>)
        /// this plan's background research found are obsolete in the installed SDK version,
        /// superseded by one unified <see cref="Status.Flag"/>.
        /// </summary>
        public static PrinterState MapState(Status status)
        {
            if (status.IsOn(Status.Flag.ERR_MECHANICAL) || status.error != 0)
            {
                return PrinterState.Error;
            }

            if (status.warning != 0)
            {
                return PrinterState.Warning;
            }

            return PrinterState.Ready;
        }
    }
}
