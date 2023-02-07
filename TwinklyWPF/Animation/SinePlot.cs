using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwinklyWPF.Utilities;

namespace TwinklyWPF.Animation
{
    public class SinePlot
    {
        public IList<ColorMorph> palette;

        // state
        Stopwatch _stopwatch;
        Random _random = new Random();

        // const per frame
        double t;
        double xscale, zscale;

        public SinePlot()
        {
            // ugly default palette
            palette = new ColorMorph[3] {
                    new ColorMorph( 1.0, 0.0, 0.0 ),
                    new ColorMorph( 0.0, 1.0, 0.0 ),
                    new ColorMorph( 0.0, 0.0, 1.0 ),
                };

            _stopwatch = new Stopwatch();
            _stopwatch.Start();
        }

        public void Update()
        {
            t = _stopwatch.ElapsedMilliseconds * 0.001;
            xscale = 0.5 + 0.4 * Math.Cos(t * 0.2);
            zscale = 6.0 + 5.0 * Math.Cos(t * 0.11);
        }

        public double[] GetColorAt(double x, double y)
        {
            double u = x * xscale - y * 0.2;
            double v = x * 0.2 + y * xscale + t * 0.2;

            double z = Math.Sin(zscale * (Math.Sin(u) + Math.Sin(v)));

            if (z < 0)
                return ColorMorph.Mix(palette[1].GetColor(), palette[2].GetColor(), -z);
            return ColorMorph.Mix(palette[1].GetColor(), palette[0].GetColor(), z);
        }
    };
}
