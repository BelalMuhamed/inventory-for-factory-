using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.CardFiles;
using DomainLayer.Common;

namespace ApplicationLayer.ServicesContracts
{
    /// <summary>
    /// The card-file generation use case (Card File Generation, Phase 9.5): validate the target
    /// tenant → validate every card → serialize → fingerprint → encrypt. The system admin calls
    /// it; the resulting <c>.dat</c> is delivered out-of-band to the tenant, who feeds it back in
    /// through <see cref="IBatchUploadService"/>.
    /// <para>
    /// Read-only with respect to the database. No <c>Batch</c>, <c>ProductItem</c>, or
    /// <c>Stock</c> row is written here — those are the upload side's business. There is
    /// therefore no transaction and no unit-of-work commit in this pipeline.
    /// </para>
    /// </summary>
    public interface ICardFileGenerationService
    {
        /// <summary>
        /// Generates an encrypted card file for the tenant named in <paramref name="request"/>.
        /// </summary>
        /// <param name="request">Target tenant and the cards to include.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        /// <returns>
        /// The encrypted file plus the metadata the hand-off needs, or a failure. Unlike the
        /// upload pipeline, per-card problems are <em>not</em> a collected outcome: a single bad
        /// card fails the whole request with <c>CardFileErrors.CardsRejected</c> and nothing is
        /// generated. Upload collects-and-continues because it processes an artifact it did not
        /// create; generation creates one, so it can and should refuse to emit a broken file.
        /// </returns>
        Task<Result<CardFileGenerationResult>> GenerateAsync(
            CardFileGenerationRequest request,
            CancellationToken cancellationToken = default);
    }
}
