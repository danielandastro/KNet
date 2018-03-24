
using System;
using System.Net;

namespace KNet_Client
{
    class Client : KNet.KNet_Client
    {
        public Client(string ipAddress) : base(IPAddress.Parse(ipAddress), 4855) { }

        public void RPC_Echo(string message)
        {
            Console.WriteLine("Remote: " + message);
        }

        public void RPC_LoginSuccess() { Console.WriteLine("Successfully logged in."); }

        public void RPC_LoginFailed(string message)
        {
            Console.WriteLine("Login failed. " + message);
        }

    }

    class Program
    {

        static void Main(string[] args)
        {
            Console.Write("Input target IP Address: ");
            Client client = new Client(Console.ReadLine());
            client.Start();
            client.Login("", "");

            while(client.isRunning)
            {
                string message = Console.ReadLine();
                client.SendRPC("RPC_Echo", new byte[0], message);
            }

        }
    }
}
