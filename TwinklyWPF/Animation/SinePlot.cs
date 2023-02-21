using System;
using TwinklyWPF.Utilities;

namespace TwinklyWPF.Animation
{
    public class SinePlot : IAnimation
    {
        RealtimeMovie _context;

        public string Name => "Neapolitan";

        public SinePlot()
        {
        }

        public void Initialize(RealtimeMovie context)
        {
            _context = context;

            if (_context.CurrentPalette.Count != 3)
            {
                _context.RandomizePalette(3);
            }
        }

        public void Draw(byte[] _frameData)
        {
            double t = _context.CurrentTime;
            var colors = _context.GetPaletteSnapshot();

            double xscale = 0.5 + 0.4 * Math.Cos(t * 0.2);
            double zscale = 6.0 + 5.0 * Math.Cos(t * 0.11);

            int fi = 0;
            for (int j = 0; j < _context.Layout.coordinates.Length; ++j)
            {
                if (_context.Layout.coordinates[j].z == 3 || _context.Layout.coordinates[j].z == 11) { fi += 3; continue; }

                double x = _context.Layout.coordinates[j].x;
                double y = _context.Layout.coordinates[j].y;
                double[] color;
                {

                    double u = x * xscale - y * 0.2;
                    double v = x * 0.2 + y * xscale + t * 0.2;

                    double z = Math.Sin(zscale * (Math.Sin(u) + Math.Sin(v)));

                    color = (z < 0)
                                ? ColorMorph.MixSaturated(colors[1], colors[2], -z)
                                : ColorMorph.MixSaturated(colors[1], colors[0], z);
                }

                // if a note has recently been played...
                if (/*j < 60 &&*/ _context.Piano.IdleTime < _context.Settings.IdleTimeout)
                {
                    const double step = 0.3;
                    double z = 0;
                    var chromaPower = _context.Piano.ChromaPower();
                    const double octavex = 12 * step;
                    double px = 0.33 * step;
                    foreach (var p in chromaPower)
                    {
                        var w = p * Waveform.SpacedTriangle(x - px, octavex, 5);
                        z += w;

                        px += step;
                    }

                    z = Math.Min(1.0, z);

                    // shows notes in a more quantum way
                    //var z = Piano.ChromaPower()[j % 12];
                    color[0] *= z;// + Piano.BassBump * 5;
                    color[1] *= z;
                    color[2] *= z;// + Piano.BassBump;
                }

                fi = _context.SetFrameDataRGB(fi, color);
            }
        }

    }
}
