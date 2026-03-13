using System;
using System.Collections.Generic;
using System.IO;
using PX.Data;

namespace FinancialReport.Helper
{
    public static class TraceLogger
    {
        private static readonly string BaseDirectory = "C:\\Program Files\\Acumatica ERP\\saga\\App_Data\\Logs\\FinancialReports\\Overview Trace\\";
        private static readonly string LogFileName = $"FinancialReportTrace_{DateTime.Now:yyyyMMdd_HHmmss}.log";
        private static readonly string LogPath;

        static TraceLogger()
        {
            // Ensure directory exists
            if (!Directory.Exists(BaseDirectory))
            {
                Directory.CreateDirectory(BaseDirectory);
            }

            LogPath = Path.Combine(BaseDirectory, LogFileName);

            // Optionally, you can log that the logger started
            WriteToFile("[INFO]", "TraceLogger initialized.");
        }

        public static void Info(string message)
        {
            //PXTrace.WriteInformation(message);
            WriteToFile("[INFO]", message);
        }

        public static void Error(string message)
        {
            //PXTrace.WriteError(message);
            WriteToFile("[ERROR]", message);
        }

        private static void WriteToFile(string level, string message)
        {
            try
            {
                File.AppendAllText(LogPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {level} {message}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                PXTrace.WriteError($"Failed to write to trace file: {ex.Message}");
            }
        }
    }
}
