using System;
using System.Collections.Generic;
using System.Text;
using SDL2;

namespace Client
{
    class Cell
    {
        public static IntPtr playerCircle;
        public static IntPtr foodCircle;
        public int id;
        public string name;
        public (int, int) center;
        public int radius;
        //public SDL.SDL_FRect image;

        public Cell(string username, (int, int) coords, int radius)
        {
            var name_id = username.Split("#");
            this.name = name_id[0];
            this.center = coords;
            this.radius = radius;
            this.id = int.Parse(name_id[1]);
            //this.image = new SDL.SDL_FRect { x = coords.Item1-radius, y = coords.Item2 - radius, w = radius*2, h = radius * 2 };
        }

        public void Draw(IntPtr renderer, (int, int) center, (int, int) offset, float visionScale)
        {
            float dx = (this.center.Item1 - center.Item1) * visionScale;
            float dy = (this.center.Item2 - center.Item2) * visionScale;

            float scaledR = radius * visionScale;

            if (Math.Abs(dx) - scaledR > offset.Item1 || Math.Abs(dy) - scaledR > offset.Item2)
                return;
            IntPtr surface;
            if (name == "")
                surface = foodCircle;
            else
                surface = playerCircle;

            var windowsSize = Program.windowSize;

            var scaledRect = new SDL.SDL_FRect()
            {
                h = scaledR * 2,
                w = scaledR * 2,
                x = windowsSize.Item1/2 + dx - scaledR,
                y = windowsSize.Item2/2 + dy - scaledR
            };
            SDL.SDL_RenderCopyF(renderer, surface, IntPtr.Zero, ref scaledRect);
        }
        public void Update((int, int) coords, int radius)
        {
            this.center = coords;
            this.radius = radius;
        }
    }
}
