using NAudio.Midi;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using Twinkly_xled.JSONModels;
using TwinklyWPF.Utilities;
using static TwinklyWPF.Piano;

namespace TwinklyWPF
{
    public class RealtimeMovie: INotifyPropertyChanged
    {
        private System.Timers.Timer _frameTimer;
        protected Stopwatch _stopwatch;
        protected double CurrentTime => _stopwatch.ElapsedMilliseconds* 0.001;

        public int FrameCounter { get; private set; }

        public byte[] FrameData => _frameData;
        protected byte[] _frameData;

        Layout _layout;
        public Layout Layout
        {
            get { return _layout; }
            set
            {
                _layout = value;
                OnLayoutChanged();
            }
        }

        Rect _layoutBounds;

        Random _random = new Random();

        public Piano Piano;
        public int Inputs = 0;

        protected double[] KeysDownTimes = new double[3];
        protected double[] KeysUpTimes = new double[3];

        public int ColorMode = 1;

        public readonly static double[] Black = new double[3] { 0, 0, 0 };
        public readonly static double[] WarmWhite = new double[3] { 1.0, 0.9, 0.5 };
        public readonly static double[] White = new double[3] { 1, 1, 1 };

        public readonly static double[][] GoodPalette = { 
            new double[3] { 1.0, 0.0, 0.1 },    // magenta
            new double[3] { 1.0, 0.1, 0.0 },    // orangered
            new double[3] { 1.0, 0.3, 0.0 },    // orange
            new double[3] { 1.0, 0.5, 0.0 },    // gold
            new double[3] { 1.0, 0.7, 0.0 },    // basically yellow
            new double[3] { 0.5, 1.0, 0.0 },    // yellow-green
            new double[3] { 0.1, 1.0, 0.0 },    // emerald green
            new double[3] { 0.0, 1.0, 0.1 },    // spring green
            new double[3] { 0.0, 0.5, 1.0 },    // light blue
            new double[3] { 0.0, 0.1, 1.0 },    // blue blue
            new double[3] { 0.3, 0.0, 1.0 },    // purple
            new double[3] { 1.0, 0.0, 0.5 },    // hot pink
            //WarmWhite
        };


        ColorMorph[] _currentPalette = {
            new ColorMorph( 1.0, 0.4, 0.0 ),    // gold
            new ColorMorph( 0.5, 1.0, 0.0 ),    // yellow-green
            new ColorMorph( 0.0, 0.5, 1.0 ),    // light blue
        };

        class WalkerGroup
        {
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

            public WalkerGroup(int n, double min, double max)
            {
                walkers = new Walker[n];
                minx = min;
                maxx = max;
                extendedmin = minx - 0.1 * (maxx - minx);
                extendedmax = maxx + 0.1 * (maxx - minx);

                for (int i=0; i<n; ++i)
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
                foreach(var walker in walkers)
                {
                    sz += String.Format("{0:0.00} {1:0.00}, ", walker.x, walker.velocity);
                }

                return sz;
            }

