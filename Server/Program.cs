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
 
        static ASCIIEncoding ascii = new ASCIIEncoding();

        static int localPort = 7777;
        static UdpClient UDPClient;

        static void Main(string[] args)
        {
            UDPClient = new UdpClient(localPort);
            initGame();
            UDPClient.BeginReceive(UDPRecieveCallback, null);
            while (true) 
            {
                Update();
                System.Threading.Thread.Sleep(5);
            }

        }
        public static void initGame()
        {
            players = new Map<(IPEndPoint, string), Player>();
            board = new Board();
            board.foodFillBoard();
        }
        public static Player spawnPlayer()
        {
            return board.spawnPlayer();
        }
        public static void removePlayer(Player player)
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

            SendAppear(state.Item2.newEntities, User.Item1);
            SendDisappear(state.Item2.goneEntities, User.Item1);
            SendUpdate(players.Forward[User], User.Item1);
        }

        public static void SendUpdate(Player player, IPEndPoint endpoint)
        {
            if (board.players.Count() <= 0)
                return;

            var bytes = new List<byte>() { 102 };
            bytes.AddRange(BitConverter.GetBytes((ushort)board.players.Count()));

            bytes.AddRange(BitConverter.GetBytes((ushort)Math.Round(player.center.Item1)));
            bytes.AddRange(BitConverter.GetBytes((ushort)Math.Round(player.center.Item2)));
            bytes.AddRange(BitConverter.GetBytes((ushort)player.radius));

            var name = players.Reverse[player].Item2;
            int nameLentgh = name.Length;
            byte[] nameBytes = ascii.GetBytes(name);

            bytes.Add((byte)nameLentgh);
            bytes.AddRange(nameBytes);

            foreach (var p in board.players)
            {
                if (p == player)
                    continue;

                bytes.AddRange(BitConverter.GetBytes((ushort)Math.Round(p.center.Item1)));
                bytes.AddRange(BitConverter.GetBytes((ushort)Math.Round(p.center.Item2)));
                bytes.AddRange(BitConverter.GetBytes((ushort)p.radius));

                name = players.Reverse[p].Item2;
                nameLentgh = name.Length;
                nameBytes = ascii.GetBytes(name);


                bytes.Add((byte)nameLentgh);
                bytes.AddRange(nameBytes);
            }

            UDPClient.BeginSend(bytes.ToArray(), bytes.Count(), endpoint, UDPSendCallback, null);
        }
        public static void SendDisappear(HashSet<Cell> entities, IPEndPoint endPoint)
        {
            if (entities.Count() <= 0)
                return;
            var bytes = new List<byte>() { 101 };
            bytes.AddRange(BitConverter.GetBytes((ushort)entities.Count()));

            foreach (var entity in entities)
            {
                int nameLentgh = 0;
                byte[] nameBytes = null;
                if (typeof(Player).IsInstanceOfType(entity))
                {
                    var name = players.Reverse[(Player)entity].Item2;
                    nameLentgh = name.Length;
                    nameBytes = ascii.GetBytes(name);
                }
                else if (typeof(Food).IsInstanceOfType(entity))
                {
                    var name = $"#{((Food)entity).Id}";
                    nameLentgh = name.Length;
                    nameBytes = ascii.GetBytes(name);
                }
                bytes.Add((byte)nameLentgh);
                bytes.AddRange(nameBytes);
            }
            UDPClient.BeginSend(bytes.ToArray(), bytes.Count(), endPoint, UDPSendCallback, null);
        }
        public static void SendAppear(List<HashSet<Cell>> listEntities, IPEndPoint endpoint)
        {
            if (listEntities.Count() <= 0)
                return;

            var bytes = new List<byte>() { 100 };
            bytes.AddRange(BitConverter.GetBytes((ushort)listEntities.Count()));

            foreach (var entities in listEntities)
                foreach (var entity in entities)
                {
                    bytes.AddRange(BitConverter.GetBytes((ushort)Math.Round(entity.center.Item1)));
                    bytes.AddRange(BitConverter.GetBytes((ushort)Math.Round(entity.center.Item2)));
                    bytes.AddRange(BitConverter.GetBytes((ushort)entity.radius));
                    int nameLentgh = 0;
                    byte[] nameBytes = null;
                    if (typeof(Player).IsInstanceOfType(entity))
                    {
                        var name = players.Reverse[(Player)entity].Item2;
                        nameLentgh = name.Length;
                        nameBytes = ascii.GetBytes(name);
                    }
                    else if (typeof(Food).IsInstanceOfType(entity))
                    {
                        var name = $"#{((Food)entity).Id}";
                        nameLentgh = name.Length;
                        nameBytes = ascii.GetBytes(name);
                    }
                    bytes.Add((byte)nameLentgh);
                    bytes.AddRange(nameBytes);
                }
            UDPClient.BeginSend(bytes.ToArray(), bytes.Count(), endpoint, UDPSendCallback, null);

        }

        static void UDPSendCallback(IAsyncResult result)
        {
            // UdpClient end = (UdpClient)result.AsyncState;
            //Console.WriteLine($"number of bytes sent: {client.EndSend(result)}");
            UDPClient.EndSend(result);
        }

        public static void UDPRecieveCallback(IAsyncResult result)
        {
            IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = UDPClient.EndReceive(result, ref clientEndPoint);

            UDPClient.BeginReceive(UDPRecieveCallback, null);

            try
            {
                Player player;
                string name;
                switch (data[0]) {
                    case 254:
                        var newName = ascii.GetString(data, 2, data[1]);
                        var newPlayer = spawnPlayer();
                        players.Add((clientEndPoint, newName),
                            newPlayer);
                        Task.Run(() => SendAppear(new List<HashSet<Cell>>() { newPlayer.getLoadedArea() }, clientEndPoint));
                        Console.WriteLine($"Connected {newName} ip - {clientEndPoint}");
                        break;

                    case 255:
                        name = ascii.GetString(data, 2, data[1]);
                        player = players.Forward[(clientEndPoint, name)];
                        removePlayer(player);
                        players.Remove(player);

                        Console.WriteLine($"Connected {name} ip - {clientEndPoint}");
                        break;

                    case 102:
                        name = ascii.GetString(data, 2, data[1]);
                        player = players.Forward[(clientEndPoint, name)];
                        var offset = 2 + data[1] - 1;
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
                Console.WriteLine($"-- UDPRecieveCallback Error --\n{error.StackTrace}\n{error.Message}\n");
            }

        }

    }
}