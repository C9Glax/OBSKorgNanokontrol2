using System;
using nanoKontrol2OBS;

namespace ConsoleExecutable
{
    class Executable
    {

        public Executable(string url, string password)
        {
            Kontrol2OBS control = new Kontrol2OBS(url, password);
            control.OnStatusLog += (s, e) => { this.WriteLine(LogType.Status, e.text); };
            control.OnWarningLog += (s, e) => { this.WriteLine(LogType.Warning, e.text); };
            control.OnInfoLog += (s, e) => { this.WriteLine(LogType.Information, e.text); };
            control.Create();

            this.WriteLine(LogType.Information, "Press Escape for clean shutdown.");
            while (Console.ReadKey().Key != ConsoleKey.Escape) ;
            control.Dispose();
        }

        private enum LogType { Information, Warning, Status}
        private void WriteLine(LogType logtype, string text)
        {
            DateTime logtime = DateTime.Now;
            string time = string.Format("{0}:{1}:{2}.{3}", logtime.Hour, logtime.Minute, logtime.Second, logtime.Millisecond);
            string type;
            switch (logtype)
            {
                case LogType.Information:
                    Console.ForegroundColor = ConsoleColor.Gray;
                    type = "Info";
                    break;
                case LogType.Warning:
                    Console.ForegroundColor = ConsoleColor.Red;
                    type = "WARN";
                    break;
                case LogType.Status:
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    type = "Status";
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.White;
                    type = "";
                    break;
            }
            Console.WriteLine("[{0}] ({1}) {2}", time, type, text);
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("obs-websocket address (Press enter for 127.0.0.1:4444):");
                string ip = Console.ReadLine();
                Console.WriteLine("Password (Press Enter if none):");
                string password = Console.ReadLine();
                new Executable((ip == "") ? "127.0.0.1:4444" : ip, password);
            }
            else if (args.Length == 1)
                new Executable(args[0], "");
            else if (args.Length == 2)
                new Executable(args[0], args[1]);
        }
    }
}
