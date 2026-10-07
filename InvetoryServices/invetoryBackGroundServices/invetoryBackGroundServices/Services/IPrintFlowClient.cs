using System.Threading;
using System.Threading.Tasks;
using invetoryBackGroundServices.PrintFlow;

namespace invetoryBackGroundServices.Services
{
    /// <summary>
    /// The two Matica Print Flow backend calls this service makes to the Inventory API — resolving
    /// a card for print (right after <c>ReadMAG</c>) and recording the physical outcome (right
    /// after <c>EjectCard</c>). Both are called with the short-lived Print Agent token Angular
    /// handed this service, forwarded through unchanged — this service never sees, and never needs,
    /// the caller's real Inventory API session token.
    /// </summary>
    public interface IPrintFlowClient
    {
        /// <summary>Backend Call #1. See <see cref="RecordPrintResultAsync"/> for Backend Call #2.</summary>
        /// <param name="bearerToken">The Print Agent token to present as this call's own credential.</param>
        /// <param name="pan">Raw full PAN as read off the magnetic stripe.</param>
        /// <param name="productId">The product this card is expected to be.</param>
        /// <param name="branchId">The branch this card is being printed at.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<PrintFlowResult<ResolveForPrintResponseDto>> ResolveForPrintAsync(
            string bearerToken, string pan, long productId, long branchId, CancellationToken cancellationToken);

        /// <summary>
        /// Backend Call #2. Retries a bounded number of times on a transient failure (network
        /// error, timeout, or 5xx) reusing the same <paramref name="idempotencyKey"/> on every
        /// attempt; does not retry a well-formed business failure (4xx), since resending the same
        /// request won't change that answer.
        /// </summary>
        Task<PrintFlowResult<object?>> RecordPrintResultAsync(
            string bearerToken, long productItemId, long branchId, bool success, string? holderName,
            string idempotencyKey, CancellationToken cancellationToken);
    }
}
