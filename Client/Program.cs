using System;
using System.Collections.Generic;
using System.Text;
using SDL2;
namespace Client
{
    class Program
    {
        static readonly (int, int) windowSize = (640, 640);
       // static int FPS = 120; 
        static int quit = 0;
        static (int, int) mousePos = (0,0);
        static (Cell, string)? player = null;
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

                    case SDL.SDL_EventType.SDL_QUIT:
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
                    IpAdress = Console.ReadLine();
                    port = int.Parse(Console.ReadLine());
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

                while (quit == 0 || NetCode.isConnected)
                {
                    PollEvents();
                    if (NetCode.isConnected && NetCode.isReadToPlay)
                    {
                        NetCode.Update(mousePos);

                        SDL.SDL_SetRenderDrawColor(renderer, 255, 255, 255, 255);
                        SDL.SDL_RenderClear(renderer);

                        var widest = Math.Max(windowSize.Item1, windowSize.Item2);
                        var allowed = Math.Min(widest, 840);
                        var coef = allowed / Math.Max(widest, 840) * player.Value.Item1.radius / 70;
                        var offset = (windowSize.Item1 * coef, windowSize.Item2 * coef);

                        foreach (var cell in cells)
                        {
                            cell.Value.Draw(renderer, player.Value.Item1.center, offset, coef);
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
