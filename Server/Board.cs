using System;
using System.Linq;
using System.Collections.Generic;

namespace Server
{
    class Board
    {
        //public HashSet<Cell> frameGoneEntities = null;
        //public HashSet<Cell> frameNewEntities = null;
        public HashSet<Cell> frameGonePlayers = null;

        Random rand;
        int sectorsNum;
        public int Size { get; private set; }
        public Player leadingPlayer;

        Sector[,] sectors;
        public List<Player> players = new List<Player>();

        public Board(int boardSize = 1000)
        {
            Size = boardSize;
            sectorsNum = (int)Math.Round(Size / (Utils.getRadius(Utils.PLAYER_MASS) * 16) );
            Sector.size = (float)Size / sectorsNum;
            sectors = new Sector[sectorsNum, sectorsNum];

            for (int i = 0; i < sectorsNum; i++)
                for (int j = 0; j < sectorsNum; j++)
                    sectors[i, j] = new Sector();

            rand = new Random();
            Cell.board = this;
        }

        public void FoodFillBoard()
        {
            var startFood = rand.Next(Size/7, Size/5);
            for (int i = 0; i < startFood; i++)
            {
                var coords = (rand.Next(0, Size * 10) / 10f, rand.Next(0, Size * 10) / 10f);
                SpawnEntity<Food>(coords);
            }
        }
        public List<(Player, NewState)> UpdateBoard(float frameScale)
        {
            //frameNewEntities = new HashSet<Cell>();
            //frameGoneEntities = new HashSet<Cell>();
            frameGonePlayers = new HashSet<Cell>();

            var states = new List<(Player, NewState)>();

            players.Sort((p1, p2) => p2.mass.CompareTo(p1.mass));
            if(players.Count > 0) //&& (leadingPlayer == null || Math.Abs(leadingPlayer.mass - players[0].mass) > 20))
                leadingPlayer = players[0];


            foreach(var player in players)
            {
                player.loadedEntities = player.getLoadedEntities();
            }
            foreach (var player in players)
            {
                states.Add((player, player.Move(frameScale)));
            }

            //foreach(var state in states)
            //{
            //    state.Item1.loadedEntities = state.Item1.getLoadedEntities();
            //    state.Item2.loadedEntities = state.Item1.loadedEntities;
            //}

            foreach (var player in players)
            {
                player.Update();
            }

            foreach (Player p in frameGonePlayers)
                RemovePlayer(p);

            if (rand.NextDouble() > 0.98)
                SpawnEntity<Food>((
                    rand.Next(0, Size * 10) / 10f,
                    rand.Next(0, Size * 10) / 10f
                ));
            return states;
        }

        public Player SpawnPlayer()
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
                    if (Math.Pow(diff.Item1,2) + Math.Pow(diff.Item2, 2) <= Math.Pow(p.radius, 2) + Math.Pow(initialRadius, 2))
                    {
                        IsSpawnable = false;
                        break;
                    }
                }
                if (IsSpawnable)
                {
                    var player = SpawnEntity<Player>(coords);
                    this.players.Add(player);
                    return player;
                }
            }
            return null;
        }
        public void RemovePlayer(Player player)
        {
            players.Remove(player);
            RemoveEntity(player);
        }
        public T SpawnEntity<T>((float, float) coords) where T : Cell
        {
            var sectorsCoords = Utils.getSectorNum(coords);

            var entity = (T)Activator.CreateInstance(typeof(T), coords);
            sectors[sectorsCoords.Item1, sectorsCoords.Item2].entities.Add(entity);

            return entity;
        }
        public void RemoveEntity(Cell entity)
        {
            var sector = Utils.getSectorNum(entity.center);
            sectors[sector.Item1, sector.Item2].entities.Remove(entity);
        }

        public HashSet<Sector> GetNeighbourSectors(Cell entity, int radius)
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