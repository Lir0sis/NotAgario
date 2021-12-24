using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Client
{
    static class NetCode
    {
        static UdpClient client;
        static (Cell, string)? player = null;
        public static bool isConnected { get => player != null; }
        public static bool isReadToPlay { get; private set; }
        public static bool isInitialized { get => client != null; }

        public static void Connect(string IpAddress, int port, string username)
        {
            if (!isInitialized)
            {
                client = new UdpClient(IpAddress, port);
                client.BeginReceive(UDPRecieveCallback, null);
            }
            SendConnect(username);
        }

        public static void Disconnet()
        {
            SendDisconnect();
            client.Close();
            client = null;
            player = null;
            isReadToPlay = false;
        }

        public static void Update((int,int) mousePos)
        {
            if (isConnected)
                SendMoveVec(mousePos);
        }
        static void UDPSendCallback(IAsyncResult result)
        {
            client.EndSend(result);
            client.BeginReceive(UDPRecieveCallback, null);
        }

        static void UDPRecieveCallback(IAsyncResult result)
        {
            IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = client.EndReceive(result, ref clientEndPoint);

            client.BeginReceive(UDPRecieveCallback, null);

            var stream = new MemoryStream(data);
            var reader = new BinaryReader(stream);
            try
            {
                switch (reader.ReadByte())
                {
                    case 100:
                        for (int i = 0; i < reader.ReadUInt16(); i++)
                        {
                            int x = reader.ReadUInt16();
                            int y = reader.ReadUInt16();
                            int radius = reader.ReadUInt16();
                            string name_id = reader.ReadString();
                            Console.WriteLine(name_id);
                            var cell = new Cell(int.Parse(name_id.Split('#')[1]), name_id.Split('#')[0], (x, y), radius);
                            if (i == 0 && player == null)
                            {
                                player = (cell, name_id);
                            }
                            Program.cells.Add(name_id, cell);
                        }
                        if (!isReadToPlay)
                            isReadToPlay = true;
                        break;

                    case 101:
                        if (!isReadToPlay)
                            break;
                        for (int i = 0; i < reader.ReadUInt16(); i++)
                        {
                            string name_id = reader.ReadString();
                            Program.cells.Remove(name_id);
                        }
                        break;

                    case 102:
                        if (!isReadToPlay)
                            break;
                        for (int i = 0; i < reader.ReadUInt16(); i++)
                        {
                            int x = reader.ReadUInt16();
                            int y = reader.ReadUInt16();
                            int radius = reader.ReadUInt16();
                            string name_id = reader.ReadString();
                            Program.cells[name_id].Update((x, y), radius);
                        }
                        break;
                }
            }
            catch (Exception error)
            {
                Console.WriteLine($"-- UDPRecieveCallback Error --\n" +
                    $"{error.StackTrace}" +
                    $"\n{error.Message}");
            }
        }

        static void SendConnect(string Username)
        {
            var name = Encoding.ASCII.GetBytes(Username);
            List<byte> bytes = new List<byte>();
            bytes.Add(254);
            bytes.Add((byte)name.Length);
            bytes.AddRange(name);

            var array = bytes.ToArray();
            client.BeginSend(array, array.Length, UDPSendCallback, null);
        }

        static void SendDisconnect()
        {
            var name = Encoding.ASCII.GetBytes(player.Value.Item2);
            List<byte> bytes = new List<byte>();
            bytes.Add(255);
            bytes.Add((byte)name.Length);
            bytes.AddRange(name);

            var array = bytes.ToArray();
            client.BeginSend(array, array.Length, UDPSendCallback, null);
        }

        static void SendMoveVec((int, int) mousePos)
        {
            var name = Encoding.ASCII.GetBytes(player.Value.Item2);
            List<byte> bytes = new List<byte>();
            bytes.Add(102);
            bytes.Add((byte)name.Length);
            bytes.AddRange(name);

            float dx = -480 / 2 + mousePos.Item1;
            float dy = -480 / 2 + mousePos.Item2;

            float length = (float)Math.Sqrt(Math.Pow(dx, 2) + Math.Pow(dy, 2));
            if (length <= 1)
                length = 1;
            bytes.AddRange(BitConverter.GetBytes(dx / length));
            bytes.AddRange(BitConverter.GetBytes(dy / length));

            var array = bytes.ToArray();
            client.BeginSend(array, array.Length, UDPSendCallback, null);
        }

    }
}
