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
        public int mass;
        public SDL.SDL_Rect image;

        public Cell(int id, string name, (int, int) coords, int mass)
        {
            this.name = name;
            this.center = coords;
            this.mass = mass;
            this.id = id;
        }

        public void Update((int, int) coords, int mass)
        {
            this.center = coords;
            this.mass = mass;
        }
    }
}
