using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    class Program
    {
        static Board board;

        static Map<(IPEndPoint, string), Player> players;
        static Dictionary<IPEndPoint, Queue<(uint, byte[])>> playersPackets;

        static ASCIIEncoding ascii = new ASCIIEncoding();

        static uint _packetId = 0;
        static uint PacketId { get => _packetId++; }

        static int localPort = 7777;
        static UdpClient UDPClient;
        static float untilStart = 7;

        static void Main(string[] args)
        {
            UDPClient = new UdpClient(localPort);
            UDPClient.DontFragment = true;
            InitGame();
            UDPClient.BeginReceive(UDPRecieveCallback, null);
            while (true) 
            {
                if (untilStart > 0)
                    untilStart -= Utils.FrameTime;
                else
                    Update();
                System.Threading.Thread.Sleep(5);
            }

        }
        public static void InitGame()
        {
            players = new Map<(IPEndPoint, string), Player>();
            playersPackets = new Dictionary<IPEndPoint, Queue<(uint, byte[])>>();
            board = new Board();
            board.foodFillBoard();
        }
        public static Player SpawnPlayer()
        {
            return board.spawnPlayer();
        }
        public static void RemovePlayer(Player player)
        {
            board.removeEntity(player);
        }
        public static void Update()
        {
            List<Task> tasks = new List<Task>();
            var newStates = board.updateBoard(Utils.FrameTime);
            foreach(var state in newStates)
            {
                tasks.Add(Task.Run(() => SendData(state)));
            }
            Task.WaitAll(tasks.ToArray());
        }

        public static void SendData((Player, NewState) state)
        {
            var User = players.Reverse[state.Item1];

            state.Item2.goneEntities.UnionWith(state.Item2.loadedEntities.Intersect(board.goneEntities));

            SendUpdate(players.Forward[User], User.Item1);
            SendDisappear(state.Item2.goneEntities, User.Item1);
            SendAppear(state.Item2.newEntities, User.Item1);
        }

        public static void SendUpdate(Player player, IPEndPoint endpoint)
        {
            if (board.players.Count() <= 0)
                return;

            var bytes = new List<byte>() { 102 };
            bytes.AddRange(BitConverter.GetBytes((ushort)board.players.Count()));

            foreach (var p in board.players)
            {
                bytes.AddRange(BitConverter.GetBytes((ushort)Math.Round(p.center.Item1)));
                bytes.AddRange(BitConverter.GetBytes((ushort)Math.Round(p.center.Item2)));
                bytes.AddRange(BitConverter.GetBytes((ushort)p.radius));

                var name = players.Reverse[p].Item2;

                bytes.Add((byte)name.Length);
                bytes.AddRange(ascii.GetBytes(name));
            }
            var arr = bytes.ToArray();
            UDPClient.BeginSend(arr, arr.Length, endpoint, UDPSendCallback, arr);
        }
        public static void SendDisappear(HashSet<Cell> entities, IPEndPoint endPoint)
        {
            if (entities.Count() <= 0)
                return;
            int n = entities.Count() / 50 + 1;
            var packetIds = new uint[n];

            var packets = new List<byte>[n];

            for (int i = 0; i < n; i++)
            {
                packets[i] = new List<byte>() { 101 };
                packetIds[i] = PacketId;
                packets[i].AddRange(BitConverter.GetBytes(packetIds[i]));
                int count = i == n - 1 ? entities.Count() % 50 : 50;
                packets[i].AddRange(BitConverter.GetBytes((ushort)count));
            }

            int entityN = 0;
            int packetN = 0;
            foreach (var entity in entities)
            {
                string name = "";

                if (typeof(Player).IsInstanceOfType(entity))
                    name = players.Reverse[(Player)entity].Item2;
                else //if (typeof(Food).IsInstanceOfType(entity))
                    name = $"#{((Food)entity).Id}";

                packets[packetN].Add((byte)name.Length);
                packets[packetN].AddRange(ascii.GetBytes(name));

                entityN++;
                if (entityN >= 50)
                {
                    packetN++;
                    entityN = 0;
                }
            }
            for (int i = 0; i < packets.Length; i++)
            {
                var arr = packets[i].ToArray();
                UDPClient.BeginSend(arr, arr.Length, endPoint, UDPSendCallback, arr);
                playersPackets[endPoint].Enqueue((packetIds[i], arr));
            }
        }
        public static void SendAppear(List<HashSet<Cell>> listEntities, IPEndPoint endPoint)
        {
            if (listEntities.Count() <= 0 || listEntities[0].Count() <= 0)
                return;

            int c = 0;
            foreach (var list in listEntities)
                c += list.Count;

            int n = c / 50 + 1;
            var packetIds = new uint[n];

            var packets = new List<byte>[n];

            for (int i = 0; i < n; i++)
            {
                packets[i] = new List<byte>() { 100 };
                packetIds[i] = PacketId;
                packets[i].AddRange(BitConverter.GetBytes(packetIds[i]));
                int count = i == n - 1 ? c % 50 : 50;
                packets[i].AddRange(BitConverter.GetBytes((ushort)count));
            }

            int entityN = 0;
            int packetN = 0;
            foreach (var entities in listEntities)
                foreach (var entity in entities)
                {
                    packets[packetN].AddRange(BitConverter.GetBytes((ushort)Math.Round(entity.center.Item1)));
                    packets[packetN].AddRange(BitConverter.GetBytes((ushort)Math.Round(entity.center.Item2)));
                    packets[packetN].AddRange(BitConverter.GetBytes((ushort)entity.radius));

                    string name = "";

                    if (typeof(Player).IsInstanceOfType(entity))
                        name = players.Reverse[(Player)entity].Item2;
                    else //if (typeof(Food).IsInstanceOfType(entity))
                        name = $"#{((Food)entity).Id}";
                    
                    packets[packetN].Add((byte)name.Length);
                    packets[packetN].AddRange(ascii.GetBytes(name));

                    entityN++;
                    if (entityN >= 50)
                    {
                        packetN++;
                        entityN = 0;
                    }
                }

            for (int i = 0; i < packets.Length; i++)
            {
                var arr = packets[i].ToArray();
                UDPClient.BeginSend(arr, arr.Length, endPoint, UDPSendCallback, arr);
                playersPackets[endPoint].Enqueue((packetIds[i], arr));
            }
        }

        

        public static void SendConnect(string newName, IPEndPoint endPoint)
        {
            var packetId = PacketId;
            var bytes = new List<byte>() { 254 };
            bytes.AddRange(BitConverter.GetBytes(packetId));
            bytes.Add((byte)newName.Length);
            bytes.AddRange(ascii.GetBytes(newName));

            var arr = bytes.ToArray();
            UDPClient.BeginSend(arr, arr.Length, endPoint, UDPSendCallback, arr);
            playersPackets[endPoint].Enqueue((packetId, arr));
        }

        static void UDPSendCallback(IAsyncResult result)
        {
            var data = (byte[])result.AsyncState;
            var sent = UDPClient.EndSend(result);
            if (sent < data.Length)
                Console.WriteLine($"UDP data sent: {data.Length} -> {sent}");
        }
        public static void UDPRecieveCallback(IAsyncResult result)
        {
            IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = UDPClient.EndReceive(result, ref clientEndPoint);

            UDPClient.BeginReceive(UDPRecieveCallback, null);

            Player player = null;
            string name = "";
            uint packetId = uint.MaxValue;
            try
            {
                switch (data[0])
                {
                    case 200:
                        name = ascii.GetString(data, 2, data[1]);
                        packetId = BitConverter.ToUInt32(data, 2 + data[1]);
                        break;
                    //case 253:
                    //    name = ascii.GetString(data, 2, data[1]);
                    //    player = players.Forward[(clientEndPoint, name)];
                    //    Task.Run(() => SendAppear(new List<HashSet<Cell>>() { player.getLoadedArea() }, clientEndPoint));
                    //    Console.WriteLine($"Requested init Data {name} ip - {clientEndPoint}");
                    //    break;
                    case 254:
                        name = ascii.GetString(data, 2, data[1]);
                        player = SpawnPlayer();
                        name += "#" + player.Id;
                        players.Add((clientEndPoint, name),
                            player);
                        playersPackets.Add(clientEndPoint, new Queue<(uint, byte[])>());
                        Task.Run(() => SendConnect(name, clientEndPoint));
                        Console.WriteLine($"Connected {name} ip - {clientEndPoint}");
                        break;

                    case 255:
                        name = ascii.GetString(data, 2, data[1]);
                        player = players.Forward[(clientEndPoint, name)];
                        RemovePlayer(player);
                        players.Remove(player);

                        Console.WriteLine($"Disconnected {name} ip - {clientEndPoint}");
                        break;

                    case 102:
                        name = ascii.GetString(data, 2, data[1]);
                        player = players.Forward[(clientEndPoint, name)];
                        var offset = 2 + data[1];
                        (float, float) moveVec = (
                            BitConverter.ToSingle(data, offset),
                            BitConverter.ToSingle(data, offset + sizeof(float))
                            );

                        player.moveVec = moveVec;
                        Console.WriteLine($"Updating {name} ip - {clientEndPoint}");
                        break;
                }
            }
            catch (Exception error)
            {
                Console.WriteLine($"--- UDPRecieveCallback Error ---\n" +
                    $"{error.StackTrace}\n" +
                    $"{error.Message}" +
                    $"- UDPRecieveCallback Error End -");
            }

            var packets = playersPackets[clientEndPoint];
            if (packets.Count <= 0)
                return;

            var lostPacket = packets.Peek();
            if (lostPacket.Item1 == packetId)
            {
                packets.Dequeue();
                if (lostPacket.Item2[0] == 254)
                {
                    player = players.Forward[(clientEndPoint, name)];
                    Task.Run(() => SendAppear(new List<HashSet<Cell>>() { player.getLoadedArea() }, clientEndPoint));
                }
            }
            else
            {
                var bytes = lostPacket.Item2;
                UDPClient.BeginSend(bytes, bytes.Length, clientEndPoint, UDPSendCallback, bytes);
            }


        }

    }
}