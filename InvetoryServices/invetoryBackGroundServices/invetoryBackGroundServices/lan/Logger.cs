    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using invetoryBackGroundServices.Security;

namespace MATICA_S3300e.LAN
{
    public class Logger
    {
        #region Declarations
        private string _fileName;
        private string _filePath;
        private string _fullName;
        private bool _logForDay;
        private readonly IFileEncryptionService _encryption;

        private const string DBG_FileName = "DBG_AddDebugLog.txt";

        private Mutex objMutex = new Mutex();
        private Stopwatch _chrono;
        private TimeCounter _counter;

        public enum LogType
        {
            Error,
            Info,
            Debug,
            Trans
        }
        #endregion

        #region Properties
        public string FileName
        {
            get { return _fileName; }
        }
        public string FilePath
        {
            get { return _filePath; }
        }
        public string FullName
        {
            get { return _fullName; }
        }
        public TimeCounter Counter
        {
            get { return _counter; }
        }
        public bool LogForDay
        {
            get { return _logForDay; }
        }
        private string DefaultFileName
        {
            get { return Assembly.GetExecutingAssembly().GetName().Name; }
        }
        private static string DefaultFilePath
        {
            get { return "C:\\temp\\" + Assembly.GetExecutingAssembly().GetName().Name + "_Log\\"; }
        }
        #endregion

        #region Constructors
        public Logger(IFileEncryptionService encryption) : this(encryption, true) { }

        public Logger(IFileEncryptionService encryption, bool deleteOldLog)
            : this(encryption, Assembly.GetExecutingAssembly().GetName().Name, DefaultFilePath, deleteOldLog) { }

        public Logger(IFileEncryptionService encryption, string name, string path, bool deleteOldLog)
            : this(encryption, name, path, deleteOldLog, true) { }

        public Logger(IFileEncryptionService encryption, string name, string path, bool deleteOldLog, bool logForDay)
        {
            _encryption = encryption;
            _fileName = name;
            _filePath = (path[path.Length - 1] != '\\') ? path + "\\" : path;
            _logForDay = logForDay;

            StartLog();

            _counter = new TimeCounter(_filePath, _fileName, this);

            if (deleteOldLog) DeleteOldFiles();
        }

        #endregion

        #region Methods

        #region Public
        /// <summary>
        /// Append string to log file
        /// </summary>
        /// <param name="toAppend">string to append</param>
        /// <param name="type">log type</param>
        public void AppendLog(string toAppend, LogType type)
        {
            lock (objMutex)
            {
                StringBuilder sbLog = new StringBuilder();

                if (type == LogType.Debug && !File.Exists(DBG_FileName)) return;

                sbLog.Append(GetLogDate() + " | ");
                sbLog.Append("[" + type.ToString() + "] >> ");
                // Matica Print Flow, file-encryption phase: AES-GCM via the shared
                // IFileEncryptionService, replacing the old TripleDES call - fixed key and fixed
                // all-zero IV, confirmed directly against real log files where the same ciphertext
                // block appeared verbatim over a year apart. Every AppendLog call gets its own
                // fresh random nonce now; see AesGcmFileEncryptionService's own doc comment for the
                // full reasoning. The "[Username:...]" prefix from the old implementation is
                // dropped - CLS.GLOBALS._loginUname was never actually set anywhere in this
                // codebase (confirmed by grep before this phase), so it was always empty; not
                // worth carrying an always-empty field into the new format.
                string cipherText = _encryption.Encrypt(toAppend);
                sbLog.Append(cipherText + Environment.NewLine);

                File.AppendAllText(_fullName, sbLog.ToString());
            }
        }

        #region Chrono

        /// <summary>
        /// Starts chronometer
        /// </summary>
        public void StartChrono()
        {
            if (_chrono == null) _chrono = new Stopwatch();
            if (_chrono.IsRunning) _chrono.Stop();

            _chrono.Start();
            AppendLog("Chronometer Started", LogType.Info);
        }

        /// <summary>
        /// Stops chronometer
        /// </summary>
        /// <returns>
        /// return chrono TimeSpan (empty if Error)
        /// </returns>
        public TimeSpan StopChrono()
        {
            if (_chrono == null) return new TimeSpan();

            _chrono.Stop();

            StringBuilder sbChrono = new StringBuilder();
            TimeSpan ts = _chrono.Elapsed;

            sbChrono.Append("Chronometer stopped >> Time: ");

            sbChrono.Append(string.Format("{0:00}:{1:00}:{2:00}.{3:000}",
                                          ts.Hours,
                                          ts.Minutes,
                                          ts.Seconds,
                                          ts.Milliseconds));

            AppendLog(sbChrono.ToString(), LogType.Info);

            return ts;
        }
        #endregion

