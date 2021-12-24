using System;
using System.Collections.Generic;
using System.Text;
using SDL2;

namespace Client
{
    class Cell
    {
        public int id;
        public string name;
        public (int, int) center;
        public int radius;
        //public SDL.SDL_FRect image;

        public Cell(int id, string name, (int, int) coords, int radius)
        {
            this.name = name;
            this.center = coords;
            this.radius = radius;
            this.id = id;
            //this.image = new SDL.SDL_FRect { x = coords.Item1-radius, y = coords.Item2 - radius, w = radius*2, h = radius * 2 };
        }

        public void Draw(IntPtr renderer, (int, int) center, (int, int) offset, float visionScale)
        {
            var dx = this.center.Item1 - center.Item1;
            var dy = this.center.Item2 - center.Item2;

            var scaledR = radius * visionScale;

            if (Math.Abs(dx) - scaledR > offset.Item1 || Math.Abs(dy) - scaledR > offset.Item2)
                return;

            if (name[0] == '#')
                SDL.SDL_SetRenderDrawColor(renderer, 100, 255, 50, 255);
            else
                SDL.SDL_SetRenderDrawColor(renderer, 100, 100, 255, 255);

            var scaledRect = new SDL.SDL_FRect()
            {
                h = scaledR * 2,
                w = scaledR * 2,
                x = dx - scaledR,
                y = dy - scaledR
            };

            SDL.SDL_RenderDrawRectF(renderer, ref scaledRect);
            SDL.SDL_RenderFillRectF(renderer, ref scaledRect);
        }
        public void Update((int, int) coords, int radius)
        {
            this.center = coords;
            this.radius = radius;
        }
    }
}
