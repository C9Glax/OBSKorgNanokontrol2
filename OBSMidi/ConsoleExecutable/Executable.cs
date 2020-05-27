using System;
using nanoKontrol2OBS;

namespace ConsoleExecutable
{
    class Executable
    {

        public Executable(string url, string password)
        {
            Kontrol2OBS control = new Kontrol2OBS(url, password);
            control.OnLoggingEvent += (s, e) => { Console.WriteLine(e.text); };
            control.Create();

            while (Console.ReadKey().Key != ConsoleKey.Escape) ;
            control.Dispose();
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("IP:");
                string ip = Console.ReadLine();
                Console.WriteLine("Password (Press Enter if none):");
                string password = Console.ReadLine();
                new Executable(ip, password);
            }
            else if (args.Length == 1)
                new Executable(args[0], "");
            else if (args.Length == 2)
                new Executable(args[0], args[1]);
        }
    }
}
