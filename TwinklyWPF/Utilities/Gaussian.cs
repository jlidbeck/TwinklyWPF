using System;

namespace TwinklyWPF.Utilities
{
    public class Gaussian
    {
        public static Random random = new Random();

        private static bool uselast = true;
        private static double next_gaussian = 0.0;

        public static double NextDouble()
        {
            if (uselast)
            {
                uselast = false;
                return next_gaussian;
            }
            else
            {
                double v1, v2, s;
                do
                {
                    v1 = 2.0 * random.NextDouble() - 1.0;
                    v2 = 2.0 * random.NextDouble() - 1.0;
                    s = v1 * v1 + v2 * v2;
                } while (s >= 1.0 || s == 0);

                s = System.Math.Sqrt((-2.0 * System.Math.Log(s)) / s);

                next_gaussian = v2 * s;
                uselast = true;
                return v1 * s;
            }
        }

        public static double NextDouble(double mean, double standard_deviation)
        {
            return mean + NextDouble() * standard_deviation;
        }

        public static int Next(int min, int max)
        {
            return (int)NextDouble(min + (max - min) / 2.0, 1.0);
        }
    }
}