        #endregion

        #region Private
        /// <summary>
        /// Starts the Log
        /// </summary>
        private void StartLog()
        {
            CheckFolder();
            _fullName = GetFullFileName();

            if (!File.Exists(_fullName))
            {
                File.AppendAllText(_fullName, string.Empty);
                LogStartLines();
            }
        }

        /// <summary>
        /// Check if path exist.
        /// if not, creates it.
        /// </summary>
        private void CheckFolder()
        {
            if (!Directory.Exists(_filePath))
                Directory.CreateDirectory(_filePath);
        }

        /// <summary>
        /// Get date for log file name
        /// </summary>
        /// <returns></returns>
        private string GetStartDate()
        {
            string _fullDate = string.Empty;

            if (_logForDay)
            {
                _fullDate = DateTime.Now.ToString("MM-dd-yyyy");
            }
            else
            {
                string date = DateTime.Now.ToString("MMddyyyy");
                string hour = DateTime.Now.ToString("HHmmss");

                _fullDate = date + "_" + hour;
            }

            return _fullDate;
        }

        /// <summary>
        /// get date for log lines
        /// </summary>
        /// <returns></returns>
        private string GetLogDate()
        {
            string _fullDate = string.Empty;

            if (_logForDay)
            {
                _fullDate = DateTime.Now.ToString("HH:mm:ss.fff");
            }
            else
            {
                string date = DateTime.Now.ToString("MM/dd/yyyy");
                string hour = DateTime.Now.ToString("HH:mm:ss.fff");

                _fullDate = date + " " + hour;
            }

            return _fullDate;
        }

        /// <summary>
        /// Get full path name
        /// </summary>
        /// <returns></returns>
        private string GetFullFileName()
        {
            return _filePath + _fileName + "_" + CLS.GLOBALS._branchID + "_" + GetStartDate() + ".log";
        }

        private void LogStartLines()
        {
            AppendLog("App Name >> " + Assembly.GetExecutingAssembly().GetName().Name, LogType.Info);
            AppendLog("App Version >> " + Assembly.GetExecutingAssembly().GetName().Version, LogType.Info);
        }

        /// <summary>
        /// delete files older than 7 days
        /// </summary>
        private void DeleteOldFiles()
        {
            string[] files = Directory.GetFiles(_filePath);

            foreach (string file in files)
            {
                if (file.Contains(_fileName))
                {
                    FileInfo _fileInfo = new FileInfo(file);

                    if (_fileInfo.LastAccessTime < DateTime.Now.AddDays(-7))
                    {
                        _fileInfo.Delete();
                    }
                }
            }
        }
        #endregion

        #endregion

        #region Nested Class
        public class TimeCounter
        {
            #region Declarations
            private string _path;
            private string _name;
            private string _fullName;

            private Logger _log;
            private Stopwatch chronoCounter;
            #endregion

            #region Properties
            public string CounterFullName => _fullName;
            public string CounterPath => _path;
            public string CounterName => _name;
            public string GetDate => DateTime.Now.ToString("MM/dd/yyyy");
            #endregion

            #region Constructors
            public TimeCounter(string path, string name, Logger log)
            {
                _path = path + "Counter\\";
                _name = name + "_Count.txt";

                _fullName = _path + _name;

                _log = log;
                chronoCounter = new Stopwatch();
            }
            #endregion

            #region Methods

            #region Public
            public void Start()
            {
                CheckFolder();

                if (chronoCounter.IsRunning) chronoCounter.Stop();
                chronoCounter.Start();

                _log.AppendLog("Counter chrono >> Started", LogType.Info);
            }

            public void Stop()
            {
                if (chronoCounter.IsRunning)
                {
                    chronoCounter.Stop();
                    TimeSpan ts = chronoCounter.Elapsed;

                    _log.AppendLog("Counter chrono >> Stopped", LogType.Info);

                    Worker(ts);
                }
            }
            #endregion

            #region Private
            /// <summary>
            /// Check if path exist.
            /// if not, creates it.
            /// </summary>
            private void CheckFolder()
            {
                if (!Directory.Exists(_path))
                    Directory.CreateDirectory(_path);
            }

            private void CreateCounterFile()
            {
                File.AppendAllText(_fullName, "Time Counter:" + Environment.NewLine);
            }

