using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Client
{
    static class NetCode
    {
        static UdpClient UDPClient;

        static (Cell, string) player = (null, "");


        //static Queue<(uint, byte[])> noResponsePackets = new Queue<(uint, byte[])>();

        //static uint _packetId = 0;
        //static uint PacketId { get => _packetId++; }

        public static (int, int) playerCenter { get => player.Item1.center; }
        public static int playerRadius { get => player.Item1.radius; }

        public static bool isConnected { get => player.Item2 != ""; }
        public static bool isReadyToPlay { get => player.Item1 != null; }
        public static bool isInitialized { get => UDPClient != null; }

        public static void Connect(string IpAddress, int port, string username)
        {
            if (!isInitialized)
            {
                UDPClient = new UdpClient(IpAddress, port);
                UDPClient.DontFragment = true;
                UDPClient.BeginReceive(UDPRecieveCallback, null);
            }
            SendConnect(username);
        }

        public static void Disconnet()
        {
            if (isConnected)
            {
                SendDisconnect();
                UDPClient.Close();
            }

            player = (null, "");
        }

        public static void GetInitData()
        {
            var name = Encoding.ASCII.GetBytes(player.Item2);
            //var packetId = PacketId;
            List<byte> bytes = new List<byte>();
            bytes.Add(253);
            //bytes.AddRange(BitConverter.GetBytes(packetId));
            bytes.Add((byte)name.Length);
            bytes.AddRange(name);

            var array = bytes.ToArray();
            UDPClient.BeginSend(array, array.Length, UDPSendCallback, null);
        }

        public static void Update((int, int) mousePos)
        {
            if (isConnected)
            {
                Task.Run(() =>
                    SendMoveVec(mousePos)
                );
            }
        }
        static void UDPSendCallback(IAsyncResult result)
        {
            UDPClient.EndSend(result);
            UDPClient.BeginReceive(UDPRecieveCallback, null);
        }

        static void UDPRecieveCallback(IAsyncResult result)
        {
            IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = new byte[0];
            try
            {
                data = UDPClient.EndReceive(result, ref clientEndPoint);
            }
            catch(SocketException e)
            {
                if (e.SocketErrorCode == SocketError.ConnectionReset)
                {
                    Disconnet();
                }
                return;
            }

            UDPClient.BeginReceive(UDPRecieveCallback, null);

            var stream = new MemoryStream(data);
            var reader = new BinaryReader(stream);

            byte code = reader.ReadByte();
            Console.WriteLine(data.Length + $" {code}, {stream.Capacity} {stream.Position}");

            int length = -1;
            uint packetId = uint.MaxValue;
            try
            {
                switch (code)
                {
                    case 100:
                        packetId = reader.ReadUInt32();
                        length = reader.ReadUInt16();
                        Cell player_cell = null;
                        for (int i = 0; i < length; i++)
                        {
                            int x = reader.ReadUInt16();
                            int y = reader.ReadUInt16();
                            int radius = reader.ReadUInt16();
                            string name_id = reader.ReadString();

                            if (Program.cells.ContainsKey(name_id))
                                continue;

                            var cell = new Cell(name_id, (x, y), radius);
                            if (name_id == player.Item2)
                                player_cell = cell;

                            Program.cells.Add(name_id, cell);
                        }
                        if(player_cell != null)
                            player.Item1 = player_cell;

                        SendSuccess(packetId, player.Item2);
                        break;

                    case 101:
                        while(!isReadyToPlay) { System.Threading.Thread.Sleep(5); }

                        packetId = reader.ReadUInt32();
                        length = reader.ReadUInt16();
                        for (int i = 0; i < length; i++)
                        {
                            string name_id = reader.ReadString();
                            if (!Program.cells.ContainsKey(name_id))
                                continue;

                            Program.cells.Remove(name_id);
                        }
                        SendSuccess(packetId, player.Item2);
                        break;

                    case 102:
                        if (!isReadyToPlay)
                            break;

                        length = reader.ReadUInt16();
                        for (int i = 0; i < length; i++)
                        {
                            int x = reader.ReadUInt16();
                            int y = reader.ReadUInt16();
                            int radius = reader.ReadUInt16();
                            string name_id = reader.ReadString();
                            if (!Program.cells.ContainsKey(name_id))
                                continue;
                            Program.cells[name_id].Update((x, y), radius);
                        }
                        break;
                    case 200:
                        packetId = reader.ReadUInt32();
                        break;
                    case 254:
                        packetId = reader.ReadUInt32();
                        string username = reader.ReadString();
                        player = (new Cell(username, (0, 0), 1), username);

                        SendSuccess(packetId, username);
                        break;
                }
            }
            catch (Exception error)
            {
                Console.WriteLine($"-- UDPRecieveCallback Error --\n" +
                    $"{error.StackTrace}" +
                    $"\n{error.Message}");
            }
            finally
            {
                //var lostPacket = noResponsePackets.Peek();
                //if (lostPacket.Item1 == packetId)
                //    noResponsePackets.Dequeue();
                //else
                //{
                //    var bytes = lostPacket.Item2;
                //    UDPClient.BeginSend(bytes, bytes.Length, UDPSendCallback, null);
                //}
            }

        }

        static void SendSuccess(uint packedId, string username)
        {
            var name = Encoding.ASCII.GetBytes(username);
            List<byte> bytes = new List<byte>();
            bytes.Add(200);
            bytes.Add((byte)name.Length);
            bytes.AddRange(name);
            bytes.AddRange(BitConverter.GetBytes(packedId));

            var array = bytes.ToArray();
            UDPClient.BeginSend(array, array.Length, UDPSendCallback, null);
        }

        static void SendConnect(string Username)
        {
            var name = Encoding.ASCII.GetBytes(Username);
            List<byte> bytes = new List<byte>();
            bytes.Add(254);
            bytes.Add((byte)name.Length);
            bytes.AddRange(name);

            var array = bytes.ToArray();
            UDPClient.BeginSend(array, array.Length, UDPSendCallback, null);
        }

        static void SendDisconnect()
        {
            var name = Encoding.ASCII.GetBytes(player.Item2);
            List<byte> bytes = new List<byte>();
            bytes.Add(255);
            bytes.Add((byte)name.Length);
            bytes.AddRange(name);

            var array = bytes.ToArray();
            UDPClient.BeginSend(array, array.Length, UDPSendCallback, null);
        }

        static void SendMoveVec((int, int) mousePos)
        {
            var name = Encoding.ASCII.GetBytes(player.Item2);
            List<byte> bytes = new List<byte>();
            bytes.Add(102);
            bytes.Add((byte)name.Length);
            bytes.AddRange(name);

            float dx = -480 / 2 + mousePos.Item1;
            float dy = -480 / 2 + mousePos.Item2;

            float length = (float)Math.Sqrt(Math.Pow(dx, 2) + Math.Pow(dy, 2));
            if (length <= 1)
                length = 1;

            var mouseVec = (dx / length, dy / length);
            //Console.WriteLine(mouseVec);

            bytes.AddRange(BitConverter.GetBytes(mouseVec.Item1));
            bytes.AddRange(BitConverter.GetBytes(mouseVec.Item2));

            var array = bytes.ToArray();
            UDPClient.BeginSend(array, array.Length, UDPSendCallback, null);
            System.Threading.Thread.Sleep(5);
        }

    }
}
