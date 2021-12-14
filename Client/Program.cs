using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text;
using SDL2;
namespace Client
{
    class Program
    {
        static UdpClient client;
        static int FPS = 120; 
        static int quit = 0;
        static (int, int) mousePos = (0,0);
        static Cell player = null;

        static Dictionary<string, Cell> cells = new Dictionary<string, Cell>();

        static SDL.SDL_Rect rect = new SDL.SDL_Rect { x = -30, y = 30, w = 50, h = 50 };

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
                            int mass = reader.ReadUInt16();
                            int n = reader.ReadByte();
                            string name_id = Encoding.ASCII.GetString(reader.ReadBytes(n));
                            var cell = new Cell(int.Parse(name_id.Split('#')[1]), name_id.Split('#')[0], (x, y), mass);
                            if (i == 0 && player == null)
                            {
                                player = cell;
                            }
                            cells.Add(name_id, cell);
                        }
                        break;

                    case 101:
                        for (int i = 0; i < reader.ReadUInt16(); i++)
                        {
                            int n = reader.ReadByte();
                            string name_id = Encoding.ASCII.GetString(reader.ReadBytes(n));
                            cells.Remove(name_id);
                        }
                        break;

                    case 102:
                        for (int i = 0; i < reader.ReadUInt16(); i++)
                        {
                            int x = reader.ReadUInt16();
                            int y = reader.ReadUInt16();
                            int mass = reader.ReadUInt16();
                            int n = reader.ReadByte();
                            string name_id = Encoding.ASCII.GetString(reader.ReadBytes(n));
                            cells[name_id].Update((x, y), mass);
                        }
                        break;
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("-- UDPRecieveCallback Error --");
                Console.WriteLine(error.Message);
            }
        }

        static void PollEvents()
        {
            SDL.SDL_Event @event;

            while(SDL.SDL_PollEvent(out @event) == 1)
            {
                switch (@event.type) {
                    case SDL.SDL_EventType.SDL_MOUSEMOTION:
                       
                        SDL.SDL_GetMouseState(out mousePos.Item1, out mousePos.Item2);
                        //Console.WriteLine(mousePos);
                        break;

                    case SDL.SDL_EventType.SDL_QUIT:
                        quit = 1;
                        break;
                }
            }
        }

        static void SendConnect()
        {
            var name = Encoding.ASCII.GetBytes("Valik");
            List<byte> bytes = new List<byte>();
            bytes.Add(254);
            bytes.Add((byte)name.Length);
            bytes.AddRange(name);

            var array = bytes.ToArray();
            client.BeginSend(array, array.Length, UDPSendCallback, null);
        }

        static void SendDisconnect()
        {
            var name = Encoding.ASCII.GetBytes("Valik");
            List<byte> bytes = new List<byte>();
            bytes.Add(255);
            bytes.Add((byte)name.Length);
            bytes.AddRange(name);

            var array = bytes.ToArray();
            client.BeginSend(array, array.Length, UDPSendCallback, null);
        }

        static void SendMoveVec()
        {
            var name = Encoding.ASCII.GetBytes("Valik");
            List<byte> bytes = new List<byte>();
            bytes.Add(102);
            bytes.Add((byte)name.Length);
            bytes.AddRange(name);

            float dx = 480 / 2 - mousePos.Item1;
            float dy = 480 / 2 - mousePos.Item2;

            float length = (float)Math.Sqrt(Math.Pow(dx, 2) + Math.Pow(dy, 2));
            bytes.AddRange(BitConverter.GetBytes(dx / length));
            bytes.AddRange(BitConverter.GetBytes(dy / length));

            var array = bytes.ToArray();
            client.BeginSend(array, array.Length, UDPSendCallback, null);
        }

        static void Main(string[] args)
        {
            client = new UdpClient(Console.ReadLine(), int.Parse(Console.ReadLine()));
            
            if (SDL.SDL_Init(SDL.SDL_INIT_EVERYTHING) == -1)
            {
                Console.WriteLine("Couldn't initialize SDL2#");
            }
            var Window = SDL.SDL_CreateWindow("Hello, SDL 2!", SDL.SDL_WINDOWPOS_UNDEFINED,
            SDL.SDL_WINDOWPOS_UNDEFINED, 640, 640, 0);

            if (Window == null)
                return;

            var renderer = SDL.SDL_CreateRenderer(Window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
            SendConnect();
            while (quit == 0)
            {
                PollEvents();
                SendMoveVec();
                SDL.SDL_SetRenderDrawColor(renderer, 255, 255, 255, 255);

                SDL.SDL_RenderClear(renderer);

                SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 255, 255);

                SDL.SDL_RenderDrawRect(renderer, ref rect);

                SDL.SDL_RenderFillRect(renderer, ref rect);

                SDL.SDL_Delay(15);
                SDL.SDL_RenderPresent(renderer);
            }
            SendDisconnect();
            SDL.SDL_DestroyWindow(Window);
            SDL.SDL_Quit();

        }
    }
}
