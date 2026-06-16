using System;
using System.IO;

namespace BleWorkbench.Core
{
    public enum LogLevel { Info, Success, Warn, Error, Tx, Rx }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; }

        public string TimeText { get { return Timestamp.ToString("HH:mm:ss.fff"); } }
    }

    /// <summary>
    /// Application-wide console / activity log. The UI subscribes and renders to
    /// a coloured text pane. Safe to call from any thread.
    /// </summary>
    public static class AppLog
    {
        public static event EventHandler<LogEntry> Logged;

        private static readonly object FileGate = new object();
        public static string LogFilePath { get; private set; }

        static AppLog()
        {
            try
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                Directory.CreateDirectory(dir);
                LogFilePath = Path.Combine(dir, "session-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
            }
            catch { LogFilePath = null; }
        }

        public static void Write(LogLevel level, string message)
        {
            var entry = new LogEntry { Timestamp = DateTime.Now, Level = level, Message = message ?? string.Empty };

            if (LogFilePath != null)
            {
                try
                {
                    lock (FileGate)
                        File.AppendAllText(LogFilePath, "[" + entry.TimeText + "] " + level.ToString().ToUpperInvariant() + "  " + entry.Message + Environment.NewLine);
                }
                catch { }
            }

            var handler = Logged;
            if (handler != null) handler(null, entry);
        }

        public static void Info(string m) { Write(LogLevel.Info, m); }
        public static void Success(string m) { Write(LogLevel.Success, m); }
        public static void Warn(string m) { Write(LogLevel.Warn, m); }
        public static void Error(string m) { Write(LogLevel.Error, m); }
        public static void Tx(string m) { Write(LogLevel.Tx, m); }
        public static void Rx(string m) { Write(LogLevel.Rx, m); }

        public static void Error(string context, Exception ex)
        {
            string msg = ex == null ? context : context + ": " + ex.Message;
            Write(LogLevel.Error, msg);
        }
    }
}
