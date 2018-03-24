using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

namespace KNet
{

    public static class KNet
    {
        private static KNet_Server server = null;
        private static KNet_Client client = null;

        public static KNet_Packet DeserializePacket(byte[] buffer, int offset, int count)
        {
            using (MemoryStream memStream = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                memStream.Write(buffer, offset, count);
                memStream.Seek(0, SeekOrigin.Begin);
                KNet_Packet packet = (KNet_Packet)formatter.Deserialize(memStream);
                return packet;
            }
        }

        public static byte[] SerializePacket(KNet_Packet packet)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            using (MemoryStream memStream = new MemoryStream())
            {
                formatter.Serialize(memStream, packet);
                return memStream.ToArray();
            }
        }

        public static bool ExecutePacket(KNet_Packet packet)
        {
            if (packet.rpcName.StartsWith("RPC"))
            {
                if (client == null) return false;

                MethodInfo method = client.GetType().GetMethod(packet.rpcName);
                if (method == null) return false;
                method.Invoke(client, packet.rpcArgs);
                return true;
            }
            else if (packet.rpcName.StartsWith("CMD"))
            {
                if (server == null) return false;

                MethodInfo method = server.GetType().GetMethod(packet.rpcName);
                if (method == null) return false;
                method.Invoke(server, packet.rpcArgs);
                return true;
            }
            else return false;

        }

        public static bool SetServer(KNet_Server server)
        {
            if (KNet.server == null)
            {
                KNet.server = server;
                return true;
            }
            else return false;
        }

