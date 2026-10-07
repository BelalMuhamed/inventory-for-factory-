using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using DomainLayer.Common;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Matica Print Flow, Super-Admin decryption phase: decrypts a Printer Agent log or outbox
    /// file that a Super Admin already has locally (retrieved from the branch machine by whatever
    /// means - out of scope for this endpoint) and uploads here. Nothing this endpoint touches is
    /// ever written to disk, a database, or any persistent store on this side - the uploaded bytes
    /// live only in the request's memory for the duration of the call, and the decrypted result is
    /// streamed straight back in the response body, never saved.
    /// <para>
    /// Deliberately upload-based rather than reaching out to the Printer Agent for the file: this
    /// service has no existing mechanism to access a remote Printer Agent's filesystem (confirmed
    /// by inspection before this endpoint was designed - see the accompanying design discussion),
    /// and building one would have meant either a new authenticated capability on the Printer
    /// Agent or a shared storage layer that doesn't currently exist. Upload avoids needing either.
    /// It also means there is no server-side file path to validate at all - the path-traversal
    /// concern that would normally apply to a "decrypt this file the server looks up" endpoint
    /// doesn't arise here, because there's no path parameter in the first place.
    /// </para>
    /// </summary>
    /// <response code="401">No valid bearer token was supplied.</response>
    /// <response code="403">A non-system-admin caller attempted this - system-admin only.</response>
    [ApiController]
    [Route("api/admin/file-decryption")]
    [Authorize(Policy = AuthorizationPolicies.SystemAdminOnly)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public sealed class AdminFileDecryptionController : ControllerBase
    {
        /// <summary>
        /// Upper bound on an uploaded file's size - a day's worth of log lines or a single Outbox
        /// entry are both comfortably under this; it exists to reject something clearly wrong
        /// early rather than to accommodate any real expected file.
        /// </summary>
        private const long MaxUploadBytes = 50 * 1024 * 1024;

        private readonly IPrintAgentFileDecryptionService _decryption;

        /// <summary>Creates the controller with the shared decryption service.</summary>
        public AdminFileDecryptionController(IPrintAgentFileDecryptionService decryption) => _decryption = decryption;

        /// <summary>
        /// Decrypts an uploaded Printer Agent log or outbox file and returns the clear content as
        /// a downloadable file. Auto-detects which of the two shapes was uploaded: an Outbox file
        /// is a single encrypted blob covering the whole file (Matica Print Flow,
        /// file-encryption phase - <c>FileOutboxStore</c> encrypts the whole JSON payload per
        /// entry), while a log file is many independently-encrypted lines, each with a plaintext
        /// timestamp/level prefix ahead of the cipher (<c>Logger.AppendLog</c>'s own format).
        /// </summary>
        /// <param name="file">The encrypted file, as multipart form data.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">
        /// Decrypted content, returned as a file download. For a log file, lines that could not be
        /// decrypted (an old TripleDES-encrypted line from before this phase, or a genuinely
        /// tampered/corrupted line) are preserved with an inline marker rather than silently
        /// dropped or failing the whole file - see <see cref="DecryptLogLines"/>'s own doc comment.
        /// </response>
        /// <response code="422">No file was supplied, or the file was empty or exceeded the size limit.</response>
        /// <response code="409">
        /// The upload was recognized as a single-blob (Outbox-style) encrypted file, but
        /// decryption failed - either it was never produced by the Printer Agent's encryption
        /// service (wrong format entirely) or it fails the AES-GCM authentication tag check
        /// (tampered or corrupted). Unlike a log file, there is nothing partial to return here, so
        /// this is a hard failure rather than an inline marker.
        /// </response>
        [HttpPost("decrypt")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaxUploadBytes)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Decrypt(IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return Result.Failure(
                    Error.Validation("FileDecryption.NoFileSupplied", "No file was supplied, or the file was empty."))
                    .ToActionResult(this);
            }

            if (file.Length > MaxUploadBytes)
            {
                return Result.Failure(
                    Error.Validation("FileDecryption.FileTooLarge", "The uploaded file exceeds the maximum allowed size."))
                    .ToActionResult(this);
            }

            string content;
            // The uploaded stream is a request-scoped buffer ASP.NET Core manages and disposes
            // itself once the request completes - nothing here opens a file on this server's own
            // disk, and nothing written here persists past the response being sent.
            using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
            {
                content = await reader.ReadToEndAsync(cancellationToken);
            }

            string trimmed = content.Trim();

            if (_decryption.LooksEncrypted(trimmed))
            {
                // Outbox-style: the whole file is one encrypted blob. Nothing partial to salvage
                // if this fails, unlike the per-line log case below - a hard failure.
                string decryptedJson;
                try
                {
                    decryptedJson = _decryption.Decrypt(trimmed);
                }
                catch (Exception ex) when (ex is PrintAgentFileFormatException or CryptographicException)
                {
                    // Neither the format nor the cryptographic failure reason is echoed back - see
                    // this controller's own scope note on not leaking implementation details.
                    return Result.Failure(
                        Error.Conflict("FileDecryption.Failed",
                            "The uploaded file could not be decrypted. It may not have been produced by the " +
                            "Printer Agent's encryption service, or it may have been altered."))
                        .ToActionResult(this);
                }

                byte[] jsonBytes = Encoding.UTF8.GetBytes(decryptedJson);
                return File(jsonBytes, "application/json", BuildDownloadName(file.FileName, ".json"));
            }

            // Log-style: many independently-encrypted lines, each with a plaintext prefix.
            string decryptedLog = DecryptLogLines(content);
            byte[] logBytes = Encoding.UTF8.GetBytes(decryptedLog);
            return File(logBytes, "text/plain", BuildDownloadName(file.FileName, ".log"));
        }

        /// <summary>
        /// Decrypts each line of a Printer Agent log file independently, matching
        /// <c>Logger.AppendLog</c>'s exact format: <c>{timestamp} | [{type}] &gt;&gt; {cipher}</c>
        /// - the prefix before the last <c>" &gt;&gt; "</c> is already plaintext; only the trailing
        /// segment is encrypted.
        /// <para>
        /// A line that doesn't decrypt is not treated as a whole-file failure, unlike the Outbox
        /// case - a single bad or old-format line shouldn't hide every other line's content from
        /// a Super Admin trying to actually read a log. Three cases, each marked distinctly so the
        /// reader knows which happened: no recognizable <c>" >> "</c> delimiter at all (passed
        /// through unchanged - most likely a line that predates this format entirely); a trailing
        /// segment that doesn't look like this service's encrypted format (most likely an old
        /// TripleDES-encrypted line from before the encryption phase - passed through unchanged,
        /// since this service has no way to decrypt that format and pretending otherwise would be
        /// worse than saying so); and a segment that looks right but fails the authentication tag
        /// check (a genuinely tampered or corrupted line - marked explicitly, not silently
        /// dropped, since that failure is itself diagnostically meaningful).
        /// </para>
        /// </summary>
        private string DecryptLogLines(string content)
        {
            var result = new StringBuilder();
            const string delimiter = " >> ";

            foreach (string rawLine in content.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                int delimiterIndex = line.IndexOf(delimiter, StringComparison.Ordinal);

                if (delimiterIndex < 0)
                {
                    result.AppendLine(line);
                    continue;
                }

                string prefix = line[..(delimiterIndex + delimiter.Length)];
                string cipher = line[(delimiterIndex + delimiter.Length)..];

                if (!_decryption.LooksEncrypted(cipher))
                {
                    // Old TripleDES-encrypted line (from before this phase) or already-plaintext -
                    // this service cannot decrypt that format with this key. Preserved as-is
                    // rather than guessed at.
                    result.AppendLine(line);
                    continue;
                }

                try
                {
                    result.Append(prefix).AppendLine(_decryption.Decrypt(cipher));
                }
                catch (Exception ex) when (ex is PrintAgentFileFormatException or CryptographicException)
                {
                    result.Append(prefix).AppendLine("[LINE COULD NOT BE DECRYPTED - possibly corrupted or tampered]");
                }
            }

            return result.ToString();
        }

        /// <summary>Builds a download file name from the original upload name, defaulting if none was supplied.</summary>
        private static string BuildDownloadName(string? originalFileName, string extension)
        {
            string baseName = string.IsNullOrWhiteSpace(originalFileName)
                ? "decrypted"
                : Path.GetFileNameWithoutExtension(originalFileName);

            return $"{baseName}-decrypted{extension}";
        }
    }
}
