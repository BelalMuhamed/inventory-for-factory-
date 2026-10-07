using MATICA_S3300e.CLS;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MATICA_S3300e.LAN
{
    // =============================== Card Data
    public class MachineResponse
    {
        [JsonProperty("Answer")]
        public string Answer { get; set; }

        [JsonProperty("Machine_Configuration")]
        public MachineConfiguration MachineConfiguration { get; set; }

        [JsonProperty("Machine_Status")]
        public MachineInfoJSON MachineStatus { get; set; }
    }
    public class MachineConfiguration
    {
        [JsonProperty("machine_model")]
        public string MachineModel { get; set; }

        [JsonProperty("machine_name")]
        public string MachineName { get; set; }

        [JsonProperty("machine_sn")]
        public string MachineSN { get; set; }

        [JsonProperty("number_of_feeders")]
        public string NumberOfFeeders { get; set; }

        [JsonProperty("card_exit")]
        public string CardExit { get; set; }

        [JsonProperty("card_reject")]
        public string CardReject { get; set; }

        [JsonProperty("card_counter")]
        public string CardCounter { get; set; }
    }
    public class MachineInfoJSON
    {
        [JsonProperty("machine_status")]
        public string machineStatus { get; set; }

        [JsonProperty("card_inside")]
        public string CardInside { get; set; }

        [JsonProperty("cover_open")]
        public string CoverOpen { get; set; }

        [JsonProperty("error_message")]
        public string ErrorMessage { get; set; }

        [JsonProperty("feeder_0_card_presence")]
        public string Feeder0CardPresence { get; set; }

        [JsonProperty("feeder_1_card_presence")]
        public string Feeder1CardPresence { get; set; }

        [JsonProperty("tipper_status")]
        public string TipperStatus { get; set; }

        [JsonProperty("tipper_temperature")]
        public string TipperTemperature { get; set; }

        [JsonProperty("tipper_near_end")]
        public string TipperNearEnd { get; set; }

        [JsonProperty("tipper_end_ribbon")]
        public string TipperEndRibbon { get; set; }

        [JsonProperty("rear_infiller_near_end")]
        public string RearInfillerNearEnd { get; set; }

        [JsonProperty("rear_infiller_end_ribbon")]
        public string RearInfillerEndRibbon { get; set; }

        [JsonProperty("top_infiller_near_end")]
        public string TopInfillerNearEnd { get; set; }

        [JsonProperty("top_infiller_end_ribbon")]
        public string TopInfillerEndRibbon { get; set; }
    }
    /// <summary>
    /// Deserialization target for the machine's <c>GetInfoJson</c> response (Matica Print Flow,
    /// status-parsing fix). <c>machine_status</c>, <c>card_inside</c>, and <c>tipper_status</c> are
    /// <b>confirmed</b> wire field names — they're the exact strings the pre-fix code checked with
    /// <c>string.Contains(...)</c>. Every other <c>[JsonProperty]</c> name below is a best-guess
    /// snake_case conversion following that same pattern, <b>not independently confirmed against a
    /// real device response</b> — verify these against an actual capture before relying on them for
    /// anything beyond the three confirmed fields (see the Matica patch notes).
    /// </summary>
   
    // =============================== Command Classes
    public class LoadCardClass
    {
        #region Declarations
        private string _Command;
        private string _feeder_id;
        #endregion

        #region Properties
        public string Command
        {
            get { return _Command; }
            set { _Command = value; }
        }

        public string Feeder_ID
        {
            get { return _feeder_id; }
            set { _feeder_id = value; }
        }
        #endregion

        #region Constructors
        public LoadCardClass(string command, string feeder_id)
        {
            _Command = command;
            _feeder_id = feeder_id;
        }
        #endregion
    }
    public class EjectCardClass
    {
        #region Declarations
        private string _command;
        private string _reject_id;
        #endregion

        #region Properties
        public string Command
        {
            get { return _command; }
            set { _command = value; }
        }

        public string Reject_ID
        {
            get { return _reject_id; }
            set { _reject_id = value; }
        }
        #endregion

        #region Constructors
        public EjectCardClass(string command, string reject_id)
        {
            _command = command;
            _reject_id = reject_id;
        }
        #endregion
    }

    public class ReadMAGClass
    {
        #region Declarations
        private string _command;
        private string _track_id;
        #endregion

        #region Properties
        public string Command
        {
            get { return _command; }
            set { _command = value; }
        }
        public string Track_ID
        {
            get { return _track_id; }
            set { _track_id = value; }
        }
        #endregion

        #region Constructors
        public ReadMAGClass(string command, string track_id)
        {
            _command = command;
            _track_id = track_id;
        }
        #endregion
    }

    public class EmbossLine
    {
        #region Declarations
        private string _font;
        private string _cpi;
        private string _x;
        private string _y;
        private string _text;
        #endregion

        #region Properties
        public string font
        {
            get { return _font; }
            set { _font = value; }
        }
        public string cpi
        {
            get { return _cpi; }
            set { _cpi = value; }
        }
        public string x
        {
            get { return _x; }
            set { _x = value; }
        }
        public string y
        {
            get { return _y; }
            set { _y = value; }
        }
        public string text
        {
            get { return _text; }
            set { _text = value; }
        }
        #endregion

        #region Constructors
        public EmbossLine(string font, string cpi, string x, string y, string text)
        {
            _font = font;
            _cpi = cpi;
            _x = x;
            _y = y;
            _text = text;
        }
        #endregion
    }
    public class EmbossLineClass
    {
        #region Declarations
        private string _command;
        private List<EmbossLine> _emboss_line;
        private string _tipper_on;
        private string _tipper_termperature;
        private string _tipper_pressure;
        private string _tipper_consuming;
        private string _tipper_time;
        #endregion

        #region Properties
        public string Command
        {
            get { return _command; }
            set { _command = value; }
        }
        public List<EmbossLine> Emboss_Line
        {
            get { return _emboss_line; }
            set { _emboss_line = value; }
        }
        public string Tipper_ON
        {
            get { return _tipper_on; }
            set { _tipper_on = value; }
        }
        public string Tipper_termperature
        {
            get { return _tipper_termperature; }
            set { _tipper_termperature = value; }
        }
        public string Tipper_pressure
        {
            get { return _tipper_pressure; }
            set { _tipper_pressure = value; }
        }
        public string Tipper_consuming
        {
            get { return _tipper_consuming; }
            set { _tipper_consuming = value; }
        }
        public string Tipper_time
        {
            get { return _tipper_time; }
            set { _tipper_time = value; }
        }
        #endregion

        #region Constructors
        public EmbossLineClass(string command, List<EmbossLine> emboss_line, string tipper_on,string tipTemp,string tipPress,string tipCons,string tipTime)
        {
            _command = command;
            _emboss_line = emboss_line;
            _tipper_on = tipper_on;
            _tipper_pressure = tipPress;
            _tipper_termperature = tipTemp;
            _tipper_consuming = tipCons;
            _tipper_time = tipTime;
        }
        #endregion
    }

    public class AnswerClass
    {
        #region Declarations
        private string _answer;
        private Error _error;
        private string _data;
        #endregion

        #region Properties
        public string Answer
        {
            get { return _answer; }
            set { _answer = value; }
        }
        public Error Error
        {
            get { return _error; }
            set { _error = value; }
        }
        public string Data
        {
            get { return _data; }
            set { _data = value; }
        }
        #endregion

        #region Constructors
        public AnswerClass()
        {
            _answer = string.Empty;
            _error = new Error();
            _data = string.Empty;
        }

        public AnswerClass(string answer, string data, string group, string code, string message)
        {
            _answer = answer;
            _error = new Error(group, code, message);
            _data = data;
        }
        #endregion
    }
    public class Error
    {
        #region Declarations
        private string _group;
        private string _code;
        private string _message;
        #endregion

        #region Properties
        public string group
        {
            get { return _group; }
            set { _group = value; }
        }
        public string code
        {
            get { return _code; }
            set { _code = value; }
        }
        public string message
        {
            get { return _message; }
            set { _message = value; }
        }
        #endregion

        #region Constructors
        public Error()
        {
            _group = string.Empty;
            _code = string.Empty;
            _message = string.Empty;
        }

        public Error(string group, string code, string message)
        {
            _group = group;
            _code = code;
            _message = message;
        }
        #endregion
    }
}
