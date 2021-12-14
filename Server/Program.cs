using System;
using System.Collections.Generic;
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
        static UdpClient UDPListener;

        static void Main(string[] args)
        {
            UDPListener = new UdpClient(localPort);
            initGame();
            UDPListener.BeginReceive(UDPRecieveCallback, null);
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
            var newStates = board.updateBoard(Utils.FrameTime);
        }

        public static void UDPRecieveCallback(IAsyncResult result)
        {
            IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = UDPListener.EndReceive(result, ref clientEndPoint);

            UDPListener.BeginReceive(UDPRecieveCallback, null);

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