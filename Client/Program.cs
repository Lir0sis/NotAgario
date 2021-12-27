using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using SDL2;
namespace Client
{
    class Program
    {
        public static readonly (int, int) windowSize = (640, 640);
       // static int FPS = 120; 
        static int quit = 0;
        static (int, int) mousePos = (0,0);

        static IntPtr window;
        static IntPtr renderer;

        public static Dictionary<string, Cell> cells = null;

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

                    case SDL.SDL_EventType.SDL_WINDOWEVENT:
                        if(SDL.SDL_WindowEventID.SDL_WINDOWEVENT_CLOSE == @event.window.windowEvent)
                            quit = 1;
                        break;
                }
            }
        }
        static void Main(string[] args)
        {
            string username = "";
            string IpAdress = "";
            int port = 0;

            while (true)
            {
                if (!NetCode.isInitialized)
                {
                    IpAdress = "127.0.0.1";//"5.248.192.64";//Console.ReadLine();
                    port = 7777;// int.Parse(Console.ReadLine());
                    username = Console.ReadLine();
                }

                if (IpAdress == "exit" || IpAdress == "")
                    break;

                cells = new Dictionary<string, Cell>();
                NetCode.Connect(IpAdress, port, username);

                if (SDL.SDL_Init(SDL.SDL_INIT_EVERYTHING) == -1)
                    Console.WriteLine("Couldn't initialize SDL2#");
                if (window == IntPtr.Zero)
                    window = SDL.SDL_CreateWindow("NotAgario", SDL.SDL_WINDOWPOS_UNDEFINED,
                        SDL.SDL_WINDOWPOS_UNDEFINED, windowSize.Item1, windowSize.Item2, 0);

                if (window == IntPtr.Zero)
                    return;

                if (renderer == IntPtr.Zero)
                    renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);

                //1
                //cells.Add("#22", new Cell(22, "", (1, 1), 9));
                //cells.Add("#25", new Cell(25, "", (1, 8), 9));
                //cells.Add("#23", new Cell(23, "", (22, 1), 9));
                //
                //NetCode.player = (new Cell(223, "valik", (4, 17), 12), "valik#223");
                //
                //cells.Add(NetCode.player.Item2, NetCode.player.Item1);
                //1

                while (quit == 0 || NetCode.isConnected)
                {
                    PollEvents();
                    if (NetCode.isConnected && NetCode.isReadyToPlay)
                    {
                        NetCode.Update(mousePos);

                        SDL.SDL_SetRenderDrawColor(renderer, 255, 255, 255, 255);
                        SDL.SDL_RenderClear(renderer);

                        var widest = Math.Max(windowSize.Item1, windowSize.Item2);
                        var allowed = Math.Min(widest, 840);
                        float coef = (float)allowed / Math.Max(widest, 840) /** NetCode.playerRadius / 20f*/;
                        var offset = windowSize; //((int)Math.Round(windowSize.Item1 * coef), (int)Math.Round(windowSize.Item2 * coef));

                        var list = cells.ToList();

                        foreach (var cell in list)
                        {
                            cell.Value.Draw(renderer, NetCode.playerCenter, offset, coef);
                        }

                        SDL.SDL_RenderPresent(renderer);
                    }
                    SDL.SDL_Delay(15);
                }
                NetCode.Disconnet();
                SDL.SDL_DestroyWindow(window);
            }
            SDL.SDL_Quit();

        }
    }
}
