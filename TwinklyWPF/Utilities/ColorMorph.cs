using System;
using System.Diagnostics;

namespace TwinklyWPF.Utilities
{
    public class ColorMorph
    {
        double[] _startColor= new double[3];
        double[] _targetColor = null;
        Stopwatch _stopwatch = new Stopwatch();

        public long TransitionTime = 1800;      // ms

        #region Constructors

        public ColorMorph() { }

        public ColorMorph(double[] rgb)
        {
            _startColor = rgb;
        }

        public ColorMorph(double r, double g, double b)
        {
            _startColor[0] = r;
            _startColor[1] = g;
            _startColor[2] = b;
        }

        #endregion

        //public bool InTransition => (targetColor != null && palTime < 1.0);

        // Set the target color for the next morph. Does not change the immediate color.
        public void SetTarget(double[] rgb)
        {
            double palTime = _stopwatch.ElapsedMilliseconds / (double)TransitionTime;
            if (_targetColor != null && palTime < 1.0)
            {
                // setting a new target mid-transition:
                // lock in-transition color as new starting point
                for (int j = 0; j < 3; ++j)
                    _startColor[j] = palTime * _targetColor[j] + (1 - palTime) * _startColor[j];
            }

            _targetColor = rgb;
            _stopwatch.Restart();
        }

        //  Gets immediate color value.
        //  If in transition, uses linear interpolation between startColor and targetColor.
        //  Otherwise returns _currentPalette.
        public double[] GetColor()
        {
            double palTime = _stopwatch.ElapsedMilliseconds / (double)TransitionTime;

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

        internal static double[] Mix(double[] startColor, double[] targetColor, double t)
        {
            // interpolate colors
            var colors = new double[3];
            for (int j = 0; j < 3; ++j)
            {
                colors[j] = t * targetColor[j] + (1 - t) * startColor[j];
            }
            return colors;
        }
    }
}
