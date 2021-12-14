using System;
using System.Collections.Generic;
using System.Text;

namespace Server
{

    struct Sector
    {
        public List<Cell> entities;
    }
    abstract class Cell
    {
        static Board board;

        public float radius;
        public (float, float) center;
        private int _mass;
        public float speed;
        public int mass {
            get => _mass; 
            set
            {
                _mass = value;
                speed = (float)Utils.getSpeed(_mass);
                radius = (float)Utils.getRadius(_mass);
            }
        }

    }

    class Player : Cell
    {
        (float, float) moveVec = (0f,0f);
        public Player((float, float) coords, Board board)
        {
            center = coords;
            mass = Utils.PLAYER_MASS;
        }
    }

    class Food : Cell
    {
        public Food((float, float) coords)
        {
            center = coords;
            mass = 30;
        }
    }
}
