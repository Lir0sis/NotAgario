using System;
using System.Linq;

namespace Server
{
    class Board
    {
        Random rand;
        int sectorsScale;
        int size;

        Sector[,] sectors;

        public Board()
        {
            size = 2000;
            sectorsScale = (int)Math.Round(size / Utils.getRadius(Utils.PLAYER_MASS) / 8);
            sectors = new Sector[sectorsScale, sectorsScale];
            rand = new Random();
        }

        private void foodFillBoard()
        {
            var startFood = rand.Next(140, 250);
            for (int i = 0; i < startFood; i++)
            {
                var coords = (rand.Next(0, size * 10) / 10f, rand.Next(0, size * 10) / 10f);
                spawnEntity<Food>(coords);
            }
        }

        protected Player spawnPlayer()
        {
            Player player = null;
            while (player == null)
            {
                var coords = (rand.Next(0, size * 10) / 10f, rand.Next(0, size * 10) / 10f);

                player = new Player(coords, this);

                var sectorsCoords = Utils.getSectorNum(coords, sectorsScale);
                var sector = sectors[sectorsCoords.Item1, sectorsCoords.Item2];
                var players = sector.entities.Where(x => typeof(Player).IsInstanceOfType(x));

                foreach (Player p in players)
                {
                    var diff = Utils.Subtract(p.center, player.center);
                    if (diff.Item1 + diff.Item2 <= Math.Pow(p.radius + player.radius, 2))
                    {
                        player = null;
                        break;
                    }
                }
                if (player != null)
                {
                    sector.entities.Add(player);

                }
            }
            return player;
        }

        public T spawnEntity<T>((float, float) coords) where T : Cell
        {
            var sectorsCoords = Utils.getSectorNum(coords, sectorsScale);

            var entity = (T)Activator.CreateInstance(typeof(T), coords);
            sectors[sectorsCoords.Item1, sectorsCoords.Item2].entities.Add(entity);

            return entity;
        }
    }
}