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
            REMOVE_PLAYER,
            REMOVE_CLIENT,
            REMOVE_SPECTATOR,
            UPDATE
        }

        static Board board;

        static List<(int, string)> leaderBoard = new List<(int, string)>();

        static ClientsMap<(IPEndPoint, string), Player> players;
        static HashSet<IPEndPoint> spectators;
        static Dictionary<IPEndPoint, Queue<(uint, byte[])>> playersPackets = 
            new Dictionary<IPEndPoint, Queue<(uint, byte[])>>();

        static ConcurrentQueue<(ActionApplied, dynamic[])> queuedDicts =
            new ConcurrentQueue<(ActionApplied, dynamic[])>();

        static uint _packetId = 0;
        static uint PacketId { get => _packetId++; }

        static UdpClient UDPClient;

        static readonly int POSTGAME_TIME = 5;

        public static int ServerSleepTime { get; private set; }
        static float untilStart;
        static float untilEnd;
        static readonly float FPS = 30;
        public static int BoardSize { get; private set; }               
        public static int MinPlayers { get; private set; }

        static void Main(string[] args)                                         
        { 
            int port = int.Parse(args[0]);
            BoardSize = int.Parse(args[1]);
            ServerSleepTime = int.Parse(args[2]);
            MinPlayers = int.Parse(args[3]);

            InitGame(new ClientsMap<(IPEndPoint, string), Player>(), new HashSet<IPEndPoint>());

            UDPClient = new UdpClient(port);
            UDPClient.DontFragment = true;
            UDPClient.BeginReceive(UDPRecieveCallback, null);

            while (true)
            {
                if (players.Count >= MinPlayers)
                {
                    if (untilStart > 0)
                        untilStart -= Utils.FrameTime;
                }
                else if (players.Count == 1 && untilStart <= 0 && untilEnd > 0)
                {
                    untilEnd -= Utils.FrameTime;
                }
                else if (players.Count < MinPlayers && untilStart < ServerSleepTime && untilEnd == POSTGAME_TIME)
                    untilStart = ServerSleepTime;

                float sleepTime = (1f / FPS - Utils.FrameTime) * 1000;
                if(sleepTime > 7)
                    System.Threading.Thread.Sleep((int)sleepTime);
                Update();
            }

        }
        public static void InitGame(ClientsMap<(IPEndPoint, string), Player> prevClients, HashSet<IPEndPoint> spectators)
        {
            untilStart = ServerSleepTime;
            untilEnd = POSTGAME_TIME;
            players = prevClients;
            Program.spectators = spectators;
            if (spectators.Count > 0)
            {
                foreach(var endPoint in spectators)
                {
                    Task.Run(() => SendDisconnect(endPoint));
                }
            }
            spectators = new HashSet<IPEndPoint>();

            board = new Board(BoardSize);
            board.FoodFillBoard();
            if (players.Count > 0)
                foreach (var p in players)
                {
                    Task.Run(() => SendDisconnect(p.Key.Item1));
                }
            
            players = new ClientsMap<(IPEndPoint, string), Player>();
            leaderBoard = new List<(int, string)>();
        }
        public static Player SpawnPlayer()
        {
            return board.SpawnPlayer();
        }
        public static void RemovePlayer(Player player)
        {
            //board.RemovePlayer(player);
            var key = players.Reverse[player];
            
            if (untilStart <= 0)
            {
                leaderBoard.Add((player.mass, key.Item2));

                spectators.Add(key.Item1);
                Task.Run(() => { SendSpec(players.Reverse[board.leadingPlayer].Item2, key.Item1); });
            }
        }
        public static void SendDisconnect(IPEndPoint endPoint)
        {
            var key = players.Forward.FindByLambda(key => key.Item1 == endPoint).Item1;
            if (spectators.Contains(endPoint))
                queuedDicts.Enqueue((ActionApplied.REMOVE_SPECTATOR, new dynamic[] { endPoint }));
            else if (key != (null, null))
                queuedDicts.Enqueue((ActionApplied.REMOVE_PLAYER,
                    new dynamic[] { players.Forward[key] }));
            else return;

            var bytes = new List<byte>() { 255 };
            var packetId = PacketId;
            bytes.AddRange(BitConverter.GetBytes(packetId));

            var arr = bytes.ToArray();
            UDPClient.BeginSend(arr, arr.Length, endPoint, UDPSendCallback, arr);
            playersPackets[endPoint].Enqueue((packetId, arr));
        }
        public static void Update()
        {
            if (untilStart <= 0)
            {
                List<Task> tasks = new List<Task>();
                var oldLeader = board.leadingPlayer;
                var newStates = board.UpdateBoard(Utils.FrameTime);

                foreach (Player p in board.frameGonePlayers)
                    RemovePlayer(p);

                foreach (var state in newStates)
                {
                    if(!board.frameGonePlayers.Contains(state.Item1))
                        tasks.Add(Task.Run(() => SendData(state)));
                }
                Task.WaitAll(tasks.ToArray()); 

                SyncDict();
                
                if (oldLeader != board.leadingPlayer && board.leadingPlayer != null)
                {
                    var leadingPlayer = board.leadingPlayer;
                    foreach (var spec in spectators)
                    {
                        Task.Run(() => { SendSpec(players.Reverse[leadingPlayer].Item2, spec); });
                    }
                }
            }
            
            if (untilStart <= 0 && players.Count < MinPlayers)
            {
                if (board.leadingPlayer != null && untilEnd == POSTGAME_TIME)
                {
                    leaderBoard.Add((board.leadingPlayer.mass, players.Reverse[board.leadingPlayer].Item2));
                    leaderBoard.Sort();

                    foreach (var spec in spectators)
                        Task.Run(() => SendLeaderBoard(spec));

                    Task.Run(() => SendLeaderBoard(players.Reverse[board.leadingPlayer].Item1)).Wait();
                    board.leadingPlayer = null;
                }
                if( untilEnd <= 0)
                    InitGame(players, spectators);
                
            }
            foreach(var p in playersPackets)
            {
                if (p.Value.Count <= 0)
                    continue;
                var packet = p.Value.Peek();
                var bytes = packet.Item2;
                if (bytes == null)
                    continue;
                UDPClient.BeginSend(bytes, bytes.Length, p.Key, UDPSendCallback, bytes);
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

                if (listOfActions.Item1 == ActionApplied.REMOVE_PLAYER)
                {
                    Player player = listOfActions.Item2[0];
                    if (players.Reverse.ContainsKey(player))
                        RemovePlayer(player);
                }
                else if (listOfActions.Item1 == ActionApplied.REMOVE_SPECTATOR)
                {
                    var endPoint = listOfActions.Item2[0];
                    if (spectators.Contains(endPoint))
                        spectators.Remove(endPoint);
                }
                else if (listOfActions.Item1 == ActionApplied.UPDATE)
                {
                    Player player = listOfActions.Item2[0];
                    (float, float) moveVec = listOfActions.Item2[1];

                    if (players.Reverse.ContainsKey(player))
                        player.moveVec = moveVec;
                }
                else if (listOfActions.Item1 == ActionApplied.REMOVE_CLIENT)
                {
                    ((IPEndPoint, string), Player) record = listOfActions.Item2[0];
                    //(float, float) moveVec = listOfActions.Item2[1];

                    if (players.Forward.ContainsKey(record.Item1))
                        players.Remove(record.Item1);
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

            var updatedLoadedEntities = new HashSet<Cell>();
            foreach (var s in state.Item2.loadedEntitiesSectors)
                updatedLoadedEntities.UnionWith(s);

            state.Item2.newEntities.Add(updatedLoadedEntities.Except(state.Item2.loadedEntities).ToHashSet());
            var goneEntities = (state.Item2.loadedEntities.Except(updatedLoadedEntities)).ToHashSet();

            SendAppear(state.Item2.newEntities, clients);
            SendUpdate(clients);
            SendDisappear(goneEntities, clients);
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
            var bytes = new List<byte>() { 210 };
            var packetId = PacketId;
            bytes.AddRange(BitConverter.GetBytes(packetId));
            bytes.AddRange(BitConverter.GetBytes((ushort)leaderBoard.Count));
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
            //Console.WriteLine(clientEndPoint + " " + data[0]);

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
                        if (untilStart <= 0 && players.Count >= MinPlayers)
                        {
                            spectators.Add(clientEndPoint);
                            playersPackets.Add(clientEndPoint, new Queue<(uint, byte[])>());

                            Task.Run(() => SendSpec(players.Reverse[board.leadingPlayer].Item2, clientEndPoint));
                            break;
                        }

                        player = players.Forward.FindByLambda(key =>
                            key.Item1.Equals(clientEndPoint)
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

                        SendDisconnect(clientEndPoint);
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
                    //player = players.Forward[(clientEndPoint, name)];

                    foreach (var p in players)
                    {
                        player = p.Value;
                        Task.Run(() => SendAppear(new List<HashSet<Cell>>() { player.getLoadedEntities() },
                            new HashSet<IPEndPoint>() { p.Key.Item1 }));
                    }
                }
                else if (lostPacket.Item2[0] == 240)
                {
                    if (board.leadingPlayer == null)
                        return;

                    var appearList = new List<HashSet<Cell>>() { board.leadingPlayer.getLoadedEntities() };
                    var endpoints = new HashSet<IPEndPoint>() { clientEndPoint };
                    Task.Run(() => SendAppear(appearList,
                        endpoints));

                    var record = players.Forward.FindByLambda(key => 
                         key.Item1.Equals(clientEndPoint)
                    );
                    queuedDicts.Enqueue((ActionApplied.REMOVE_CLIENT, 
                        new dynamic[] { record }));
                }
            }
            else
            {
                var bytes = lostPacket.Item2;
                if (bytes == null)
                    return;
                UDPClient.BeginSend(bytes, bytes.Length, clientEndPoint, UDPSendCallback, bytes);
            }
        }

    }
}