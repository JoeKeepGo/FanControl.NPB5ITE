using System;
using System.Diagnostics;
using System.IO;

namespace FanControl.NPB5ITE.Logging
{
    public sealed class PluginLog
    {
        private const string Prefix = "[FanControl.NPB5ITE] ";
        private readonly Action<string>? _fanControlLog;

        public PluginLog()
        {
        }

        public PluginLog(Action<string> fanControlLog)
        {
            _fanControlLog = fanControlLog;
        }

        public void Info(string message)
        {
            Write(LogLevel.Info, message);
        }

        public void Warning(string message)
        {
            Write(LogLevel.Warning, message);
        }

        public void Error(string message, Exception exception)
        {
            Write(LogLevel.Error, message + " " + exception.GetType().Name + ": " + exception.Message);
        }

        private void Write(LogLevel level, string message)
        {
            var line = Prefix + message;

            switch (level)
            {
                case LogLevel.Info:
                    Trace.TraceInformation(line);
                    break;
                case LogLevel.Warning:
                    Trace.TraceWarning(line);
                    break;
                case LogLevel.Error:
                    Trace.TraceError(line);
                    break;
            }

            try
            {
                _fanControlLog?.Invoke(line);
            }
            catch
            {
                // Logging must never break sensor updates.
            }

            try
            {
                var directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FanControl.NPB5ITE");

                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "plugin.log"), DateTimeOffset.Now.ToString("O") + " " + line + Environment.NewLine);
            }
            catch
            {
                // File logging is best-effort only.
            }
        }

        private enum LogLevel
        {
            Info,
            Warning,
            Error
        }
    }
}
