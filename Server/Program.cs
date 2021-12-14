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
            List<byte> bytes = null;

            state.Item2.goneEntities.UnionWith(state.Item2.loadedEntities.Intersect(board.goneEntities));
            if (state.Item2.newEntities.Count() > 0)
            {
                bytes = new List<byte>() { 100 };
                bytes.AddRange(BitConverter.GetBytes((ushort)state.Item2.newEntities.Count()));

                foreach (var entities in state.Item2.newEntities)
                    foreach (var entity in entities)
                    {
                        bytes.AddRange(BitConverter.GetBytes((ushort)Math.Round(entity.center.Item1)));
                        bytes.AddRange(BitConverter.GetBytes((ushort)Math.Round(entity.center.Item2)));
                        bytes.AddRange(BitConverter.GetBytes((ushort)entity.mass));
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
                UDPClient.BeginSend(bytes.ToArray(), bytes.Count(), User.Item1, UDPSendCallback, null);
            }

            if (state.Item2.goneEntities.Count() > 0)
            {
                bytes = new List<byte>() { 101 };
                bytes.AddRange(BitConverter.GetBytes((ushort)state.Item2.goneEntities.Count()));

                foreach (var entity in state.Item2.goneEntities)
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
                UDPClient.BeginSend(bytes.ToArray(), bytes.Count(), User.Item1, UDPSendCallback, null);
            }

            if (board.players.Count() > 0)
            {
                var player = state.Item1;
                bytes = new List<byte>() { 102 };
                bytes.AddRange(BitConverter.GetBytes((ushort)board.players.Count()));

                bytes.AddRange(BitConverter.GetBytes((ushort)Math.Round(player.center.Item1)));
                bytes.AddRange(BitConverter.GetBytes((ushort)Math.Round(player.center.Item2)));
                bytes.AddRange(BitConverter.GetBytes((ushort)player.mass));
                int nameLentgh = 0;
                byte[] nameBytes = null;
            
                var name = players.Reverse[player].Item2;
                nameLentgh = name.Length;
                nameBytes = ascii.GetBytes(name);
                
                bytes.Add((byte)nameLentgh);
                bytes.AddRange(nameBytes);

                foreach (var p in board.players)
                {
                    if (player == state.Item1)
                        continue;

                    bytes.AddRange(BitConverter.GetBytes((ushort)Math.Round(p.center.Item1)));
                    bytes.AddRange(BitConverter.GetBytes((ushort)Math.Round(p.center.Item2)));
                    bytes.AddRange(BitConverter.GetBytes((ushort)p.mass));

                    name = players.Reverse[p].Item2;
                    nameLentgh = name.Length;
                    nameBytes = ascii.GetBytes(name);


                    bytes.Add((byte)nameLentgh);
                    bytes.AddRange(nameBytes);
                }

            }
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
                        players.Add((clientEndPoint, 
                            ascii.GetString(new ArraySegment<byte>(data, 2, 2 + data[1] - 1).Array)),
                            spawnPlayer());
                        break;

                    case 255:
                        name = ascii.GetString(new ArraySegment<byte>(data, 2, 2 + data[1] - 1).Array);
                        player = players.Forward[(clientEndPoint, name)];
                        removePlayer(player);
                        players.Remove(player);
                        break;

                    case 102:
                        name = ascii.GetString(new ArraySegment<byte>(data, 2, 2 + data[1] - 1).Array);
                        player = players.Forward[(clientEndPoint, name)];
                        var offset = 2 + data[1] - 1;
                        (float, float) moveVec = (
                            BitConverter.ToSingle(data, offset),
                            BitConverter.ToSingle(data, offset + sizeof(float))
                            );
                        player.moveVec = moveVec;
                        break;
                }   
            }
            catch (Exception error)
            {
                Console.WriteLine("-- UDPRecieveCallback Error --");
                Console.WriteLine(error.Message);
            }

        }

    }
}