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
        static class Game
        {
            static Board board;

            public static void InitGame()
            {
                board = new Board();
            }
        }

        static Map<IPEndPoint, Player> map = new Map<IPEndPoint, Player>();

        static int localPort = 7777;
        static UdpClient UDPListener;

        static List<IPEndPoint> clients = new List<IPEndPoint>();

        static void Main(string[] args)
        {
            
            UDPListener = new UdpClient(localPort);
            Game.InitGame();
            UDPListener.BeginReceive(UDPRecieveCallback, null);
            while (true) { 
            }

        }

        public static void UDPRecieveCallback(IAsyncResult result)
        {
            IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = UDPListener.EndReceive(result, ref clientEndPoint);

            UDPListener.BeginReceive(UDPRecieveCallback, null);

            try
            {
                switch (data[0]) {
                    case 254:
                        break;
                }
            }
            catch
            {
                Console.WriteLine("error occurred");
            }

        }

    }
}