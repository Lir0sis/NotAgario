using System;
using System.Net;
using System.Net.Sockets;
using SDL2;
namespace Client
{
    class Program
    {
        static UdpClient client;
        static int FPS = 120; 
        static int quit = 0;
        static (int, int) mousePos = (0,0);
        static SDL.SDL_Rect rect = new SDL.SDL_Rect { x = -30, y = 30, w = 50, h = 50 };

        static void SendTestData(IAsyncResult result)
        {
            // UdpClient end = (UdpClient)result.AsyncState;
            Console.WriteLine($"number of bytes sent: {client.EndSend(result)}");
        }

        static void PollEvents()
        {
            SDL.SDL_Event @event;

            while(SDL.SDL_PollEvent(out @event) == 1)
            {
                switch (@event.type) {
                    case SDL.SDL_EventType.SDL_KEYDOWN:
                        client.BeginSend(new byte[] { 255, 255, 255 }, 3, SendTestData, null);
                        Console.WriteLine("Pressed Key");
                        break;
                    case SDL.SDL_EventType.SDL_KEYUP:
                        Console.WriteLine("Released Key");
                        break;

                    case SDL.SDL_EventType.SDL_MOUSEMOTION:
                        client.BeginSend(new byte[] { 255, 255, 255 }, 3, SendTestData, null);
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
            client = new UdpClient("5.248.192.64", 7777);
            
            if (SDL.SDL_Init(SDL.SDL_INIT_EVERYTHING) == -1)
            {
                Console.WriteLine("Couldn't initialize SDL2#");
            }
            var Window = SDL.SDL_CreateWindow("Hello, SDL 2!", SDL.SDL_WINDOWPOS_UNDEFINED,
            SDL.SDL_WINDOWPOS_UNDEFINED, 640, 480, 0);

            if (Window == null)
                return;

            var renderer = SDL.SDL_CreateRenderer(Window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);

            while (quit == 0)
            {
                PollEvents();
                SDL.SDL_SetRenderDrawColor(renderer, 255, 255, 255, 255);

                SDL.SDL_RenderClear(renderer);

                SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 255, 255);

                SDL.SDL_RenderDrawRect(renderer, ref rect);

                SDL.SDL_RenderFillRect(renderer, ref rect);

                SDL.SDL_Delay(15);
                SDL.SDL_RenderPresent(renderer);
            }

            SDL.SDL_DestroyWindow(Window);
            SDL.SDL_Quit();

        }
    }
}
