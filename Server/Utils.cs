using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace Server
{
    public class ClientsMap<T1, T2>
    {
        private Dictionary<T1, T2> _forward = new Dictionary<T1, T2>();
        private Dictionary<T2, T1> _reverse = new Dictionary<T2, T1>();
        public int Count { get => Forward.Count; }

        public ClientsMap()
        {
            Forward = new Indexer<T1, T2>(_forward);
            Reverse = new Indexer<T2, T1>(_reverse);
        }

        public IEnumerator<KeyValuePair<T1, T2>> GetEnumerator()
        {
            return _forward.GetEnumerator();
        }

        public class Indexer<T3, T4>
        {
            private Dictionary<T3, T4> _dictionary;
            public Indexer(Dictionary<T3, T4> dictionary)
            {
                _dictionary = dictionary;
            }
            public int Count { get => _dictionary.Count; }
            public T4 this[T3 index]
            {
                get { return _dictionary[index]; }
                private set { _dictionary[index] = value; }
            }

            public bool TryGetValue(T3 key, out T4 value)
            {
                return _dictionary.TryGetValue(key, out value);
            }
            public (T3, T4) FindByLambda(Func<T3, bool> comparator)
            {
                foreach (var key in _dictionary.Keys)
                    if (comparator(key))
                        return (key, this[key]);
                return default((T3,T4));
            }
            public bool ContainsKey(T3 key)
            {
                return _dictionary.ContainsKey(key);
            }            
        }
        public void SetByLambda(T2 value, Func<T1, bool> comparator)
        {
            foreach (var key in _forward.Keys)
                if (comparator(key))
                {
                    SetValue(key, value);
                    return;
                }
        }
        public void SetByLambda(T1 value, Func<T2, bool> comparator)
        {
            foreach (var key in _reverse.Keys)
                if (comparator(key))
                {
                    SetValue(key, value);
                    return;
                }
        }
        public void RemoveByLambda(Func<T1, bool> comparator)
        {
            foreach (var key in _forward.Keys)
            {
                if (comparator(key))
                    Remove(key);
            }
        }
        public void RemoveByLambda(Func<T2, bool> comparator)
        {
            foreach (var key in _reverse.Keys)
            {
                if (comparator(key))
                    Remove(key);
            }
        }
        public void SetValue(T1 key, T2 val)
        {
            _forward[key] = val;
            _reverse[val] = key;
        }
        public void SetValue(T2 key, T1 val)
        {
            _forward[val] = key;
            _reverse[key] = val;
        }
        public void Add(T1 t1, T2 t2)
        {
            _forward.Add(t1, t2);
            _reverse.Add(t2, t1);
        }

        public void Remove(T1 t)
        {
            var v = Forward[t];
            _reverse.Remove(v);
            _forward.Remove(t);
        }
        public void Remove(T2 t)
        {
            var v = Reverse[t];
            _reverse.Remove(t);
            _forward.Remove(v);
        }

        public Indexer<T1, T2> Forward { get; private set; }
        public Indexer<T2, T1> Reverse { get; private set; }
    }
    static class Utils
    {
        private static DateTime _time = DateTime.UtcNow;
        public static void UpdateTime()
        {
            var lastFrame = _time;
            _time = DateTime.UtcNow;
            FrameTime = (float)(_time - lastFrame).TotalSeconds;
        }
        public static float FrameTime { get; private set; }
        public const int PLAYER_MASS = 100;
        public static double getRadius(int mass)
        {
            return Math.Log(mass+1) * Math.Sqrt(mass/4);
        }

        public static double getSpeed(int mass)
        {
            return 1/mass * 1000 * 24;
        }

        public static (int, int) getSectorNum((float, float) coords)
        {
            return ((int)Math.Floor(coords.Item1 / (Sector.size + 0.05f)), (int)Math.Floor(coords.Item2 / (Sector.size + 0.05f)));
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

        public static (T1, T2) Multiply<T1, T2>((T1, T2) tuple1, float value)
        {
            dynamic a1 = tuple1.Item1, b1 = tuple1.Item2;

            return (a1 + value, b1 + value);
        }
    }
}