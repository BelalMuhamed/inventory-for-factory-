using System.Threading;
using System.Threading.Tasks;
using invetoryBackGroundServices.Machine.Abstractions;

namespace invetoryBackGroundServices.Machine.Orchestration
{
    /// <summary>
    /// Runs one Evolis print job end to end (Evolis Integration Plan §10, the twelve steps) — the
    /// single entry point <c>MachineController.PrintEvolis</c> calls.
    /// </summary>
    public interface IEvolisCardJobOrchestrator
    {
        /// <param name="job">Everything about the card and the product/branch it belongs to.</param>
        /// <param name="tenantId">The authenticated Print Agent token's tenant id — for the local batch line (step 9), same as Matica's.</param>
        /// <param name="bearerToken">The Print Agent token to forward to the Inventory API's print-flow endpoints.</param>
        /// <param name="cancellationToken">Observed throughout; an abandoned request releases the printer gate naturally.</param>
        Task<CardJobResult> RunAsync(CardJob job, string tenantId, string bearerToken, CancellationToken cancellationToken);
    }
}
