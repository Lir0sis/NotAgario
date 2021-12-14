using System;
using System.Collections.Generic;

namespace Server
{
    public class Map<T1, T2>
    {
        private Dictionary<T1, T2> _forward = new Dictionary<T1, T2>();
        private Dictionary<T2, T1> _reverse = new Dictionary<T2, T1>();

        public Map()
        {
            this.Forward = new Indexer<T1, T2>(_forward);
            this.Reverse = new Indexer<T2, T1>(_reverse);
        }

        public class Indexer<T3, T4>
        {
            private Dictionary<T3, T4> _dictionary;
            public Indexer(Dictionary<T3, T4> dictionary)
            {
                _dictionary = dictionary;
            }
            public T4 this[T3 index]
            {
                get { return _dictionary[index]; }
                set { _dictionary[index] = value; }
            }
        }

        public void Add(T1 t1, T2 t2)
        {
            _forward.Add(t1, t2);
            _reverse.Add(t2, t1);
        }

        public Indexer<T1, T2> Forward { get; private set; }
        public Indexer<T2, T1> Reverse { get; private set; }
    }
    public static class Utils
    {
        public const int PLAYER_MASS = 100;
        
        public static double getRadius(int mass)
        {
            return Math.Log(1 + mass / 5) * 2;
        }

        public static double getSpeed(int mass)
        {
            return Math.Log10(1 + mass / 5) * 2;
        }

        public static (int, int) getSectorNum((float, float) coords, int scale)
        {
            return ((int)Math.Floor(coords.Item1 / scale), (int)Math.Floor(coords.Item2 / scale));
        }

        public static (T1, T2) Subtract<T1, T2>((T1, T2) tuple1, (T1, T2) tuple2)
        {
            dynamic a1 = tuple1.Item1, b1 = tuple1.Item2, 
                a2 = tuple2.Item1, b2 = tuple2.Item2;

            return (a1 - a2, b1 - b2);
        }

        public static (T1, T2) Add<T1, T2>((T1, T2) tuple1, (T1, T2) tuple2)
        {
            dynamic a1 = tuple1.Item1, b1 = tuple1.Item2,
                a2 = tuple2.Item1, b2 = tuple2.Item2;

            return (a1 + a2, b1 + b2);
        }
    }
}