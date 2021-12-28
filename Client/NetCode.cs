using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Linq;

namespace Client
{
    static class NetCode
    {
        static UdpClient UDPClient;

        static (Cell, string) player = (null, "");

        enum ActionApplied {
            ADD,
            REMOVE,
            UPDATE
        }
        public static int TimeOutMax = 5;
        public static DateTime lastResponse = DateTime.UtcNow;

        static ConcurrentQueue<(ActionApplied, List<dynamic[]>)> queuedDicts = 
            new ConcurrentQueue<(ActionApplied, List<dynamic[]>)>();

        static HashSet<uint> seenPackets = new HashSet<uint>();
        public static (int, int) playerCenter { get => player.Item1.center; }
        public static int playerRadius { 
            get
            { 
                if (isReadyToRender)
                    return player.Item1.radius;
                else
                    return 0;
            }
        }
        public static bool isSpectator { get; private set; }
        public static bool isConnected { get => player.Item2 != ""; }
        public static bool isReadyToRender { get => player.Item1 != null; }
        public static bool SocketActive { get => UDPClient != null; }

        public static void Init()
        {
            queuedDicts = new ConcurrentQueue<(ActionApplied, List<dynamic[]>)>();
            Program.cells = new Dictionary<string, Cell>();
        }
        public static void Connect(string IpAddress, int port, string username)
        {
            if (!SocketActive)
            {
                seenPackets = new HashSet<uint>();
                UDPClient = new UdpClient(IpAddress, port);
                UDPClient.DontFragment = true;
                UDPClient.BeginReceive(UDPRecieveCallback, null);
            }
            SendConnect(username);
        }

        public static void Disconnect()
        {
            Reset();
            Init();
        }
        public static void Reset()
        {
            if (SocketActive)
                UDPClient.Close();
            UDPClient = null;
            player = (null, "");
        }

        public static void Update((int, int) mousePos)
        {
            if (isSpectator)
                return;
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
        }

        public static void SyncDict()
        {
            var itemsN = queuedDicts.Count;
            while (itemsN > 0)
            {
                itemsN--;
                (ActionApplied, List<dynamic[]>) listOfActions;
                while (!queuedDicts.TryDequeue(out listOfActions)) { };

                if (listOfActions.Item1 == ActionApplied.ADD)
                    foreach (var i in listOfActions.Item2)
                    {
                        string key = i[0];
                        Cell cell = i[1];
                        if (Program.cells.ContainsKey(key))
                            continue;
                        Program.cells.Add(key, cell);
                        if (key == player.Item2)
                            player.Item1 = cell;
                    }
                else if (listOfActions.Item1 == ActionApplied.REMOVE)
                    foreach (var i in listOfActions.Item2)
                    {
                        string key = i[1];
                        if (Program.cells.ContainsKey(key))
                            Program.cells.Remove(key);
                    }
                else if (listOfActions.Item1 == ActionApplied.UPDATE)
                    foreach (var i in listOfActions.Item2)
                    {
                        string key = i[0];
                        (int, int) coords = i[1];
                        int radius = i[2];
                        if (key == player.Item2)
                            player.Item1.Update(coords, radius);
                        else
                            if (Program.cells.ContainsKey(key))
                            Program.cells[key].Update(coords, radius);
                    }
            }
        }

