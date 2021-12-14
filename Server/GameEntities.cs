using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server
{
    struct NewState
    {
        public Player player;
        public List<HashSet<Cell>> newEntities;
        public HashSet<Cell> goneEntities;
        public HashSet<Cell> loadedEntities;
    }

    struct Sector
    {
        public static int size;
        //public (int, int) coords;
        public HashSet<Cell> entities;
    }
    abstract class Cell
    {
        public static Board board;

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
        public HashSet<Sector> loadedArea;
        public (float, float) moveVec = (0f,0f);
        public Player((float, float) coords)
        {
            center = coords;
            mass = Utils.PLAYER_MASS;
            loadedArea = board.getVisibleArea(this);
        }
        public NewState Move(float frameScale)
        {
            var result = new NewState();
            var newCoords = Utils.Add(center, Utils.Multiply(moveVec,frameScale));
            if (Utils.getSectorNum(center) != Utils.getSectorNum(newCoords))
            {
                var newArea = board.getVisibleArea(this);
                foreach (var sec in newArea.Except(loadedArea))
                    result.goneEntities.UnionWith(sec.entities);
                foreach (var sec in loadedArea.Except(newArea))
                    result.newEntities.Add(sec.entities);
                loadedArea = newArea;
            }
            center = newCoords;
            return result;
        }
        public void Update()
        {
            foreach (var sec in loadedArea)
            {
                foreach (var entity in sec.entities)
                {

                }
            }
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
