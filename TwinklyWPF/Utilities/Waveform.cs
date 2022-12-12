using System;

namespace TwinklyWPF.Utilities
{
    public class Waveform
    {
        //      /|/|/|/|
        public static double Sawtooth(double x, double period)
        {
            return fmodf(x, period) / period;
        }

        //      ___/___/___/___/
        public static double SpacedSawtooth(double x, double period)
        {
            return Math.Max(0, fmodf(x, period) / period * 4.0 - 3.0);
        }

        //      \/\/\/\/
        public static double Triangle(double x, double period)
        {
            return Math.Abs(x / period % 2.0 - 1.0);
        }

        //      \______/\______/
        public static double SpacedTriangle(double x, double period)
        {
            return Math.Max(0, Math.Abs(x / (period * 4.0) % 2.0 - 1.0) * 4.0 - 3.0);
        }

        // danger: for simplicity, assumes y > 0
        // returns a value in the range [0,y)
        public static double fmodf(double x, double y)
        {
            return x < 0
                ? x % y + y
                : x % y;
        }
    }
}