        static void UDPRecieveCallback(IAsyncResult result)
        {
            //var responseNow = DateTime.UtcNow;
            //if ((responseNow - lastResponse).TotalSeconds >= TimeOutMax)
            //{
            //    SendDisconnect();
            //    Disconnect();
            //    return;
            //}
            IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = new byte[0];
            try
            {
                if (UDPClient == null)
                    return;
                data = UDPClient.EndReceive(result, ref clientEndPoint);
            }
            catch 
            {
                Disconnect();
                return;
            }

            UDPClient.BeginReceive(UDPRecieveCallback, null);

            var stream = new MemoryStream(data);
            var reader = new BinaryReader(stream);

            byte code = reader.ReadByte();

            int length = -1;
            uint packetId = uint.MaxValue;
            List<dynamic[]> actionList = null;

            try
            {
                switch (code)
                {
                    case 100:
                        packetId = reader.ReadUInt32();

                        if (!seenPackets.Contains(packetId))
                        {
                            actionList = new List<dynamic[]>();
                            length = reader.ReadUInt16();

                            for (int i = 0; i < length; i++)
                            {
                                int x = reader.ReadUInt16();
                                int y = reader.ReadUInt16();
                                int radius = reader.ReadUInt16();
                                string name_id = reader.ReadString();

                                var cell = new Cell(name_id, (x, y), radius);

                                actionList.Add(new dynamic[] { name_id, cell });
                            }

                            queuedDicts.Enqueue((ActionApplied.ADD, actionList));
                            seenPackets.Add(packetId);
                        }
                        SendSuccess(packetId, player.Item2);
                        break;

                    case 101:
                        while(!isReadyToRender) { System.Threading.Thread.Sleep(5); }
                        packetId = reader.ReadUInt32();
                        if (!seenPackets.Contains(packetId))
                        {
                            actionList = new List<dynamic[]>();
                            length = reader.ReadUInt16();
                            for (int i = 0; i < length; i++)
                            {
                                string name_id = reader.ReadString();

                                if (name_id.Split("#")[0] == "")
                                    actionList.Add(new dynamic[] { false, name_id });
                                else
                                    actionList.Add(new dynamic[] { true, name_id });
                            }

                            queuedDicts.Enqueue((ActionApplied.REMOVE, actionList));
                            seenPackets.Add(packetId);
                        }
                        SendSuccess(packetId, player.Item2);
                        break;

                    case 102:
                        if (!isReadyToRender)
                            break;
                        actionList = new List<dynamic[]>();

                        length = reader.ReadUInt16();
                        for (int i = 0; i < length; i++)
                        {
                            int x = reader.ReadUInt16();
                            int y = reader.ReadUInt16();
                            int radius = reader.ReadUInt16();
                            string name_id = reader.ReadString();

                            actionList.Add(new dynamic[] { name_id, (x, y), radius });
                        }

                        queuedDicts.Enqueue((ActionApplied.UPDATE, actionList));
                        break;
                    case 200:
                        packetId = reader.ReadUInt32();
                        break;
                    case 210:
                        packetId = reader.ReadUInt32();
                        if (!seenPackets.Contains(packetId))
                        {
                            string res = "--- LeaderBoard ---";
                            length = reader.ReadUInt16();
                            for (int i = 0; i < length; i++)
                            {
                                int mass = reader.ReadUInt16();
                                string name_id = reader.ReadString();

                                res +=$"\n {name_id} - {mass}";
                            }
                            Console.WriteLine(res);
                            seenPackets.Add(packetId);
                        }
                        SendSuccess(packetId, player.Item2);
                        break;
                    case 240:
                        packetId = reader.ReadUInt32();
                        if (!seenPackets.Contains(packetId))
                        {
                            Init();
                            string username = reader.ReadString();
                            player = (new Cell(username, (0, 0), 1), username);
                            seenPackets.Add(packetId);
                            isSpectator = true;
                        }
                        SendSuccess(packetId, player.Item2);
                        break;
                    case 254:
                        packetId = reader.ReadUInt32();
                        if (!seenPackets.Contains(packetId))
                        {
                            Init();
                            string username = reader.ReadString();
                            player = (new Cell(username, (0, 0), 1), username);
                            seenPackets.Add(packetId);
                            isSpectator = false;
                        }

                        SendSuccess(packetId, player.Item2);
                        break;
                    case 255:
                        packetId = reader.ReadUInt32();
                        if (!seenPackets.Contains(packetId))
                        {
                            Disconnect();
                            seenPackets.Add(packetId);
                            Console.WriteLine($"--Disconnected--");
                        }
                        SendSuccess(packetId, player.Item2);
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

        static void SendSuccess(uint packedId, string username)
        {
            if (!SocketActive)
                return;
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
            if (!SocketActive)
                return;
            var name = Encoding.ASCII.GetBytes(Username);
            List<byte> bytes = new List<byte>() { 254 };
            bytes.Add((byte)name.Length);
            bytes.AddRange(name);

            var array = bytes.ToArray();
            UDPClient.BeginSend(array, array.Length, UDPSendCallback, null);
        }

        public static void SendDisconnect()
        {
            if (!SocketActive || !isConnected)
                return;
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
            if (!SocketActive)
                return;
            var name = Encoding.ASCII.GetBytes(player.Item2);
            List<byte> bytes = new List<byte>();
            bytes.Add(102);
            bytes.Add((byte)name.Length);
            bytes.AddRange(name);

            var wSize = Program.windowSize;

            float dx = -wSize.Item1 / 2 + mousePos.Item1;
            float dy = -wSize.Item2 / 2 + mousePos.Item2;
            float maxLength = Math.Min(wSize.Item1, wSize.Item2) / 3;

            float length = (float)Math.Sqrt(Math.Pow(dx, 2) + Math.Pow(dy, 2));
            if (length < maxLength)
                length = maxLength;

            var mouseVec = (dx / length, dy / length);

            bytes.AddRange(BitConverter.GetBytes(mouseVec.Item1));
            bytes.AddRange(BitConverter.GetBytes(mouseVec.Item2));

            var array = bytes.ToArray();
            UDPClient.BeginSend(array, array.Length, UDPSendCallback, null);
        }

    }
}
