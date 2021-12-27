using System;
using System.Linq;
using System.Collections.Generic;

namespace Server
{
    class Board
    {
        public HashSet<Cell> goneEntities = null;
        public HashSet<Cell> newEntities = null;
        Random rand;
        int sectorsNum;
        public int Size { get; private set; }

        Sector[,] sectors;
        public List<Player> players = new List<Player>();

        public Board()
        {
            Size = 2000;
            sectorsNum = (int)Math.Round(Size / (Utils.getRadius(Utils.PLAYER_MASS) * 16) );
            Sector.size = (float)Size / sectorsNum;
            sectors = new Sector[sectorsNum, sectorsNum];

            for (int i = 0; i < sectorsNum; i++)
                for (int j = 0; j < sectorsNum; j++)
                    sectors[i, j] = new Sector();

            rand = new Random();
            Cell.board = this;
        }

        public void foodFillBoard()
        {
            var startFood = rand.Next(Size/3, Size/2);
            for (int i = 0; i < startFood; i++)
            {
                var coords = (rand.Next(0, Size * 10) / 10f, rand.Next(0, Size * 10) / 10f);
                spawnEntity<Food>(coords);
            }
        }

        public Player spawnPlayer()
        {
            bool IsSpawnable = false;
            double initialRadius = Utils.getRadius(Utils.PLAYER_MASS);

            while (!IsSpawnable)
            {
                var coords = (rand.Next(0, Size * 10) / 10f, rand.Next(0, Size * 10) / 10f);
                IsSpawnable = true;

                var sectorsCoords = Utils.getSectorNum(coords);
                var sector = sectors[sectorsCoords.Item1, sectorsCoords.Item2];
                var players = sector.entities.Where(x => typeof(Player).IsInstanceOfType(x));

                foreach (var p in players)
                {
                    var diff = Utils.Subtract(p.center, coords);
                    if (diff.Item1 + diff.Item2 <= Math.Pow(p.radius + initialRadius, 2))
                    {
                        IsSpawnable = false;
                        break;
                    }
                }
                if (IsSpawnable)
                {
                    var player = spawnEntity<Player>(coords);
                    this.players.Add(player);
                    return player;
                }
            }
            return null;
        }

        public T spawnEntity<T>((float, float) coords) where T : Cell
        {
            var sectorsCoords = Utils.getSectorNum(coords);

            var entity = (T)Activator.CreateInstance(typeof(T), coords);
            sectors[sectorsCoords.Item1, sectorsCoords.Item2].entities.Add(entity);

            return entity;
        }

        public List<(Player, NewState)> updateBoard(float frameScale)
        {
            newEntities = new HashSet<Cell>();
            goneEntities = new HashSet<Cell>();
            //if (rand.NextDouble()> 0.95)
            //    newEntities.Add(spawnEntity<Food>((
            //        rand.Next(0, Size * 10) / 10f, 
            //        rand.Next(0, Size * 10) / 10f
            //    )));

            var states = new List<(Player, NewState)>();

            players.Sort((p1, p2) => p1.mass.CompareTo(p2.mass));
            foreach(var player in players)
            {
                states.Add((player, player.Move(frameScale)));
            }
            //foreach (var player in players)
            //{
            //    player.Update();
            //}

            return states;
        }

        public void removeEntity(Cell entity)
        {
            var sector = Utils.getSectorNum(entity.center);
            sectors[sector.Item1, sector.Item2].entities.Remove(entity);
        }

        public HashSet<Sector> getVisibleArea(Cell entity, int radius = 1)
        {
            HashSet<Sector> res = new HashSet<Sector>();
            var sectorCoords = Utils.getSectorNum(entity.center);
            for (int i = sectorCoords.Item1 - radius; i <= sectorCoords.Item1 + radius; i++)
            {
                if (i > sectorsNum - 1 || i < 0)
                    continue;
                for (int j = sectorCoords.Item2 - radius; j <= sectorCoords.Item2 + radius; j++)
                {
                    if (j > sectorsNum - 1 || j < 0)
                        continue;

                    res.Add(sectors[i, j]);
                }
            }

            return res;
        }
    }
}