using System;

namespace Server
{
    static class Utils
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