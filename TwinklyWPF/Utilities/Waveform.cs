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
        //      -- period
        public static double Triangle(double x, double period)
        {
            return Math.Abs(fmodf(2*x / period, 2.0) - 1.0);
        }

        //      \______/\______/
        //      -------- period
        //  frac: ratio of period to width of the triangles
        //  
        public static double SpacedTriangle(double x, double period, double frac)
        {
            return Math.Max(0, Math.Abs(fmodf(2*x / period, 2.0) - 1.0) * frac - frac + 1.0);
        }

        // danger: for simplicity, assumes y > 0
        // returns a value in the range [0,y)
        public static double fmodf(double x, double y)
        {
            return x < 0
                ? x % y + y
                : x % y;
        }
        public static double expgrowth(double initialValue, double time, double k)
        {
            return initialValue * Math.Exp(k * time);
        }

    }
}
