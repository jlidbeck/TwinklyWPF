using System;
using System.Diagnostics;

namespace TwinklyWPF.Utilities
{
    [DebuggerDisplay("ColorMorph: {ColorToString(_startColor)} -> {ColorToString(_targetColor)}")]
    public class ColorMorph
    {
        double[] _startColor= new double[3];
        double[] _targetColor = null;
        Stopwatch _stopwatch = new Stopwatch();

        public double TransitionTimeMS { get; private set; } = 1800;      // ms

        #region Constructors

        public ColorMorph() { }

        public ColorMorph(double[] rgb)
        {
            _startColor = (double[])rgb.Clone();
        }

        public ColorMorph(double r, double g, double b)
        {
            _startColor[0] = r;
            _startColor[1] = g;
            _startColor[2] = b;
        }

        #endregion

        public override string ToString()
        {
            return $"{ColorToString(_startColor)} -> {ColorToString(_targetColor)} ({Progress})";
        }

        public bool InTransition => (_targetColor != null);

        public double Progress => (_stopwatch.ElapsedMilliseconds / TransitionTimeMS);

        // Set the target color for the next morph. Does not change the immediate color.
        // Animation begins immediately and completes in {time} seconds.
        public void SetTarget(double[] rgb, double time=1.8)
        {
            // If in transition...
            double palTime = _stopwatch.ElapsedMilliseconds / TransitionTimeMS;
            if (_targetColor != null && palTime < 1.0)
            {
                // setting a new target mid-transition:
                // lock in-transition color as new starting point
                for (int j = 0; j < 3; ++j)
                    _startColor[j] = palTime * _targetColor[j] + (1 - palTime) * _startColor[j];
            }

            TransitionTimeMS = time * 1000.0;
            _targetColor = (double[])rgb.Clone();
            _stopwatch.Restart();
        }

        //  Gets immediate color value.
        //  If in transition, uses linear interpolation between startColor and targetColor.
        //  Otherwise returns _currentPalette.
        public double[] GetColor()
        {
            double palTime = _stopwatch.ElapsedMilliseconds / TransitionTimeMS;

            if (_targetColor?.Length == _startColor.Length)
            {
                // in transition
                if (palTime >= 1.0)
                {
                    // color animation complete
                    _stopwatch.Stop();
                    _startColor = _targetColor;
                    _targetColor = null;
                }
                else
                {
                    // interpolate colors
                    var colors = new double[3];
                    for (int j = 0; j < 3; ++j)
                    {
                        colors[j] = palTime * _targetColor[j] + (1 - palTime) * _startColor[j];
                    }
                    return colors;
                }
            }

            return _startColor;
        }

        //  Returns target color, if currently in transition.
        //  If not in transition returns current color.
        public double[] TargetColor => InTransition ? _targetColor : _startColor;


        public static double[] Mix(double[] startColor, double[] targetColor, double t)
        {
            // interpolate colors
            var colors = new double[3];
            for (int j = 0; j < 3; ++j)
            {
                colors[j] = t * targetColor[j] + (1 - t) * startColor[j];
            }
            return colors;
        }

        public static double[] MixSaturated(double[] startColor, double[] targetColor, double t)
        {
            // interpolate colors
            var startHSV = new double[3];
            var targetHSV = new double[3];
            ColorHelper.RgbToHsv(startColor, ref startHSV);
            ColorHelper.RgbToHsv(targetColor, ref targetHSV);
            double huedist = (startHSV[0] - targetHSV[0]);
            if (huedist > 0.5)
                targetHSV[0] += 1.0;
            else if (huedist < -0.5)
                startHSV[0] += 1.0;

            var rgb = new double[3];
            ColorHelper.HsvToRgb(
                t * targetHSV[0] + (1 - t) * startHSV[0],
                t * targetHSV[1] + (1 - t) * startHSV[1],
                t * targetHSV[2] + (1 - t) * startHSV[2],
                ref rgb);

            return rgb;
        }

        public static double[] HsvLirp(double[] startHSV, double[] targetHSV, double t)
        {
            double huedist = (startHSV[0] - targetHSV[0]);
            if (huedist > 0.5)
                targetHSV[0] += 1.0;
            else if (huedist < -0.5)
                startHSV[0] += 1.0;

            var hsv = new double[3]
            {
                t * targetHSV[0] + (1 - t) * startHSV[0],
                t * targetHSV[1] + (1 - t) * startHSV[1],
                t * targetHSV[2] + (1 - t) * startHSV[2]
            };

            return hsv;
        }

        public static string ColorToString(double[] rgb)
        {
            return (rgb?.Length == 3)
                ? String.Format("{0:0.00} {0:0.00} {0:0.00}", rgb[0], rgb[1], rgb[2])
                : "null";
        }

        //  hue: 
        public static double[] HsvToRgb(double hue, double sat, double val)
        {
            double r, g, b;

            if (sat <= 0)
            {
                // Gray scale
                r = g = b = val;
                return new double[3] { val, val, val };
            }
            //else
            {
                // 0 <= i<6
                // 0.0 <= f<1.0
                hue *= 6.0;
                int i = (int)Math.Floor(hue);
                double f = hue - i;
                if (i < 0) i += 6;

                double aa = val * (1 - sat);
                double bb = val * (1 - (sat * f));
                double cc = val * (1 - (sat * (1 - f)));
                switch (i)
                {
                    default:
                    case 0:
                        r = val;
                        g = cc;
                        b = aa;
                        break;
                    case 1:
                        r = bb;
                        g = val;
                        b = aa;
                        break;
                    case 2:
                        r = aa;
                        g = val;
                        b = cc;
                        break;
                    case 3:
                        r = aa;
                        g = bb;
                        b = val;
                        break;
                    case 4:
                        r = cc;
                        g = aa;
                        b = val;
                        break;
                    case 5:
                        r = val;
                        g = aa;
                        b = bb;
                        break;
                }
            }

            return new double[3] { r, g, b };
        }

    }
}
