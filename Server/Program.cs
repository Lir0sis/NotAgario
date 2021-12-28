using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    class Program
    {
        enum ActionApplied
        {
            ADD,
            REMOVE,
            UPDATE
        }

        static Board board;

        static List<(int, string)> leaderBoard = new List<(int, string)>();

        static ClientsMap<(IPEndPoint, string), Player> players;
        static HashSet<IPEndPoint> spectators;
        static Dictionary<IPEndPoint, Queue<(uint, byte[])>> playersPackets;

        static ConcurrentQueue<(ActionApplied, dynamic[])> queuedDicts =
            new ConcurrentQueue<(ActionApplied, dynamic[])>();

        static uint _packetId = 0;
        static uint PacketId { get => _packetId++; }

        static int localPort = 7777;
        static UdpClient UDPClient;

        static readonly float START_SLEEP = 5;
        static float untilStart;
        static readonly float FPS = 30;

        static void Main(string[] args)
        {
            InitGame(new ClientsMap<(IPEndPoint, string), Player>(), new HashSet<IPEndPoint>());

            UDPClient = new UdpClient(localPort);
            UDPClient.DontFragment = true;
            UDPClient.BeginReceive(UDPRecieveCallback, null);

            while (true)
            {
                if (players.Count >= 2)
                {
                    if (untilStart > 0)
                        untilStart -= Utils.FrameTime;
                }
                else
                    untilStart = START_SLEEP;

                float sleepTime = (1f / FPS - Utils.FrameTime) * 1000;
                if(sleepTime > 10)
                    System.Threading.Thread.Sleep((int)sleepTime);
                Update();
            }

        }
        public static void InitGame(ClientsMap<(IPEndPoint, string), Player> prevClients, HashSet<IPEndPoint> spectators)
        {
            untilStart = START_SLEEP;
            players = prevClients;
            Program.spectators = spectators;
            playersPackets = new Dictionary<IPEndPoint, Queue<(uint, byte[])>>();
            if (spectators.Count > 0)
            {
                foreach(var endPoint in spectators)
                {
                    Task.Run(() => Disconnect(endPoint));
                }
            }
            spectators = new HashSet<IPEndPoint>();

            board = new Board();
            board.FoodFillBoard();
            if (players.Count > 0)
                foreach (var p in players)
                {
                    Task.Run(() => Disconnect(p.Key.Item1));
                }
            
            players = new ClientsMap<(IPEndPoint, string), Player>();
        }
        public static Player SpawnPlayer()
        {
            return board.SpawnPlayer();
        }
        public static void RemovePlayer(Player player)
        {
            board.RemovePlayer(player);
            var key = players.Reverse[player];
            players.Remove(key);
            leaderBoard.Add((player.mass, key.Item2));

            spectators.Add(key.Item1);
            Task.Run(() => { SendSpec(players.Reverse[board.leadingPlayer].Item2, key.Item1); });
        }
        public static void Disconnect(IPEndPoint endPoint)
        {
            spectators.Remove(endPoint);
            players.RemoveByLambda(key => key.Item1 == endPoint);
            playersPackets.Remove(endPoint);
        }
        public static void Update()
        {
            if (players.Count > 1 && untilStart <= 0)
            {
                List<Task> tasks = new List<Task>();
                var oldLeader = board.leadingPlayer;
                var newStates = board.UpdateBoard(Utils.FrameTime);
                foreach (var state in newStates)
                {
                    tasks.Add(Task.Run(() => SendData(state)));
                }
                Task.WaitAll(tasks.ToArray()); 
                foreach (Player p in board.frameGonePlayers)
                    queuedDicts.Enqueue((ActionApplied.REMOVE, new dynamic[] { p }));

                if (oldLeader != board.leadingPlayer)
                {
                    foreach (var spec in spectators)
                    {
                        Task.Run(() => { SendSpec(players.Reverse[board.leadingPlayer].Item2, spec); });
                    }
                }

                SyncDict();
            }
            
            if (untilStart <= 0 && players.Count <= 1)
            {
                leaderBoard.Add((board.leadingPlayer.mass, players.Reverse[board.leadingPlayer].Item2));
                leaderBoard.Sort();

                foreach (var spec in spectators)
                {
                    Task.Run(() => SendLeaderBoard(spec));
                }
                Task.Run(() => SendLeaderBoard(players.Reverse[board.leadingPlayer].Item1)).Wait();
                InitGame(players, spectators);
            }
            Utils.UpdateTime();
        }

        public static void SyncDict()
        {
            var itemsN = queuedDicts.Count;
            while (itemsN >= 0)
            {
                itemsN--;
                (ActionApplied, dynamic[]) listOfActions;
                while (!queuedDicts.TryDequeue(out listOfActions)) { };

                if (listOfActions.Item1 == ActionApplied.REMOVE)
                {
                    Player player = listOfActions.Item2[0];
                    if (players.Reverse.ContainsKey(player))
                        RemovePlayer(player);
                }
                else if (listOfActions.Item1 == ActionApplied.UPDATE)
                {
                    Player player = listOfActions.Item2[0];
                    (float, float) moveVec = listOfActions.Item2[1];

                    if (players.Reverse.ContainsKey(player))
                        player.moveVec = moveVec;
                }
            }
        }
        public static void SendData((Player, NewState) state)
        {
            var User = players.Reverse[state.Item1];
            var clients = new HashSet<IPEndPoint>();
            clients.Add(User.Item1);
            if(state.Item1 == board.leadingPlayer)
                clients.UnionWith(spectators);

            state.Item2.goneEntities.UnionWith(state.Item2.loadedEntities.Intersect(board.frameGoneEntities));
            state.Item2.goneEntities.UnionWith(state.Item2.loadedEntities.Intersect(board.frameGonePlayers));
            state.Item2.newEntities.Add(state.Item2.loadedEntities.Intersect(board.players).ToHashSet());

            SendAppear(state.Item2.newEntities, clients);
            SendUpdate(clients);
            SendDisappear(state.Item2.goneEntities, clients);
        }
        public static void SendUpdate(HashSet<IPEndPoint> endPoints)
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
                bytes.AddRange(Encoding.ASCII.GetBytes(name));
            }
            var arr = bytes.ToArray();
            foreach (var endPoint in endPoints)
                UDPClient.BeginSend(arr, arr.Length, endPoint, UDPSendCallback, arr);
            
        }
        public static void SendDisappear(HashSet<Cell> entities, HashSet<IPEndPoint> endPoints)
        {
            if (entities.Count() <= 0)
                return;

            const int SplitAmount = 50;
            int n = entities.Count() / SplitAmount + 1;
            var packetIds = new uint[n];

            var packets = new List<byte>[n];

            for (int i = 0; i < n; i++)
            {
                packets[i] = new List<byte>() { 101 };
                packetIds[i] = PacketId;
                packets[i].AddRange(BitConverter.GetBytes(packetIds[i]));
                int count = i == n - 1 ? entities.Count() % SplitAmount : SplitAmount;
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
                packets[packetN].AddRange(Encoding.ASCII.GetBytes(name));

                entityN++;
                if (entityN >= SplitAmount)
                {
                    packetN++;
                    entityN = 0;
                }
            }
            foreach (var endPoint in endPoints)
            {
                for (int i = 0; i < packets.Length; i++)
                {
                    var arr = packets[i].ToArray();

                    UDPClient.BeginSend(arr, arr.Length, endPoint, UDPSendCallback, arr);
                    playersPackets[endPoint].Enqueue((packetIds[i], arr));
                }
            }
        }
        public static void SendLeaderBoard(IPEndPoint endPoint)
        {
            var bytes = new List<byte>() { 254 };
            var packetId = PacketId;
            bytes.AddRange(BitConverter.GetBytes(packetId));
            foreach (var l in leaderBoard)
            {
                bytes.AddRange(BitConverter.GetBytes((ushort)l.Item1));
                bytes.Add((byte)l.Item2.Length);
                bytes.AddRange(Encoding.ASCII.GetBytes(l.Item2));
            }

            var arr = bytes.ToArray();
            UDPClient.BeginSend(arr, arr.Length, endPoint, UDPSendCallback, arr);
            playersPackets[endPoint].Enqueue((packetId, arr));
        }
        public static void SendAppear(List<HashSet<Cell>> listEntities, HashSet<IPEndPoint> endPoints)
        {
            if (listEntities.Count() <= 0 || listEntities[0].Count() <= 0)
                return;

            const int SplitAmount = 50;

            int c = 0;
            foreach (var list in listEntities)
                c += list.Count;

            int n = c / SplitAmount + 1;
            var packetIds = new uint[n];

            var packets = new List<byte>[n];

            for (int i = 0; i < n; i++)
            {
                packets[i] = new List<byte>() { 100 };
                packetIds[i] = PacketId;
                packets[i].AddRange(BitConverter.GetBytes(packetIds[i]));
                int count = i == n - 1 ? c % SplitAmount : SplitAmount;
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
                    packets[packetN].AddRange(Encoding.ASCII.GetBytes(name));

                    entityN++;
                    if (entityN >= SplitAmount)
                    {
                        packetN++;
                        entityN = 0;
                    }
                }
            foreach (var endPoint in endPoints)
            {
                for (int i = 0; i < packets.Length; i++)
                {
                    var arr = packets[i].ToArray();
                    UDPClient.BeginSend(arr, arr.Length, endPoint, UDPSendCallback, arr);
                    playersPackets[endPoint].Enqueue((packetIds[i], arr));
                }
            } 
        }
        public static void SendConnect(string newName, IPEndPoint endPoint)
        {
            var packetId = PacketId;
            var bytes = new List<byte>() { 254 };
            bytes.AddRange(BitConverter.GetBytes(packetId));
            bytes.Add((byte)newName.Length);
            bytes.AddRange(Encoding.ASCII.GetBytes(newName));

            var arr = bytes.ToArray();
            UDPClient.BeginSend(arr, arr.Length, endPoint, UDPSendCallback, arr);
            playersPackets[endPoint].Enqueue((packetId, arr));
        }
        public static void SendSpec(string name, IPEndPoint endPoint)
        {
            var packetId = PacketId;
            var bytes = new List<byte>() { 240 };
            bytes.AddRange(BitConverter.GetBytes(packetId));
            bytes.Add((byte)name.Length);
            bytes.AddRange(Encoding.ASCII.GetBytes(name));

            var arr = bytes.ToArray();
            UDPClient.BeginSend(arr, arr.Length, endPoint, UDPSendCallback, arr);
            playersPackets[endPoint].Enqueue((packetId, arr));
        }
        public static void UDPSendCallback(IAsyncResult result)
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
            Console.WriteLine(clientEndPoint + " " + data[0]);

            Player player = null;
            string name = "";
            uint packetId = uint.MaxValue;
            try
            {
                switch (data[0])
                {
                    case 102:
                        name = Encoding.ASCII.GetString(data, 2, data[1]);
                        var offset = 2 + data[1];
                        (float, float) moveVec = (
                            BitConverter.ToSingle(data, offset),
                            BitConverter.ToSingle(data, offset + sizeof(float)));

                        queuedDicts.Enqueue((ActionApplied.UPDATE, 
                            new dynamic[] { players.Forward[(clientEndPoint, name)], moveVec }));

                        Console.WriteLine($"Updating {name} ip - {clientEndPoint}");
                        break;
                    case 200:
                        name = Encoding.ASCII.GetString(data, 2, data[1]);
                        packetId = BitConverter.ToUInt32(data, 2 + data[1]);
                        break;
                    case 254:
                        if (untilStart <= 0)
                        {
                            spectators.Add(clientEndPoint);
                            playersPackets.Add(clientEndPoint, new Queue<(uint, byte[])>());

                            Task.Run(() => SendSpec(players.Reverse[board.leadingPlayer].Item2, clientEndPoint));
                            break;
                        }

                        player = players.Forward.FindByLambda(key => 
                            key.Item1 == clientEndPoint
                        ).Item2;
                        
                        if (player == null)
                        {
                            name = Encoding.ASCII.GetString(data, 2, data[1]);
                            player = SpawnPlayer();
                            name += "#" + player.Id;
                            players.Add((clientEndPoint, name), player);
                            playersPackets.Add(clientEndPoint, new Queue<(uint, byte[])>());
                        }
                        else
                            name = players.Reverse[player].Item2;

                        Task.Run(() => SendConnect(name, clientEndPoint));

                        Console.WriteLine($"Connected {name} ip - {clientEndPoint}");
                        break;

                    case 255:
                        name = Encoding.ASCII.GetString(data, 2, data[1]);
                        if (players.Forward.ContainsKey((clientEndPoint, name)))
                            queuedDicts.Enqueue((ActionApplied.REMOVE, 
                                new dynamic[] { players.Forward[(clientEndPoint, name)] }));
                        Disconnect(clientEndPoint);

                        Console.WriteLine($"Disconnected {name} ip - {clientEndPoint}");
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
            if (packetId >= uint.MaxValue || data[0] != 200)
                return;
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
                    Task.Run(() => SendAppear(new List<HashSet<Cell>>() { player.getLoadedArea() }, 
                        new HashSet<IPEndPoint>() { clientEndPoint }));
                }
                else if (lostPacket.Item2[0] == 240)
                {
                    Task.Run(() => SendAppear(new List<HashSet<Cell>>() { board.leadingPlayer.getLoadedArea() },
                        new HashSet<IPEndPoint>() { clientEndPoint }));
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