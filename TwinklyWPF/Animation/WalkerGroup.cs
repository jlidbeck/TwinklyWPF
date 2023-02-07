using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwinklyWPF.Utilities;

namespace TwinklyWPF.Animation
{
    public class WalkerGroup
    {
        double[][] GoodPalette;

        //[DebuggerDisplay("Walker x={x} v={velocity} merging={merging}")]
        class Walker : IComparable<Walker>
        {
            // values are managed externally by the WalkerGroup class

            public double x;
            public double velocity;
            public ColorMorph color;

            public bool alive = true;
            public bool merging = false;

            public int CompareTo(Walker other)
            {
                return x.CompareTo(other.x);
            }
        }

        Walker[] walkers;
        public double minx, maxx;

        double extendedmin, extendedmax;
        const double minvelocity = 0.15;

        Stopwatch _stopwatch;
        Random _random = new Random();

        public WalkerGroup(int n, double min, double max, double[][] goodPalette)
        {
            walkers = new Walker[n];
            minx = min;
            maxx = max;
            extendedmin = minx - 0.1 * (maxx - minx);
            extendedmax = maxx + 0.1 * (maxx - minx);

            GoodPalette = goodPalette;

            for (int i = 0; i < n; ++i)
            {
                walkers[i] = CreateWalker(_random.NextDouble() * (max - min) + min);
            }
            Array.Sort<Walker>(walkers);

            _stopwatch = new Stopwatch();
            _stopwatch.Start();
        }

        public override string ToString()
        {
            string sz = String.Format("Walkers[{0}]: ", walkers.Length);
            foreach (var walker in walkers)
            {
                sz += String.Format("{0:0.00} {1:0.00}, ", walker.x, walker.velocity);
            }

            return sz;
        }

        double _lastFrameTime = 0;

        public void Update()
        {
            double t = _stopwatch.ElapsedMilliseconds * 0.001;
            double dt = t - _lastFrameTime;
            _lastFrameTime = t;

            // update all walkers
            Walker previousWalker = null;
            int countAlive = 0;
            for (int i = 0; i < walkers.Length; ++i)
            {
                var walker = walkers[i];

                if (!walker.alive)
                    continue;

                if (previousWalker != null)
                {
                    // check for intersection
                    if (walker.x < previousWalker.x)
                    {
                        walker.merging = false;
                        // walker[i] and walker[i-1] have crossed
                        // delete the slower one
                        if (Math.Abs(previousWalker.velocity) < Math.Abs(walkers[i].velocity))
                            previousWalker.alive = false;
                        else
                            walker.alive = false;
                        //for (int j = walkerToDelete; j < walkers.Length - 1; ++j)
                        //{
                        //    walkers[j] = walkers[j + 1];
                        //}
                        //--n;
                        //--i;
                        continue;
                    }

                    // check for imminent intersection
                    if (!walker.merging)
                    {
                        double intersectionTime = (walker.x - previousWalker.x) / (previousWalker.velocity - walker.velocity);
                        if (intersectionTime > 0 && intersectionTime < 3.0)
                        {
                            // walker[i] and walker[i-1] will intersect soon
                            // delete the slower one
                            int winner =
                                (Math.Abs(previousWalker.velocity) > Math.Abs(walkers[i].velocity))
                                ? i - 1 : i;
                            // start merging their colors
                            walker.merging = true;
                            var color = walkers[winner].color.TargetColor;
                            walker.color.SetTarget(color, intersectionTime);
                            previousWalker.color.SetTarget(color, intersectionTime);
                        }
                    }
                }

                //walker.x = walker.startx + t * walker.velocity;
                walker.x += dt * walker.velocity;
                if (walker.x < extendedmin)
                {
                    walker.x = extendedmin;
                    walker.velocity = Math.Abs(walker.velocity);
                    walker.color.SetTarget(GoodPalette[_random.Next() % GoodPalette.Length]);
                }
                else if (walker.x > extendedmax)
                {
                    walker.x = extendedmax;
                    walker.velocity = -Math.Abs(walker.velocity);
                    walker.color.SetTarget(GoodPalette[_random.Next() % GoodPalette.Length]);
                }


                countAlive++;
                previousWalker = walker;
            }

            if (countAlive < walkers.Length / 2)
            {
                for (int i = 0; i < walkers.Length; ++i)
                {
                    if (!walkers[i].alive)
                    {
                        var x = _random.NextDouble() * (maxx - minx) + minx;
                        walkers[i] = CreateWalker(x);
                        walkers[i].color = new ColorMorph(GetColorAt(x));
                        walkers[i].color.SetTarget(new double[] { 1, 1, 1 });
                    }
                }

                Array.Sort<Walker>(walkers);

            }
        }

        Walker CreateWalker(double x)
        {
            var walker = new Walker
            {
                x = x,
                velocity = minvelocity + 0.5 * Math.Abs(Gaussian.NextDouble()),
                color = RandomColor()
            };

            if (x >= (minx + maxx) * 0.5)
                walker.velocity = -walker.velocity;

            return walker;
        }

        public double[] GetColorAt(double x)
        {
            // find the bracketing pair of walkers
            Walker a = null;
            byte v = 0;
            int i = 0;
            foreach (var b in walkers)
            {
                if (b.x >= x)
                {
                    if (a == null)
                    {
                        // startx is lower than the lowest walker
                        return b.color.GetColor();
                        //return new double[3] { 1, 0, 0 };
                    }

                    // startx is between a and b
                    return ColorMorph.Mix(a.color.GetColor(), b.color.GetColor(), (x - a.x) / (b.x - a.x));
                    //return ColorMorph.Mix(b.merging?White:Black, GoodPalette[i % GoodPalette.Length], (x - a.x) / (b.x - a.x));
                    //return ColorMorph.Mix(Black, b.color.GetColor(), (x - a.x) / (b.x - a.x));
                    //return new double[3] { v, v, v };
                }

                a = b;
                v = (byte)(255 - v);
                ++i;
            }

            // startx is greater than the highest walker
            return a.color.GetColor();
            //return new double[3] { 0, 1, 0 };
        }

        ColorMorph RandomColor() => new ColorMorph(GoodPalette[_random.Next() % GoodPalette.Length]);

    }
}
