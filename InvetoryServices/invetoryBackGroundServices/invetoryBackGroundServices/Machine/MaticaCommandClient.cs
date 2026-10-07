using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using invetoryBackGroundServices.Options;
using MATICA_S3300e.LAN;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace invetoryBackGroundServices.Machine
{
    /// <summary>
    /// Matica-specific command construction and response interpretation, sitting on top of the
    /// vendor-neutral <see cref="IMachineTransport"/>. This is the layer that would be duplicated
    /// (not modified) to support a different device family later.
    /// </summary>
    /// <summary>Card-layout data for one embossed line, plus the printer's tipper settings for this job.</summary>
    /// <param name="Text">The line to emboss (e.g. cardholder name).</param>
    /// <param name="Font">Font index.</param>
    /// <param name="Cpi">Characters per inch.</param>
    /// <param name="OffsetX">Horizontal offset.</param>
    /// <param name="OffsetY">Vertical offset.</param>
    /// <param name="TipperTemperature">
    /// Printer-level tipper settings (Matica Print Flow, tipper-parameter phase) - sourced from
    /// the Inventory API's <c>MaticaPrinterConfiguration</c>, supplied by Angular in the print
    /// request the same way it already supplies <see cref="Font"/>/<see cref="Cpi"/>/
    /// <see cref="OffsetX"/>/<see cref="OffsetY"/>. Default 0 for a printer that has never had
    /// these configured, exactly matching every Emboss command sent before this phase existed.
    /// </param>
    public readonly record struct EmbossRequest(
        string Text, int Font, int Cpi, int OffsetX, int OffsetY,
        int TipperTemperature = 0, int TipperPressure = 0, int TipperConsumption = 0, int TipperTime = 0);

    public interface IMaticaCommandClient
    {
        /// <summary>
        /// Reads machine status, returning it parsed rather than as raw JSON text. Returns the
        /// full <see cref="MachineResponse"/> envelope - both <c>Machine_Configuration</c> and
        /// <c>Machine_Status</c> - since callers legitimately want either.
        /// <para>
        /// No <c>port</c> parameter, deliberately - unlike every other command, GetInfoJson always
        /// uses the fixed <see cref="MachineCommunicationOptions.InfoPort"/>, never a
        /// caller-supplied value. An earlier version of this method did take one; removing it from
        /// the signature entirely (rather than accepting and silently ignoring it) means a caller
        /// can't reasonably believe passing a different port here does anything.
        /// </para>
        /// </summary>
        Task<MachineResponse> GetInfoAsync(string ip, CancellationToken cancellationToken);

        /// <summary>Issues the Restore (reset) command.</summary>
        Task RestoreAsync(string ip, string port, CancellationToken cancellationToken);

        /// <summary>Ejects the currently loaded card to the given stacker/hopper.</summary>
        Task EjectCardAsync(string ip, string port, int hopperId, CancellationToken cancellationToken);

        /// <summary>Loads a card from the given feeder.</summary>
        Task LoadCardAsync(string ip, string port, int feederId, CancellationToken cancellationToken);

        /// <summary>Reads magnetic-stripe data from the given track and returns the raw track data.</summary>
        Task<string> ReadMagAsync(string ip, string port, string trackId, CancellationToken cancellationToken);

        /// <summary>Embosses one line per <paramref name="request"/>.</summary>
        Task EmbossAsync(string ip, string port, EmbossRequest request, CancellationToken cancellationToken);
    }

    /// <inheritdoc cref="IMaticaCommandClient" />
    public sealed class MaticaCommandClient : IMaticaCommandClient
    {
        private const string ActionPath = "action";

        private static readonly JsonSerializerSettings SerializerSettings = new()
        {
            ContractResolver = new DefaultContractResolver(),
            Formatting = Formatting.None
        };

        private readonly IMachineTransport _transport;
        private readonly MachineCommunicationOptions _options;

        /// <summary>Creates the command client over the given transport.</summary>
        public MaticaCommandClient(IMachineTransport transport, IOptions<MachineCommunicationOptions> options)
        {
            _transport = transport;
            _options = options.Value;
        }

        /// <inheritdoc />
        public async Task<MachineResponse> GetInfoAsync(string ip, CancellationToken cancellationToken)
        {
            // Fixed port (MachineCommunicationOptions.InfoPort), never the caller's action port -
            // see this method's own interface doc comment and InfoPort's doc comment for why.
            string body = await _transport.PostAsync(
                ip, _options.InfoPort, ActionPath, "{\"Command\":\"GetInfoJson\"}", cancellationToken);

            MachineResponse? response;
            try
            {
                // MachineResponse, not MachineInfoJSON: the real reply is an envelope
                // ({"Answer":..,"Machine_Configuration":{..},"Machine_Status":{..}}) and
                // MachineInfoJSON is only the inner Machine_Status object. Deserializing the
                // envelope straight into MachineInfoJSON would not throw - it would silently
                // yield an object with every property null.
                response = JsonConvert.DeserializeObject<MachineResponse>(body);
            }
            catch (JsonException ex)
            {
                throw new MachineProtocolException("Machine status response could not be parsed.", ex);
            }

            if (response is null)
            {
                throw new MachineProtocolException("Machine status response was empty.");
            }

            if (response.MachineStatus is null)
            {
                // Parsed, but without the section that carries the actual status - treat as a
                // protocol error rather than handing the caller a hollow object.
                throw new MachineProtocolException("Machine status response contained no status section.");
            }

            return response;
        }

        /// <inheritdoc />
        public async Task RestoreAsync(string ip, string port, CancellationToken cancellationToken)
        {
            string body = await _transport.PostAsync(
                ip, port, ActionPath, "{\"Command\":\"Restore\"}", cancellationToken);

            EnsureAccepted(body);
        }

        /// <inheritdoc />
        public async Task EjectCardAsync(
            string ip, string port, int hopperId, CancellationToken cancellationToken)
        {
            var command = new EjectCardClass("EjectCard", hopperId.ToString());
            string json = JsonConvert.SerializeObject(command, SerializerSettings);

            string body = await _transport.PostAsync(ip, port, ActionPath, json, cancellationToken);

            EnsureAccepted(body);
        }

        /// <inheritdoc />
        public async Task LoadCardAsync(
            string ip, string port, int feederId, CancellationToken cancellationToken)
        {
            var command = new LoadCardClass("LoadCard", feederId.ToString());
            string json = JsonConvert.SerializeObject(command, SerializerSettings);

            string body = await _transport.PostAsync(ip, port, ActionPath, json, cancellationToken);

            EnsureAccepted(body);
        }

        /// <inheritdoc />
        public async Task<string> ReadMagAsync(
            string ip, string port, string trackId, CancellationToken cancellationToken)
        {
            var command = new ReadMAGClass("ReadMAG", trackId);
            string json = JsonConvert.SerializeObject(command, SerializerSettings);

            string body = await _transport.PostAsync(ip, port, ActionPath, json, cancellationToken);

            // ParseData, not EnsureAccepted: ReadMAG's whole purpose is the track data carried in
            // AnswerClass.Data, which EnsureAccepted validates but does not return.
            return ParseData(body);
        }

        /// <inheritdoc />
        public async Task EmbossAsync(
            string ip, string port, EmbossRequest request, CancellationToken cancellationToken)
        {
            var lines = new List<EmbossLine>
            {
                new(request.Font.ToString(), request.Cpi.ToString(), request.OffsetX.ToString(),
                    request.OffsetY.ToString(), request.Text)
            };

            var command = new EmbossLineClass(
                "Emboss", lines, "Y",
                request.TipperTemperature.ToString(), request.TipperPressure.ToString(),
                request.TipperConsumption.ToString(), request.TipperTime.ToString());

            string json = JsonConvert.SerializeObject(command, SerializerSettings);
            string body = await _transport.PostAsync(ip, port, ActionPath, json, cancellationToken);

            EnsureAccepted(body);
        }

        /// <summary>
        /// Same envelope validation as <see cref="EnsureAccepted"/>, but returns
        /// <see cref="AnswerClass.Data"/> on success instead of discarding it - for commands like
        /// ReadMAG whose entire purpose is the data the machine returns alongside acceptance.
        /// </summary>
        private static string ParseData(string body)
        {
            AnswerClass? answer;
            try
            {
                answer = JsonConvert.DeserializeObject<AnswerClass>(body);
            }
            catch (JsonException ex)
            {
                throw new MachineProtocolException("Machine response could not be parsed.", ex);
            }

            if (answer is null)
            {
                throw new MachineProtocolException("Machine response was empty.");
            }

            if (string.Equals(answer.Answer, "KO", StringComparison.OrdinalIgnoreCase))
            {
                string detail = answer.Error is null
                    ? "no detail supplied"
                    : $"Group: {answer.Error.group}, ErrNumber: {answer.Error.code} - {answer.Error.message}";

                throw new MachineRejectedException(detail);
            }

            return answer.Data ?? string.Empty;
        }

        /// <summary>
        /// Interprets the machine's <c>AnswerClass</c> envelope, turning a reported machine-side
        /// error into a typed <see cref="MachineRejectedException"/> instead of the old code's
        /// pattern of returning -1 with the raw text as the message.
        /// <para>
        /// The failure signal is <c>Answer == "KO"</c>, exactly as the pre-existing
        /// <c>httpPOST</c> determines it - not merely the presence of error text.
        /// <c>AnswerClass</c>'s constructor always allocates a non-null <c>Error</c>, so testing
        /// for a non-null error would report success as failure. The composed message preserves
        /// the same group/code/message detail the old path put into <c>sMessage</c>, but it
        /// travels as the localization argument rather than as the user-facing message itself.
        /// </para>
        /// </summary>
        private static void EnsureAccepted(string body)
        {
            AnswerClass? answer;
            try
            {
                answer = JsonConvert.DeserializeObject<AnswerClass>(body);
            }
            catch (JsonException ex)
            {
                throw new MachineProtocolException("Machine response could not be parsed.", ex);
            }

            if (answer is null)
            {
                throw new MachineProtocolException("Machine response was empty.");
            }

            if (string.Equals(answer.Answer, "KO", StringComparison.OrdinalIgnoreCase))
            {
                string detail = answer.Error is null
                    ? "no detail supplied"
                    : $"Group: {answer.Error.group}, ErrNumber: {answer.Error.code} - {answer.Error.message}";

                throw new MachineRejectedException(detail);
            }
        }
    }
}