        public static bool SetClient(KNet_Client client)
        {
            if (KNet.client == null)
            {
                KNet.client = client;
                return true;
            }
            else return false;
        }

    }

    [Serializable]
    public struct KNet_Packet
    {
        public string rpcName;
        public object[] rpcArgs;
        public byte[] rpcTarget;
        public byte[] origin;

        public override string ToString()
        {
            string args = "rpcArgs->[";
            foreach(object o in rpcArgs)
            {
                args += o.ToString() + ", ";
            }
            args = args.TrimEnd(',', ' ');
            args += "]";

            string origin_str = "origin->";
            if (origin != null)
            {
                origin_str += "[";
                foreach (byte b in origin)
                {
                    origin_str += b.ToString() + ", ";
                }
                origin_str = origin_str.TrimEnd(',', ' ');
                origin_str += "]";
            }
            else origin_str += "NULL";

            string target = "rpcTarget->";
            if (rpcTarget != null)
            {
                target += "[";
                foreach (byte b in rpcTarget)
                {
                    target += b.ToString() + ", ";
                }
                target = target.TrimEnd(',', ' ');
                target += "]";
            }
            else target += "NULL";

            return "KNet_Packet::rpcName->" + rpcName + "::" + args + "::" + target + "::" + origin_str;
        }
    }

    public class KNet_User
    {
        public byte[] id;
        public string username;
        public string passwordHash;

        public KNet_User(string username, string passwordHash, byte[] id)
        {
            this.username = username;
            this.passwordHash = passwordHash;
            this.id = id;
        }

    }

    public class KNet_Server
    {
        public bool isRunning { get { return !isShutdownRequested; } }
        private Socket receiveSocket, sendSocket;
        private uint maxPlayers;
        private bool isShutdownRequested = false;
        private ushort port;
        private Queue<KNet_User> loginQueue = new Queue<KNet_User>();
        private List<KNet_User> activeUsers = new List<KNet_User>();
        private byte[] originOfCurrentProcessedPacket = new byte[0];

        public KNet_Server(ushort port, uint maxPlayers)
        {
            if (!KNet.SetServer(this))
            {
                isShutdownRequested = true;
                throw new Exception("Cannot start server when another server is already running!");
            }
            this.maxPlayers = maxPlayers;
            this.port = port;
        }

        public void Start()
        {
            receiveSocket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            receiveSocket.Bind(new IPEndPoint(IPAddress.Any, port));
            sendSocket = new Socket(SocketType.Dgram, ProtocolType.Udp);

            Thread thread = new Thread(new ThreadStart(HandleIncomingData));
            thread.Start();

            thread = new Thread(new ThreadStart(HandleLogins));
            thread.Start();
        }

        public void SendPacket(KNet_Packet packet)
        {
            byte[] buffer = KNet.SerializePacket(packet);

            if (packet.rpcTarget.Length == 0 || (packet.rpcTarget.Length == 1 && packet.rpcTarget[0] == 0))
            {
                Console.WriteLine("Sending packet " + packet.rpcName + " to all users.");
                foreach (KNet_User user in activeUsers.ToArray())
                {
                    IPAddress address = new IPAddress(user.id);

                    EndPoint endPoint = new IPEndPoint(address, port);
                    sendSocket.SendTo(buffer, endPoint);
                }
            }
            else
            {
                IPAddress address = new IPAddress(packet.rpcTarget);

                Console.WriteLine("Sending packet " + packet.rpcName + " to " + address.ToString());

                EndPoint endPoint = new IPEndPoint(address, port);
                sendSocket.SendTo(buffer, endPoint);
            }

        }

        private void HandleIncomingData()
        {
            while(!isShutdownRequested)
            {

                byte[] buffer = new byte[1024];
                EndPoint endPoint = new IPEndPoint(IPAddress.Any, port);
                int received = receiveSocket.ReceiveFrom(buffer, ref endPoint);
                IPEndPoint ipEndPoint = (IPEndPoint)endPoint;

                KNet_Packet packet = KNet.DeserializePacket(buffer, 0, received);
                packet.origin = ipEndPoint.Address.GetAddressBytes();

                Console.WriteLine("Received Packet " + packet.rpcName + " from " + ipEndPoint.Address.ToString());

                ParsePacket(packet);
            }

            receiveSocket.Close();
            receiveSocket.Dispose();
            sendSocket.Close();
            sendSocket.Dispose();
        }

        private void HandleLogins()
        {
            while (!isShutdownRequested)
            {
                while (loginQueue.Count == 0) ;

                KNet_User user = loginQueue.Dequeue();
                //Check database for user, send user data; if user not found, refuse them.
                //If login success:
                KNet_Packet packet = new KNet_Packet {
                    rpcName = "RPC_LoginSuccess",
                    rpcArgs = new object[0], //This will be Account Data in the future
                    rpcTarget = user.id
                };

                SendPacket(packet);
                activeUsers.Add(user);
            }
        }

        private void ParsePacket(KNet_Packet packet)
        {
            originOfCurrentProcessedPacket = packet.origin;
            if(packet.rpcName.StartsWith("RPC"))
            {
                if(IsUserActive(packet.origin))
                    SendPacket(packet);
            }else if(packet.rpcName.StartsWith("CMD"))
            {
                KNet.ExecutePacket(packet);
            }
        }

        private bool IsUserActive(byte[] id)
        {
            foreach(KNet_User user in activeUsers.ToArray())
            {
                if (id.SequenceEqual(user.id))
                    return true;
            }

            return false;
        }

        public void Shutdown()
        {
            isShutdownRequested = true;
        }

        #region RPCs

        public void CMD_Login(string username, string passwordHash)
        {
            if (activeUsers.Count >= maxPlayers && maxPlayers > 0)
            {
                KNet_Packet packet = new KNet_Packet
                {
                    rpcName = "RPC_LoginFailed",
                    rpcArgs = new object[] { "The Server was full." }, //Reason for failure
                    rpcTarget = originOfCurrentProcessedPacket
                };

                SendPacket(packet);
            }

            KNet_User user = new KNet_User(username, passwordHash, originOfCurrentProcessedPacket);
            loginQueue.Enqueue(user);
        }

        public void CMD_Logout()
        {
            foreach(KNet_User user in activeUsers.ToArray())
            {
                if(user.id.SequenceEqual(originOfCurrentProcessedPacket))
                {
                    activeUsers.Remove(user);
                    break;
                }
            }
        }

        #endregion

    }

    public class KNet_Client
    {

        private Socket sendSocket, receiveSocket;
        private EndPoint host;
        private bool isShutdownRequested = false;
        public bool isRunning { get { return !isShutdownRequested; } }

        public KNet_Client(IPAddress host, ushort port)
        {
            if(!KNet.SetClient(this))
            {
                isShutdownRequested = true;
                throw new Exception("Cannot start a Client when one already exists!");
            }
            this.host = new IPEndPoint(host, port);
        }

        public void Start()
        {
            sendSocket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            receiveSocket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            receiveSocket.Bind(new IPEndPoint(IPAddress.Loopback, ((IPEndPoint)host).Port));

            Thread thread = new Thread(new ThreadStart(HandleIncomingData));
            thread.Start();
        }

        public void Login(string username, string passwordHash)
        {
            SendRPC("CMD_Login", new byte[0], username, passwordHash);
        }

        public void SendRPC(string name, byte[] target, params object[] args)
        {
            KNet_Packet packet = new KNet_Packet
            {
                rpcName = name,
                rpcTarget = target,
                rpcArgs = args
            };

            SendPacket(packet);
        }

        private void SendPacket(KNet_Packet packet)
        {
            byte[] buffer = KNet.SerializePacket(packet);
            sendSocket.SendTo(buffer, host);
            Console.WriteLine("Sent Packet " + packet.rpcName);
        }

        private void HandleIncomingData()
        {
            while(!isShutdownRequested)
            {
                byte[] buffer = new byte[1024];
                int received = receiveSocket.Receive(buffer);

                KNet_Packet packet = KNet.DeserializePacket(buffer, 0, received);

                Console.WriteLine("Received Packet " + packet.rpcName);

                KNet.ExecutePacket(packet);
            }

            sendSocket.Close();
            sendSocket.Dispose();
            receiveSocket.Close();
            receiveSocket.Dispose();
        }

        public void Shutdown()
        {
            isShutdownRequested = true;
            SendRPC("CMD_Logout", new byte[0]);
        }

    }

}