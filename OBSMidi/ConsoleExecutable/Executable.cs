using System;
using Linker;

namespace ConsoleExecutable
{
    class Executable
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("obs-websocket address (Leave empty for 127.0.0.1:4455):");
                string? ip = Console.ReadLine();
                Console.WriteLine("Password (Leave empty for none):");
                string? password = Console.ReadLine();
                Console.Clear();
                new Executable((ip ?? "") == "" ? "127.0.0.1:4455" : ip!, password ?? "");
            }
            else if (args.Length == 1)
                if (args[0].Contains("-h") || args[0].Contains("--help"))
                    PrintHelp();
                else
                    new Executable(args[0], "");
            else if (args.Length == 2)
                new Executable(args[0], args[1]);
            else
                PrintHelp();
        }

        public Executable(string url, string password)
        {
            Kontrol2OBS control = new Kontrol2OBS(url, password,
                (s,e) => { this.WriteLine(LogType.Status, e.text); },
                (s, e) => { this.WriteLine(LogType.Warning, e.text); },
                (s, e) => { this.WriteLine(LogType.Information, e.text); },
                (s, e) => { this.WriteLine(LogType.Error, e.text); }
            );

            this.WriteLine(LogType.Information, "Press Escape for clean shutdown.");
            while (Console.ReadKey().Key != ConsoleKey.Escape) ;
            control.Dispose();
        }

        private enum LogType { Information, Warning, Status, Error}
        private void WriteLine(LogType logtype, string text)
        {
            DateTime logtime = DateTime.Now;
            string timeString = string.Format("{0:D2}:{1:D2}:{2:D2}.{3:D3}", logtime.Hour, logtime.Minute, logtime.Second, logtime.Millisecond);
            string typeString;
            switch (logtype)
            {
                case LogType.Information:
                    Console.ForegroundColor = ConsoleColor.Gray;
                    typeString = "Info";
                    break;
                case LogType.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    typeString = "WARN";
                    break;
                case LogType.Status:
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    typeString = "Status";
                    break;
                case LogType.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    typeString = "ERROR";
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.White;
                    typeString = "";
                    break;
            }
            Console.WriteLine("[{0}] ({1}) {2}", timeString, typeString, text);
        }

        static void PrintHelp()
        {
            Console.WriteLine("Usage:\nConsoleExecutable.exe [<ip:port> [password]]");
        }
    }
}
