using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace MikrotikExporter
{
    public class Log
    {
        public static bool PrintDebug1 { get; set; } = false;
        public static bool PrintDebug2 { get; set; } = false;
        public static Log Main { get; } = new Log(Array.Empty<string>());
        private static readonly object consoleLock = new object();
        private readonly string[] contexts;

        private void Write(string type, ConsoleColor color, string message)
        {
            lock (consoleLock)
            {
                Console.Write($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ");
                Console.ForegroundColor = color;
                Console.Write(type);
                Console.ResetColor();
                Console.WriteLine($" {String.Join("", contexts.Select(context => $"[{context}]").ToArray())}: {message}");
            }
        }

        public void Info(string message)
        {
            Write("INFO", ConsoleColor.White, message);
        }

        public void Error(string message)
        {
            Write("ERROR", ConsoleColor.Red, message);
        }

        public void Debug1(string message)
        {
            if (PrintDebug1 || PrintDebug2)
            {
                Write("DEBUG1", ConsoleColor.Yellow, message);
            }
        }

        public void Debug2(string message)
        {
            if (PrintDebug2)
            {
                Write("DEBUG2", ConsoleColor.Cyan, message);
            }
        }

        private Log(string[] contexts)
        {
            this.contexts = contexts;
        }

        public Log CreateContext(string context)
        {
            return new Log(contexts.Concat(new string[] { context }).ToArray());
        }
    }
}
