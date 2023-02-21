using System;
using TwinklyWPF.Utilities;

namespace TwinklyWPF.Animation
{
    public class Life : IAnimation
    {
        RealtimeMovie _context;
        GridLayout _gridLayout;

        int[] _life;

        // cell values are in range [0..alive)
        // cell values < alive are dead; >= alive are living.
        const int alive = 256;

        Random _random = new Random();

        public readonly static double[] Black = new double[3] { 0, 0, 0 };

        public Life()
        {
        }

        public string Name => "Life";

        public void Initialize(RealtimeMovie context)
        {
            _context = context;

            // TODO: verify that at least one grid is part of the layout
            // TODO: enable dynamic grid size

            _gridLayout = _context.GetGridLayout();

            _life = new int[_gridLayout.indices.Length];
            for (int i = 0; i < _life.Length; ++i)
                _life[i] = (_random.Next() % (2*alive));

            _context._randomBlackProbability = 0;
        }

        public void Draw(byte[] _frameData)
        {
            var colors = _context.GetPaletteSnapshot();

            var chromaPower = _context.Piano.ChromaPower();

            var livingColor   = colors[0];
            var thrivingColor = colors[1];
            var dyingColor    = colors[2];

            int w = _gridLayout.width;
            int n = _gridLayout.indices.Length;

            var life2 = new int[n];

            for (int j = 0; j < n; ++j)
            {
                int sum = (_life[(j + n - w - 1) % n] >= alive ? 1 : 0)
                        + (_life[(j + n - w    ) % n] >= alive ? 1 : 0)
                        + (_life[(j + n - w + 1) % n] >= alive ? 1 : 0)
                        + (_life[(j + n     - 1) % n] >= alive ? 1 : 0)
                        + (_life[(j + n     + 1) % n] >= alive ? 1 : 0)
                        + (_life[(j + n + w - 1) % n] >= alive ? 1 : 0)
                        + (_life[(j + n + w    ) % n] >= alive ? 1 : 0)
                        + (_life[(j + n + w + 1) % n] >= alive ? 1 : 0);
                if (_life[j] >= alive)
                    life2[j] = (sum == 2 || sum == 3) ? Math.Min(2 * alive, _life[j] + 1) : alive - 1;
                else
                    life2[j] = (sum == 3) ? alive : Math.Max(0, _life[j] - 1);

                if (chromaPower?.Length >= 12 && chromaPower[j % 12] > 0.1)
                    life2[j] = alive * 2 - 1;

                var color = (life2[j] >= alive)
                    ? ColorMorph.Mix(livingColor, thrivingColor, (double)(life2[j] - alive) / alive)
                    : ColorMorph.Mix(Black, dyingColor, (double)life2[j] / alive);

                _context.SetFrameDataRGB(3 * _gridLayout.indices[j], color);
            }

            _context.CopyFillFrameData(3 * life2.Length);

            life2.CopyTo(_life, 0);
        }
    }
}
