using System;
using nanoKontrol2OBS;

namespace ConsoleExecutable
{
    class Executable
    {

        public Executable(string url, string password)
        {
            Kontrol2OBS control = new Kontrol2OBS(url, password,
                (s,e) => { this.WriteLine(LogType.Status, e.text); },
                (s, e) => { this.WriteLine(LogType.Warning, e.text); },
                (s, e) => { this.WriteLine(LogType.Information, e.text); }
            );

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
                ConsoleKeyInfo key;
                string password = "";
                do
                {
                    key = Console.ReadKey();
                    if (key.Key == ConsoleKey.Backspace)
                        password = password.Substring(0, (password.Length > 0) ? password.Length - 1 : 0);
                    else if(key.Key != ConsoleKey.Enter)
                        password += key.KeyChar;
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write(new string(' ', password.Length + 1));
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write(new string('*', password.Length));
                } while (key.Key != ConsoleKey.Enter);
                Console.Clear();
                new Executable((ip == "") ? "127.0.0.1:4444" : ip, password);
            }
            else if (args.Length == 1)
                new Executable(args[0], "");
            else if (args.Length == 2)
                new Executable(args[0], args[1]);
        }
    }
}
