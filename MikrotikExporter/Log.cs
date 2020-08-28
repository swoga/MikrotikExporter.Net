using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace MikrotikExporter
{
    public class Log
    {
        public enum LogLevel
        {
            Error,
            Debug1,
            Debug2
        }
        public LogLevel Level { get; set; }
        public static Log Main { get; } = new Log(Array.Empty<string>(), LogLevel.Error, null);
        private static readonly object consoleLock = new object();
        private readonly string[] contexts;
        private readonly List<string> logs;
        public string[] Logs
        {
            get
            {
                if (logs == null)
                {
                    return null;
                }

                lock (logs)
                {
                    return logs?.ToArray();
                }
            }
        }

        private void Write(string type, ConsoleColor color, string message)
        {
            var part1 = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ";
            var part2 = type;
            var part3 = $" {String.Join("", contexts.Select(context => $"[{context}]").ToArray())}: {message}";

            lock (consoleLock)
            {
                Console.Write(part1);
                Console.ForegroundColor = color;
                Console.Write(part2);
                Console.ResetColor();
                Console.WriteLine(part3);
            }

            if (logs != null)
            {
                lock (logs)
                {
                    logs.Add($"{part1}{part2}{part3}");
                }
            }
        }

        public void Info(string message)
        {
            Write("INFO", ConsoleColor.White, message);
        }

        public void Error(string message)
        {
            if (Level >= LogLevel.Error)
            {
                Write("ERROR", ConsoleColor.Red, message);
            }
        }

        public void Debug1(string message)
        {
            if (Level >= LogLevel.Debug1)
            {
                Write("DEBUG1", ConsoleColor.Yellow, message);
            }
        }

        public void Debug2(string message)
        {
            if (Level >= LogLevel.Debug2)
            {
                Write("DEBUG2", ConsoleColor.Cyan, message);
            }
        }

        private Log(string[] contexts, LogLevel level, List<string> recorder)
        {
            this.contexts = contexts;
            Level = level;
            logs = recorder;
        }

        public Log CreateContext(string context, LogLevel? forceLevel = null, bool record = false)
        {
            bool shouldRecord = record || logs != null;
            List<string> recorder = shouldRecord ? logs ?? new List<string>() : null;
            return new Log(contexts.Concat(new string[] { context }).ToArray(), forceLevel ?? Level, recorder);
        }
    }
}