            double _lastFrameTime=0;

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
                            walkers[i].color.SetTarget(White);
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
                foreach(var b in walkers)
                {
                    if(b.x >= x)
                    {
                        if(a == null)
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
        WalkerGroup _walkerGroup;

        class SinePlot
        {
            public ColorMorph[] palette;

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
        SinePlot _sinePlot;

        public RealtimeMovie()
        {
        }

        public bool Initialized { get; private set; } = false;

        void InitializeDefaultLayout(XYZ[] coordinates, int startOffset, int n)
        {
            const double s = 0.3;   // default spacing
            double startx = (startOffset % 600 + 10) * s;
            var xyz = new XYZ { x = startx, y = 2.0, z = 0.0 };
            for (int j = 0; j < n; ++j)
            {
                coordinates[j + startOffset] = xyz;
                xyz.x += s;
            }
        }

        void InitializeHouseLayout(XYZ[] coordinates, int startOffset)
        {
            Debug.Assert(coordinates.Length >= startOffset + 600);

            int zone = 0;
            var xyz = new XYZ { x = 0, y = 3.9, z = 0.0 };
            const double s = 0.1;   // default spacing

            for (int j = 0, zi = 0; j < 600; ++j, ++zi)
            {
                switch (j)
                {
                    case 90: ++zone; xyz.z = zone; zi = 0; break;  // 1: short run
                    case 136: ++zone; xyz.z = zone; zi = 0; break;  // 2: main
                    case 187: ++zone; xyz.z = zone; zi = 0; break;  // 3: dead
                    case 198: ++zone; xyz.z = zone; zi = 0; break;  // 4: doorframe L down
                    case 224: ++zone; xyz.z = zone; zi = 0; break;  // 5: doorframe L up
                    case 248: ++zone; xyz.z = zone; zi = 0; break;  // 6: doorframe top
                    case 271: ++zone; xyz.z = zone; zi = 0; break;  // 7: doorframe R down
                    case 300: ++zone; xyz.z = zone; zi = 0; xyz = new XYZ { x = 0.5 * s, y = 3.9 - 0.5 * s, z = 5.0 }; break; // string 2
                    case 395: ++zone; xyz.z = zone; zi = 0; break;  // 9: short run
                    case 443: ++zone; xyz.z = zone; zi = 0; break;  // 10: main
                    case 557: ++zone; xyz.z = zone; zi = 0; break;  // 11: leftover/downspout
                    case 571: ++zone; xyz.z = zone; zi = 0; break;  // 12: downspout
                }

                coordinates[j + startOffset] = xyz;

                switch (zone)
                {
                    case 0: // back main #1
                    case 1:
                    case 2:
                        xyz.x += s;
                        break;
                    case 3: // link to doorframe (always off)
                        xyz.x += 0.8 * s;
                        xyz.y -= 0.5 * s;
                        break;
                    case 4: // doorframe L down
                        xyz.y -= s;
                        break;
                    case 5: // doorframe L up
                        xyz.y += s;
                        break;
                    case 6: // doorframe top
                        xyz.x += s;
                        break;
                    case 7: // doorframe R down
                        xyz.y -= s;
                        break;
                    case 8: // strand #2 back main
                        xyz.x += s * 90 / 95;
                        break;
                    case 9:
                        xyz.x += s * 46 / 48;
                        break;
                    case 10:
                        xyz.x += s;
                        break;
                    case 11: // hanging end (always off)
                        xyz.x -= 0.8 * s;
                        xyz.y -= 0.1 * s;
                        break;
                    case 12: // downspout
                        xyz.y -= s;
                        break;
                }
            }
        }

        async Task Initialize()
        {
            if (Initialized) return;

            // first make sure we are authorized and have gestalt for all devices
            await ApiSemaphore.WaitAsync();

            try
            {
                foreach (var device in Devices)
                {
                    if (device.Gestalt == null)
                        await device.Load();

                    if (device.LedConfig == null)
                        await device.UpdateAuthModels();

                    if (!device.CurrentMode_Realtime)
                        await device.ChangeMode("rt");
                }
            }
            finally
            {
                ApiSemaphore.Release();
            }

            // initialize frame buffer, layout

            int frameSize = 0;
            int n = 0;
            foreach (var device in Devices)
            {
                frameSize += device.Gestalt.bytes_per_led * device.Gestalt.number_of_led;
                n += device.Gestalt.number_of_led;
            }
            App.Log($"RT movie: {n} LEDs / {frameSize} bytes over {Devices.Count()} devices");

            _frameData = new byte[frameSize];

            //_random.NextBytes(_frameData);

            Layout houseLayout = new Layout { aspectXY=0, aspectXZ=0, source="2d", synthesized=true };
            houseLayout.coordinates = new XYZ[n];

            int offset = 0;
            foreach (var device in Devices)
            {
                if (device.Gestalt.number_of_led == 600)
                    InitializeHouseLayout(houseLayout.coordinates, offset);
                else
                    InitializeDefaultLayout(houseLayout.coordinates, offset, device.Gestalt.number_of_led);
                offset += device.Gestalt.number_of_led;
            }

            this.Layout = houseLayout;

            // initialze piano listeners
            if (Piano != null)
            {
                Piano.PianoKeyDownEvent += HandlePianoKeyDownEvent;
                Piano.PianoIdleEvent += Piano_PianoIdleEvent;
            }

            // done
            Initialized = true;
        }

        private void OnLayoutChanged()
        {
            Rect bounds = Rect.Empty;
            foreach (var point in Layout.coordinates)
            {
                bounds.Union(new System.Windows.Point(point.x, point.y));
            }
            _layoutBounds = bounds;

            _walkerGroup = new WalkerGroup(8, _layoutBounds.Left, _layoutBounds.Right);
            _sinePlot = new SinePlot { palette = _currentPalette };
        }


        struct SpatialEvent
        {
            public NoteEvent noteEvent;
            public double x, y;
            public double t;
        }
        SpatialEvent[] _lastNotes = new SpatialEvent[12];
        int _lastNotesIndex = 0;

        private int _nextColorToChange = 0; // should be treated as static in this function
        private void HandlePianoKeyDownEvent(object s, EventArgs evt_)
        {
            if (!Running)
                return;

            var evt = (PianoKeyDownEventArgs)evt_;

            // change one of the palette colors to the played tone color
            int colorIndex = evt.NoteEvent.NoteNumber % GoodPalette.Length;
            _currentPalette[_nextColorToChange].SetTarget(GoodPalette[colorIndex], 0.1);
            _nextColorToChange = (_nextColorToChange + 1) % _currentPalette.Length;

            // add note to circular buffer.
            // we're not worried about circular buffer overruns here (low stakes)
            _lastNotes[_lastNotesIndex] = new SpatialEvent { 
                t = CurrentTime, 
                noteEvent = evt.NoteEvent, 
                x =       9.0*(_random.NextDouble()-0.5), 
                y = 1.0 + 2.0*(_random.NextDouble()-0.5)
            };
            _lastNotesIndex = (_lastNotesIndex + 1) % _lastNotes.Length;
        }

        double _timeOfLastIdleEvent = double.MinValue;
        private void Piano_PianoIdleEvent(object sender, EventArgs evt_)
        {
            // every 30 seconds the piano is idle, randomly change some/all of the colors

            var evt = (PianoIdleEventArgs)evt_;
            if (evt.CurrentTime - evt.LastNoteTime > 30.0 && evt.CurrentTime - _timeOfLastIdleEvent > 30.0)
            {
                _timeOfLastIdleEvent = evt.CurrentTime;

                if (_random.Next() % 5 < 1)
                {
                    // grayscale palette
                    for (int i = 0; i < _currentPalette.Length; ++i)
                    {
                        double brightness = _random.NextDouble();
                        _currentPalette[i].SetTarget(ColorMorph.Mix(Black, WarmWhite, brightness));
                    }
                }
                else
                {
                    var color = ColorMorph.HsvToRgb(_random.NextDouble(), 1.0, 1.0);
                    _currentPalette[_nextColorToChange].SetTarget(color);
                    _nextColorToChange = (_nextColorToChange + 1) % _currentPalette.Length;
                }

            }
        }


        public void KeyDown(int keyId)
        {
            KeysDownTimes[keyId % KeysDownTimes.Length] = _stopwatch.ElapsedMilliseconds * 0.001;
            KeysUpTimes[keyId % KeysDownTimes.Length] = 0;
        }

        public void KeyUp(int keyId)
        {
            KeysDownTimes[keyId % KeysDownTimes.Length] = 0;
            KeysUpTimes[keyId % KeysDownTimes.Length] = _stopwatch.ElapsedMilliseconds * 0.001;
        }

        public void RandomizePalette()
        {
            for (int i = 0; i < _currentPalette.Length; ++i)
            {
                int j = (_random.Next() & 0xFFFF) % GoodPalette.Length;
                _currentPalette[i].SetTarget(GoodPalette[j]);
            }
        }

        protected virtual void DrawFrame()
        {
            double t = _stopwatch.ElapsedMilliseconds * 0.001;
            int fi = 0; // byte offset to start filling _frameData
            int n = Layout.coordinates.Length;

            Debug.Assert(_frameData.Length == n * 3);

            switch (ColorMode % 8)
            {
                case 1: // 3-color palette changing sinusoidal
                    {
                        _sinePlot.Update();

                        for (int j = 0; j < Layout.coordinates.Length; ++j)
                        {
                            if (Layout.coordinates[j].z == 3 || Layout.coordinates[j].z == 11) { fi += 3; continue; }

                            double x = Layout.coordinates[j].x;
                            double y = Layout.coordinates[j].y;

                            double[] color = _sinePlot.GetColorAt(x, y);

                            if (/*j < 60 &&*/ Piano.CurrentTime - Piano.LastNoteTime < 30)
                            {
                                const double step = 0.3;
                                double v = 0;
                                var chromaPower = Piano.ChromaPower();
                                const double octavex = 12 * step;
                                double px = 0.33*step;
                                foreach (var p in chromaPower)
                                {
                                    var w = p * Waveform.SpacedTriangle(x - px, octavex, 5);
                                    v += w;

                                    px += step;
                                }

                                v = Math.Min(1.0, v);

                                // shows notes in a more quantum way
                                //var v = Piano.ChromaPower()[j % 12];
                                color[0] *= v;// + Piano.BassBump * 5;
                                color[1] *= v;
                                color[2] *= v;// + Piano.BassBump;
                            }

                            _frameData[fi++] = (byte)(Math.Clamp(255.5 * color[0], 0, 255));
                            _frameData[fi++] = (byte)(Math.Clamp(255.5 * color[1], 0, 255));
                            _frameData[fi++] = (byte)(Math.Clamp(255.5 * color[2], 0, 255));
                        }
                    }
                    return;

                case 2: // simple chromatic abberration
                {
                    for (int j = 0; j < Layout.coordinates.Length; ++j)
                    {
                        if (Layout.coordinates[j].z == 3 || Layout.coordinates[j].z == 11) { fi += 3; continue; }

                        double v = Math.Sin(0.5 * Layout.coordinates[j].x + 1.5 * Layout.coordinates[j].y + 1.1 * t);

                        _frameData[fi++] = (byte)(Math.Clamp(255.5 * v, 0, 255));
                        _frameData[fi++] = (byte)(Math.Clamp(255.5 * v, 0, 255));
                        _frameData[fi++] = (byte)(Math.Clamp(255.5 * v, 0, 255));
                    }
                }
                return;

                case 3: // slow panning ribbons
                    {
                        var colors = new double[3][];
                        colors[0] = _currentPalette[0].GetColor();
                        colors[1] = _currentPalette[1].GetColor();
                        colors[2] = _currentPalette[2].GetColor();

                        double[] angles = new double[4] { 
                            t * Piano.Knobs[4],//0.1, 
                            t * Piano.Knobs[5],//-0.13, 
                            t * Piano.Knobs[6],//0.31, 
                            t * Piano.Knobs[7],//0.01
                        };
                        double[] coss = new double[angles.Length];
                        double[] sins = new double[angles.Length];
                        for(int k=0; k<angles.Length; ++k)
                        {
                            coss[k] = Math.Cos(angles[k]);
                            sins[k] = Math.Sin(angles[k]);
                        }

                        for (int j = 0; j < Layout.coordinates.Length; ++j)
                        {
                            if (Layout.coordinates[j].z == 3 || Layout.coordinates[j].z == 11) { fi += 3; continue; }

                            double x = Layout.coordinates[j].x - 20.5;
                            double y = Layout.coordinates[j].y - 10.0;

                            //double v = Math.Sin(0.5 * x + 1.5 * y + 1.1 * t);
                            //v *= Math.Abs(v);
                            //double w = Math.Sin(2.3 * x + 6.9 * y - 2.3 * t);
                            //w *= Math.Abs(w);

                            const double wavelength = 10;

                            double u = Waveform.SpacedTriangle(x+y + t * 0.3, wavelength, 4*(1-Piano.Knobs[0]));
                            double v = Waveform.SpacedTriangle(x+y - t * 0.6, wavelength, 4*(1-Piano.Knobs[1]));
                            double w = Waveform.SpacedTriangle(x+y + t * 0.7, wavelength, 4*(1-Piano.Knobs[2]));

                            _frameData[fi++] = (byte)(Math.Clamp(255.5 * (v*colors[0][0] + w*colors[1][0] + u*colors[2][0]), 0, 255));
                            _frameData[fi++] = (byte)(Math.Clamp(255.5 * (v*colors[0][1] + w*colors[1][1] + u*colors[2][1]), 0, 255));
                            _frameData[fi++] = (byte)(Math.Clamp(255.5 * (v*colors[0][2] + w*colors[1][2] + u*colors[2][2]), 0, 255));

                            var chromaPower = Piano.ChromaPower();
                            if(_random.NextDouble() < chromaPower[j%12])
                            {
                                for (int k = fi - 3; k < fi; ++k)
                                    _frameData[k] = 255;
                            }
                        }
                    }
                    return;

                case 4: // chroma key ripples
                    {
                        for (int j = 0; j < Layout.coordinates.Length; ++j)
                        {
                            if (Layout.coordinates[j].z == 3 || Layout.coordinates[j].z == 11) { fi += 3; continue; }

                            double x = Layout.coordinates[j].x - 20.5;
                            double y = Layout.coordinates[j].y -  7.0;

                            double r=0, g=0, b=0;

                            if (Piano.CurrentTime - Piano.LastNoteTime > 30)
                            {
                                // no interactivity for 30 seconds

                                var colors = new double[3][];
                                colors[0] = _currentPalette[0].GetColor();
                                colors[1] = _currentPalette[1].GetColor();
                                colors[2] = _currentPalette[2].GetColor();

                                const double f = 6 * Math.PI / 300.0;
                                double v = 1.5 * Math.Sin(f * (x - y) + t) * Math.Sin(f * (x + y) + t);
                                //r = v * (v < 0 ? colors[1] : colors[0]);
                            }
                            else
                            {
                                foreach (var evt in _lastNotes)
                                {
                                    if (evt.noteEvent != null)
                                    {
                                        var age = t - evt.t;
                                        if (age >= 0)
                                        {
                                            const double velocity = 5.0;    // units/second
                                            var dx = evt.x - x;
                                            var dy = evt.y - y;
                                            var dist = Math.Sqrt(dx * dx + dy * dy);
                                            var w = age * velocity - dist;
                                            if (w > 0)
                                            {
                                                var decay = 5.0 * Waveform.expgrowth(evt.noteEvent.Velocity / 127.0, age, -1);
                                                var v = decay * Waveform.SpacedTriangle(w * 2, 6.0, 2.0);
                                                var color = GoodPalette[evt.noteEvent.NoteNumber % GoodPalette.Length];
                                                r += v * color[0];
                                                g += v * color[1];
                                                b += v * color[2];
                                            }
                                        }
                                    }
                                }
                            }
                            if (_random.NextDouble() < 0.01)
                            {
                                //for (int k = fi - 3; k < fi; ++k)
                                //  _frameData[k] += 120;
                                b += 0.4;
                            }
                            _frameData[fi++] = (byte)(Math.Clamp(255.5 * r, 0, 255));
                            _frameData[fi++] = (byte)(Math.Clamp(255.5 * g, 0, 255));
                            _frameData[fi++] = (byte)(Math.Clamp(255.5 * b, 0, 255));

                            var chromaPower = Piano.ChromaPower();
                        }
                    }
                    return;

                case 5: // b gradients
                    {
                        _walkerGroup.Update();
                        for (int j = 0; j < Layout.coordinates.Length; ++j)
                        {
                            if (Layout.coordinates[j].z == 3 || Layout.coordinates[j].z == 11) { fi += 3; continue; }

                            double x = Layout.coordinates[j].x;

                            double[] color = _walkerGroup.GetColorAt(x);

                            var r = (byte)(Math.Clamp(255.5 * color[0], 0, 255));
                            var g = (byte)(Math.Clamp(255.5 * color[1], 0, 255));
                            var b = (byte)(Math.Clamp(255.5 * color[2], 0, 255));
                            _frameData[fi++] =  r;
                            _frameData[fi++] =  g;
                            _frameData[fi++] = b;
                        }
                    }
                    return;

                case 6: // mapping calibration pattern
                    {
                        var color = new byte[3];
                        for (int j = 0; j < Layout.coordinates.Length; ++j)
                        {
                            switch ((int)Math.Round(Layout.coordinates[j].z))
                            {

                                case 0: color = new byte[3] { 255, 255, 255 }; break; // 
                                case 1: color = new byte[3] { 255, 0, 0 }; break; // 
                                case 2: color = new byte[3] { 255, 255, 255 }; break; // 
                                case 3: color = new byte[3] { 0, 0, 0 }; break; // dead
                                case 4: color = new byte[3] { 255, 255, 0 }; break; // doorframe L
                                case 5: color = new byte[3] { 0, 127, 255 }; break; // doorframe L
                                case 6: color = new byte[3] { 255, 0, 0 }; break; // doorframe T
                                case 7: color = new byte[3] { 0, 255, 0 }; break; // doorframe R
                                case 8: color = new byte[3] { 0, 255, 255 }; break; // back
                                case 9: color = new byte[3] { 0, 0, 255 }; break; // short
                                case 10: color = new byte[3] { 255, 0, 255 }; break; // main
                                case 11: color = new byte[3] { 0, 0, 0 }; break; // leftover/downspout
                                default: color = new byte[3] { 125, 255, 255 }; break;
                            }

                            _frameData[fi++] = color[0];
                            _frameData[fi++] = color[1];
                            _frameData[fi++] = color[2];

                        }

                    }
                    return;

                case 7: // color calibration pattern
                    {
                        double offset = (t % 10.0) / 10.0 * 8.0;
                        for (int j = 0; j < Layout.coordinates.Length; ++j)
                        {
                            if (Layout.coordinates[j].z == 3 || Layout.coordinates[j].z == 11) { fi += 3; continue; }

                            double x = Layout.coordinates[j].x + 2.0 * Layout.coordinates[j].y;

                            int coloridx = (int)((offset + x) / 8.0 * GoodPalette.Length);
                            var color = GoodPalette[coloridx % GoodPalette.Length];
                            var r = (byte)(Math.Clamp(255.5 * color[0], 0, 255));
                            var g = (byte)(Math.Clamp(255.5 * color[1], 0, 255));
                            var b = (byte)(Math.Clamp(255.5 * color[2], 0, 255));
                            _frameData[fi++] = r;
                            _frameData[fi++] = g;
                            _frameData[fi++] = b;
                        }

                    }
                    return;
            }

            // reasonable, but singularity gets crazy
            //double sc = Math.Tan(t * 0.1);
            //sc = sc * sc * sc;
            // oscillate between 1/0.01 and 1/100.01
            //double sc = 1.0 / (20.1 + 20.0 * Math.Cos(t * 0.1));
            //double sc = 6.283 / 19.76 ;  // wl = 10 pixels
            double min = 5.0, max = 40.0;
            double osc = ((min + max) * 0.5 + (max - min) * 0.5 * Math.Cos(t * 0.1));


            for (int j = 0; j < Layout.coordinates.Length; ++j)
            {
                //int v = frameData[fi];
                //frameData[fi] = (byte)(((v&1)==1)
                //    ? (frameData[fi] > 1 ? frameData[fi] - 2 : 0) 
                //    : (frameData[fi] < 254 ? frameData[fi] + 2 : 255));

                // old mapping:
                //double x = sc * (double)((fi / 3) % 300);// / _frameData.Length);

                // new mapping:
                if (Layout.coordinates[j].z == 3 || Layout.coordinates[j].z == 11)
                {
                    _frameData[fi++] = 0;
                    _frameData[fi++] = 0;
                    _frameData[fi++] = 0;
                    continue; 
                }
                double x = Layout.coordinates[j].x + 3.0 * Layout.coordinates[j].y;

                double wave = 2.0 * Math.Cos(t * 0.5 + x * 0.4397);

                double[] c = new double[3];
                c[0] = Math.Sin( x - t*0.01 + wave);
                c[1] = Math.Sin( x - t*0.02 + wave);
                c[2] = Math.Sin( x + t*0.05 + wave);

                // add some key state
                for (int k = 0; k < 3; ++k)
                {
                    if (KeysDownTimes[k] > 0)
                    {
                        c[k] = 1;
                    }
                    else if (KeysUpTimes[k] > 0)
                    {
                        // exponential decay after key is released
                        double tt = t - KeysUpTimes[k];
                        if (tt < 4.0)
                        {
                            c[k] = Math.Exp(-2.5 * tt);
                        }
                    }
                }

                _frameData[fi++] = (byte)(Math.Clamp(255.9 * c[0], 0, 255));
                _frameData[fi++] = (byte)(Math.Clamp(255.9 * c[1], 0, 255));
                _frameData[fi++] = (byte)(Math.Clamp(255.9 * c[2], 0, 255));
            }

        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion

        IEnumerable<Device> _devices;
        public IEnumerable<Device> Devices 
        { 
            get => _devices;
            set 
            {
                _devices = value;
                OnPropertyChanged();
            }
        }

        // owner can replace this semaphore
        public System.Threading.SemaphoreSlim ApiSemaphore = new System.Threading.SemaphoreSlim(1, 1);

        #region Realtime test

        public bool PreviewMode = false;

        public bool Running
        {
            get => _frameTimer != null;
            set
            {
                if (value)
                    Start().Wait();
                else
                    Stop();
                OnPropertyChanged();
            }
        }

        public void Stop()
        {
            if (_stopwatch?.ElapsedMilliseconds > 0)
                Debug.WriteLine($"FPS: {FPS}   Frames: {FrameCounter} {_stopwatch.ElapsedMilliseconds * 0.001}");

            _frameTimer?.Stop();
            _frameTimer = null;
        }

        public double FPS
        {
            get
            {
                if (_stopwatch == null)
                    return 0;
                return FrameCounter / (0.001 * _stopwatch.ElapsedMilliseconds);
            }
        }

        public async Task Start()
        {
            if (Running)
                return;

            if (!Initialized)
                await Initialize();

            _frameTimer = new System.Timers.Timer { AutoReset = true, Interval = 50 };
            _frameTimer.Elapsed += OnFrameTimerElapsed;
            _frameTimer.Start();
            _stopwatch = new Stopwatch();
            _stopwatch.Start();
            FrameCounter = 0;

        }

        private System.Threading.SemaphoreSlim _frameTimerSemaphore = new System.Threading.SemaphoreSlim(1, 1);

        private async void OnFrameTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (_frameTimer == null)
                return;

            if (!_frameTimerSemaphore.Wait(TimeSpan.Zero))
            {
                return;
            }

            DrawFrame();

            // in preview mode, we don't need to actually send the image to the devices
            if (PreviewMode)
                return;

            await ApiSemaphore.WaitAsync();

            try
            {
                int offset = 0;
                foreach (var device in Devices)
                {
                    if (device.LedConfig == null)
                        continue;

                    // devices can only receive 900 bytes of data at a time.
                    // It seems like the strings[] array defines the expected frame ranges.
                    byte fragment = 0;
                    foreach (var s in device.LedConfig.strings)
                    {
                        var n = device.Gestalt.bytes_per_led * s.length;
                        device.twinklyapi.SendFrame(_frameData, offset, n, fragment++);
                        offset += n;
                    }
                }
                Debug.Assert(offset == _frameData.Length);

                FrameCounter++;

                //if (FrameCounter % 100 == 0)
                //    OnPropertyChanged("FPS");
            }
            finally
            {
                ApiSemaphore.Release();
                _frameTimerSemaphore.Release();
            }
        }

        #endregion
    }
}