            private bool dayCheck(ref int index)
            {
                StreamReader file = new StreamReader(_fullName);
                string line;

                while ((line = file.ReadLine()) != null)
                {
                    if (line.Contains(GetDate))
                    {
                        file.Close();
                        return true;
                    }

                    index++;
                }

                file.Close();
                index = -1;
                return false;
            }

            private string NewLine(TimeSpan time, string old)
            {
                string _new = string.Empty;
                TimeSpan oldTime;
                TimeSpan newtime;



                #region Split
                //mm:gg:yyyy >> hh:mm:ss.fff
                string[] splitWords = new string[3];
                splitWords = old.Split(' ');

                //hh mm ss.fff
                string[] splitTime = new string[3];
                splitTime = splitWords[2].Split(':');

                //ss fff
                string[] splitMS = new string[2];
                splitMS = splitTime[2].Split('.');
                #endregion

                oldTime = new TimeSpan(0,
                                       Convert.ToInt32(splitTime[0]),
                                       Convert.ToInt32(splitTime[1]),
                                       Convert.ToInt32(splitMS[0]),
                                       Convert.ToInt32(splitMS[1]));

                newtime = time + oldTime;
                _new = string.Format("{0:00}:{1:00}:{2:00}.{3:000}",
                                          newtime.Hours,
                                          newtime.Minutes,
                                          newtime.Seconds,
                                          newtime.Milliseconds);

                _log.AppendLog("Old time >> " + splitWords[2], LogType.Debug);
                _log.AppendLog("New time >> " + _new, LogType.Debug);

                return old.Replace(splitWords[2], _new);
            }

            private string ReadFileText()
            {
                var lines = File.ReadAllLines(_fullName);

                if (lines[lines.Length - 1].Contains("Total"))
                    File.WriteAllLines(_fullName, lines.Take(lines.Length - 1).ToArray());

                return File.ReadAllText(_fullName);
            }

            private string getTotal()
            {
                string lastLine = "Total >> ";
                TimeSpan Total = new TimeSpan(0, 0, 0);

                var lines = File.ReadAllLines(_fullName);

                for (int i = 1; i < lines.Length; i++)
                {
                    #region Split
                    //mm:gg:yyyy >> hh:mm:ss.fff
                    string[] splitWords = new string[3];
                    splitWords = lines[i].Split(' ');

                    //hh mm ss.fff
                    string[] splitTime = new string[3];
                    splitTime = splitWords[2].Split(':');

                    //ss fff
                    string[] splitMS = new string[2];
                    splitMS = splitTime[2].Split('.');
                    #endregion

                    TimeSpan temp = new TimeSpan(0,
                                                 Convert.ToInt32(splitTime[0]),
                                                 Convert.ToInt32(splitTime[1]),
                                                 Convert.ToInt32(splitMS[0]),
                                                 Convert.ToInt32(splitMS[1]));

                    Total += temp;
                }

                if (Total.Days != 0) lastLine += string.Format("{0:00} ", Total.Days);

                lastLine += string.Format("{0:00}:{1:00}:{2:00}.{3:000}",
                                          Total.Hours,
                                          Total.Minutes,
                                          Total.Seconds,
                                          Total.Milliseconds);

                _log.AppendLog(lastLine, LogType.Debug);

                return lastLine;
            }

            private void Worker(TimeSpan time)
            {
                CheckFolder();
                int index = 0;

                if (!File.Exists(_fullName)) CreateCounterFile();

                string _fileText = ReadFileText();

                if (dayCheck(ref index))
                {
                    _log.AppendLog("Day found >> " + GetDate, LogType.Debug);

                    string oldLine = File.ReadAllLines(_fullName).Skip(index).Take(1).First();
                    string newLine = NewLine(time, oldLine);

                    _fileText = _fileText.Replace(oldLine, newLine);
                }
                else
                {
                    _log.AppendLog("Day not found >> " + GetDate, LogType.Debug);

                    string timeToAppend = string.Format("{0:00}:{1:00}:{2:00}.{3:000}",
                                                    time.Hours,
                                                    time.Minutes,
                                                    time.Seconds,
                                                    time.Milliseconds);

                    _log.AppendLog("New Time >> " + timeToAppend, LogType.Debug);

                    _fileText += GetDate + " >> " + timeToAppend + Environment.NewLine;
                }

                File.Delete(_fullName);
                File.WriteAllText(_fullName, _fileText);

                _fileText += getTotal();

                File.Delete(_fullName);
                File.AppendAllText(_fullName, _fileText);
            }

            #endregion

            #endregion
        }
        #endregion
    }
}
