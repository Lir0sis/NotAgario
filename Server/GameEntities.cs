using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server
{
    class NewState
    {
        public List<HashSet<Cell>> newEntities = new List<HashSet<Cell>>();
        public HashSet<Cell> goneEntities = new HashSet<Cell>();
        public HashSet<Cell> loadedEntities = new HashSet<Cell>();
    }

    class Sector
    {
        public static float size;
        //public (int, int) coords;
        public HashSet<Cell> entities = new HashSet<Cell>();
    }
    abstract class Cell
    {
        public static Board board;

        private static ushort id = 0;
        private ushort _id;
        public ushort Id { get => _id; private set { _id = value; } }

        public float radius;
        public (float, float) center;
        private int _mass;
        public float speed;

        protected Cell()
        {
            Id = id++;
        }

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
            loadedArea = board.getVisibleArea(this, (int)Math.Ceiling(radius * 2.5f / Sector.size) + 1);
        }
        public HashSet<Cell> getLoadedArea()
        {
            var area = new HashSet<Cell>();
            foreach(var sec in loadedArea)
            {
                area.UnionWith(sec.entities);
            }
            return area;
        }
        public NewState Move(float frameScale)
        {
            var result = new NewState();
            var newCoords = Utils.Add(center, Utils.Multiply(moveVec, frameScale));

            newCoords = (
                newCoords.Item1 < 0 ? 0 : newCoords.Item1 >= board.Size ? board.Size : newCoords.Item1,
                newCoords.Item2 < 0 ? 0 : newCoords.Item2 >= board.Size ? board.Size : newCoords.Item2
                );

            if (Utils.getSectorNum(center) != Utils.getSectorNum(newCoords))
            {
                var newArea = board.getVisibleArea(this, (int)Math.Ceiling(radius * 2.5f / Sector.size) + 1);
                foreach (var sec in loadedArea.Except(newArea))
                    result.goneEntities.UnionWith(sec.entities);
                foreach (var sec in newArea.Intersect(loadedArea))
                    result.loadedEntities.UnionWith(sec.entities);
                foreach (var sec in newArea.Except(loadedArea))
                    result.newEntities.Add(sec.entities);

                loadedArea = newArea;
            }
            else
                result.loadedEntities = getLoadedArea();
            center = newCoords;
            return result;
        }
        public void Update()
        {
            foreach (var sec in loadedArea)
            {
                foreach (var entity in sec.entities)
                {
                    var vec = Utils.Subtract(center, entity.center);
                    var distEuclid = Math.Pow(vec.Item1, 2) + Math.Pow(vec.Item2, 2);
                    if (distEuclid <= Math.Pow(radius, 2) && radius > entity.radius)
                    {
                        sec.entities.Remove(entity);
                        board.goneEntities.Add(entity);
                        mass += entity.mass / 5;
                    }
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
