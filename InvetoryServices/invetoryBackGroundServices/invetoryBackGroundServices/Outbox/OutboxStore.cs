using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using invetoryBackGroundServices.Options;
using invetoryBackGroundServices.Security;
using Microsoft.Extensions.Options;

namespace invetoryBackGroundServices.Outbox
{
    /// <summary>
    /// One pending print-result confirmation that could not be delivered to the Inventory API
    /// after Backend Call #2's in-request retries were exhausted (the "printed but could not
    /// confirm" / HTTP 207 scenario). Persisted so the outcome survives a Printer Agent crash or
    /// restart between the original attempt and eventual reconciliation.
    /// <para>
    /// Matica Print Flow, reconciliation-credential phase: this no longer carries a bearer token
    /// at all. Reconciliation now authenticates with its own freshly-minted service token (see
    /// <c>IReconciliationTokenProvider</c>), fetched once per job run rather than reused from the
    /// original request - so there's no reason to persist the original print attempt's token here,
    /// and removing it closes a real exposure: every pending entry used to be a plaintext file on
    /// disk carrying a live bearer token for however long it stayed queued.
    /// </para>
    /// </summary>
    public sealed class OutboxEntry
    {
        public string IdempotencyKey { get; set; } = string.Empty;
        public long ProductItemId { get; set; }
        public long BranchId { get; set; }
        public bool Success { get; set; }
        public string? HolderName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public int AttemptCount { get; set; }
        public DateTime? LastAttemptUtc { get; set; }
        public string? LastFailureReason { get; set; }
    }

    /// <summary>Persists and retrieves pending <see cref="OutboxEntry"/> records.</summary>
    public interface IOutboxStore
    {
        /// <summary>Persists a new entry, keyed by its idempotency key.</summary>
        Task SaveAsync(OutboxEntry entry);

        /// <summary>Returns every currently pending entry.</summary>
        Task<IReadOnlyList<OutboxEntry>> GetAllAsync();

        /// <summary>Overwrites an existing entry (e.g. after a failed retry, to record the attempt).</summary>
        Task UpdateAsync(OutboxEntry entry);

        /// <summary>Removes an entry once it has been successfully reconciled.</summary>
        Task DeleteAsync(string idempotencyKey);
    }

    /// <summary>
    /// One JSON file per pending entry under a configured directory - deliberately the simplest
    /// mechanism that satisfies "survives a process restart," not a database or queue. The
    /// idempotency key is already unique per physical print attempt, so it doubles as the
    /// filename; no separate id generation needed.
    /// <para>
    /// Matica Print Flow, file-encryption phase: every file this store writes from now on is
    /// AES-GCM encrypted (whole-file, not per-line like <c>Logger</c> - an Outbox entry is written
    /// once, read once, then deleted, so there's no append-in-place concern the way there is for
    /// a log file). <see cref="GetAllAsync"/> handles both formats on read: any file already
    /// pending before this phase existed is plaintext JSON (confirmed - this store has never
    /// encrypted anything before now), and forcing a one-time migration of those files wasn't
    /// justified when simply recognizing both formats on read is smaller, safer, and achieves the
    /// same end state organically - any entry that gets rewritten via <see cref="UpdateAsync"/>
    /// (e.g. after a failed reconciliation attempt) is written back out encrypted from that point
    /// on, with no explicit backfill step required.
    /// </para>
    /// </summary>
    public sealed class FileOutboxStore : IOutboxStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private readonly string _directory;
        private readonly IFileEncryptionService _encryption;

        /// <summary>Creates the store, ensuring its backing directory exists.</summary>
        public FileOutboxStore(IOptions<OutboxOptions> options, IFileEncryptionService encryption)
        {
            string configured = options.Value.Directory;
            _directory = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(AppContext.BaseDirectory, "Outbox")
                : configured;
            _encryption = encryption;
            Directory.CreateDirectory(_directory);
        }

        /// <inheritdoc />
        public async Task SaveAsync(OutboxEntry entry)
        {
            string json = JsonSerializer.Serialize(entry, JsonOptions);
            string encrypted = _encryption.Encrypt(json);
            await File.WriteAllTextAsync(PathFor(entry.IdempotencyKey), encrypted);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<OutboxEntry>> GetAllAsync()
        {
            var entries = new List<OutboxEntry>();
            foreach (string file in Directory.EnumerateFiles(_directory, "*.json"))
            {
                try
                {
                    string content = File.ReadAllText(file);

                    // Backward compatibility: any file already pending before this phase existed
                    // is plaintext JSON (this store never encrypted anything before now).
                    // LooksEncrypted is a cheap, non-throwing shape check - it decides which path
                    // to take, not whether the content is actually valid once decrypted/parsed.
                    string json = _encryption.LooksEncrypted(content)
                        ? _encryption.Decrypt(content)
                        : content;

                    OutboxEntry? entry = JsonSerializer.Deserialize<OutboxEntry>(json);
                    if (entry is not null)
                    {
                        entries.Add(entry);
                    }
                }
                catch (Exception ex) when (
                    ex is JsonException or FileEncryptionFormatException or CryptographicException)
                {
                    // A corrupt, partially-written, or tampered file shouldn't take down the whole
                    // sweep - it's left in place for manual inspection rather than silently
                    // deleted. FileEncryptionFormatException and CryptographicException are kept
                    // as distinct exception types by the encryption service on purpose (wrong
                    // shape vs. right shape but failed the authentication tag check), but both get
                    // the same treatment here: skip this one file, keep going.
                }
            }

            return Task.FromResult<IReadOnlyList<OutboxEntry>>(entries);
        }

        /// <inheritdoc />
        public Task UpdateAsync(OutboxEntry entry) => SaveAsync(entry);

        /// <inheritdoc />
        public Task DeleteAsync(string idempotencyKey)
        {
            string path = PathFor(idempotencyKey);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return Task.CompletedTask;
        }

        private string PathFor(string idempotencyKey) => Path.Combine(_directory, $"{idempotencyKey}.json");
    }
}
