using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using SDL2;
namespace Client
{
    class Program
    {
        private static DateTime _time = DateTime.UtcNow;
        public static void UpdateTime()
        {
            var lastFrame = _time;
            _time = DateTime.UtcNow;
            FrameTime = (float)(_time - lastFrame).TotalSeconds;
        }
        public static float FrameTime { get; private set; }

        public static readonly (int, int) windowSize = (640, 640);

        static int quit = 0;
        static (int, int) mousePos = (0, 0);
        public static float FPS = 30;

        static IntPtr window;
        static IntPtr renderer;

        public static Dictionary<string, Cell> cells = null;

        static void PollEvents()
        { 
            SDL.SDL_Event @event;

            while (SDL.SDL_PollEvent(out @event) == 1)
            {
                switch (@event.type) {
                    case SDL.SDL_EventType.SDL_MOUSEMOTION:
                        SDL.SDL_GetMouseState(out mousePos.Item1, out mousePos.Item2);
                        break;

                    case SDL.SDL_EventType.SDL_WINDOWEVENT:
                        if (SDL.SDL_WindowEventID.SDL_WINDOWEVENT_CLOSE == @event.window.windowEvent)
                            quit = 1;
                        break;
                }
            }
        }
        static void Main(string[] args)
        {
            var r = new Random();
            string username = args[0] + r.Next(0, 255);
            string IpAdress = args[1];
            int port = int.Parse(args[2]);

            cells = new Dictionary<string, Cell>();
            NetCode.Init();

            if (SDL.SDL_Init(SDL.SDL_INIT_EVERYTHING) == -1)
                Console.WriteLine("Couldn't initialize SDL2#");

            if (window == IntPtr.Zero)
                window = SDL.SDL_CreateWindow("NotAgario", SDL.SDL_WINDOWPOS_UNDEFINED,
                    SDL.SDL_WINDOWPOS_UNDEFINED, windowSize.Item1, windowSize.Item2, 0);

            if (window == IntPtr.Zero)
                return;

            if (renderer == IntPtr.Zero)
                renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);

            IntPtr imageSurface = SDL.SDL_LoadBMP("./resources/circle208x208.bmp");
            IntPtr foodSurface = SDL.SDL_LoadBMP("./resources/circle104x104.bmp");
            IntPtr format1;
            IntPtr format2;
            unsafe
            {
                format1 = ((SDL.SDL_Surface*)imageSurface.ToPointer())->format;
                format2 = ((SDL.SDL_Surface*)foodSurface.ToPointer())->format;
            }
            SDL.SDL_SetColorKey(imageSurface, 1, SDL.SDL_MapRGB(format1, 255, 255, 255));
            SDL.SDL_SetColorKey(foodSurface, 1, SDL.SDL_MapRGB(format2, 255, 255, 255));
            Cell.playerCircle = SDL.SDL_CreateTextureFromSurface(renderer, imageSurface);
            Cell.foodCircle = SDL.SDL_CreateTextureFromSurface(renderer, foodSurface);

            while (quit == 0)
            {
                while (!NetCode.isConnected)
                {
                    NetCode.Connect(IpAdress, port, username);
                    SDL.SDL_Delay(500);
                }
                while (NetCode.isConnected && quit == 0)
                {
                    PollEvents();
                    if (NetCode.isReadyToRender)
                    {
                        float sleepTime = (1f / FPS - FrameTime) * 1000;
                        SDL.SDL_Delay(sleepTime > 0 ? (uint)sleepTime : 1);
                        NetCode.Update(mousePos);

                        SDL.SDL_SetRenderDrawColor(renderer, 255, 255, 255, 255);
                        SDL.SDL_RenderClear(renderer);

                        var widest = Math.Max(windowSize.Item1, windowSize.Item2);
                        var allowed = Math.Min(widest, 840);
                        float coef = (float)allowed / Math.Max(widest, 840) * 80f / NetCode.playerRadius;
                        var offset = windowSize; //((int)Math.Round(windowSize.Item1 * coef), (int)Math.Round(windowSize.Item2 * coef));

                        var list = cells.ToList();

                        foreach (var cell in list)
                        {
                            cell.Value.Draw(renderer, NetCode.playerCenter, offset, coef);
                        }

                        NetCode.SyncDict();

                        SDL.SDL_RenderPresent(renderer);
                        UpdateTime();
                    }
                    SDL.SDL_Delay(15);
                }
            }
            // while (NetCode.SocketActive)
            //{
                NetCode.SendDisconnect();
                SDL.SDL_Delay(500);
            //}
            NetCode.Disconnect();
            SDL.SDL_DestroyWindow(window);
            SDL.SDL_Quit();

        }
    }
}
