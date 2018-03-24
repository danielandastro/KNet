using System;
using System.Reflection;

namespace KNet_Server
{
    class Program
    {
        static KNet.KNet_Server server;

        static void Main(string[] args)
        {

            Console.WriteLine("Starting Server...");
            try
            {
                server = new KNet.KNet_Server(4855, 0);
                server.Start();
            }
            catch(Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Message + "\n" + e.StackTrace);
                Console.ResetColor();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Server Started.");
            Program program = new Program();

            while(server.isRunning)
            {
                string command = Console.ReadLine();
                MethodInfo method = program.GetType().GetMethod(command.ToUpper());
                if(method == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Unknown command.");
                    Console.ResetColor();
                    continue;
                }
                method.Invoke(program, new object[0]);
            }

        }

        public void SHUTDOWN()
        {
            Console.WriteLine("Shutting down...");
            server.Shutdown();
            Console.WriteLine("Done.");
        }

    }
}
