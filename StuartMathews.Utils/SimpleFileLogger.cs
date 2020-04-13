using System;
using System.IO;
using Second.Utils;


namespace Utils
{
    public class SimpleFileLogger : ISimpleFileLogger
    {
        public string Filename { get; }

        public SimpleFileLogger(string filename)
        {
            Filename = filename;
        }

        public void Log(string message, bool withoutNewline = false, bool overwite = false)
        {
            var msgformat = $"\r\n{DateTime.Now.ToLongTimeString()} {DateTime.Now.ToLongDateString()} {message}";
            if (overwite)
            {
                using (var writer = File.CreateText(Filename))
                {
                    writer.WriteLine(msgformat);
                }
                return;
            }
            using (var writer = File.AppendText(Filename))
            {
                writer.WriteLine(msgformat);
            }
        }
    }

    public class NullLogger : ISimpleFileLogger
    {
        public void Log(string message, bool withoutNewline = false, bool overwite = false)
        {
            // Intentionally does not log
        }
    }
}