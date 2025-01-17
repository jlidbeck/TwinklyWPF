﻿using NAudio.Midi;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using Twinkly_xled.JSONModels;
using TwinklyWPF.Utilities;
using static TwinklyWPF.Piano;
using static TwinklyWPF.Utilities.Layouts;

namespace TwinklyWPF
{
    public class RealtimeMovieSettings : INotifyPropertyChanged
    {
        public int ColorMode = 9;

        // interactivity timeout
        bool _idleTimeoutEnabled = true;
        public bool IdleTimeoutEnabled
        {
            get => _idleTimeoutEnabled;
            set { _idleTimeoutEnabled = value; OnPropertyChanged(); }
        }
        
        double _idleTimeout = 30;
        public double IdleTimeout
        {
            get => _idleTimeout;
            set { _idleTimeout = value; OnPropertyChanged(); }
        }

        public double ColorMorphTime = 1.8;

        public int FrameTimerInterval = 50; // 50 ms == ~20 FPS

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion
    }

    public class RealtimeMovie: INotifyPropertyChanged
    {
        private System.Timers.Timer _frameTimer;
        protected Stopwatch _stopwatch;
        public double CurrentTime => _stopwatch.ElapsedMilliseconds * 0.001;

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
                OnPropertyChanged();
                OnLayoutChanged();
            }
        }

        Rect _layoutBounds;

        Random _random = new Random();

        public Piano Piano;

        IEnumerable<Device> _devices;
        public IEnumerable<Device> Devices
        {
            get => _devices;
            set
            {
                if (Running)
                    throw new ArgumentException("Devices can't be changed while animation is running");
                _devices = value;
                OnPropertyChanged();
            }
        }

        // timestamp of last non-MIDI user interaction
        double LastInteractionTime = double.NegativeInfinity;
        public double IdleTime => CurrentTime - LastInteractionTime;

        System.Timers.Timer idleTimer;

        RealtimeMovieSettings _settings = new RealtimeMovieSettings();
        public RealtimeMovieSettings Settings
        {
            get => _settings;
            set { 
                _settings = value; 
                OnPropertyChanged();
                OnPropertyChanged("ColorModes");
                OnPropertyChanged("ColorModeName");
            }
        }

        bool _animationNeedsInit = true;

        public List<string> ColorModes
        { 
            get;
            set;
        } = new List<string> { 
            "Old Chroma",
            "Trinity",
            "Chromatic Aberration",
            "Ribbons",
            "Chroma key ripples",
            "B gradients",
            "TestPattern",
            "Palette Test Pattern",
            "Oldschool Quantum",
            "Ambients",
            "Life"
        };

        public string ColorModeName
        {
            get => ColorModes[_settings.ColorMode];
            set
            {
                int idx = ColorModes.IndexOf(value);
                if(idx>=0)
                {
                    _settings.ColorMode = idx;
                    _animationNeedsInit = true;
                    OnPropertyChanged();
                }
            }
        }

        public void NextColorMode(int step=1)
        {
            _settings.ColorMode = (_settings.ColorMode + step + ColorModes.Count) % ColorModes.Count;
            _animationNeedsInit = true;
            LastInteractionTime = CurrentTime;
            OnPropertyChanged("ColorModeName");
        }

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


        List<ColorMorph> _currentPalette = new() {
            new ColorMorph( 1.0, 0.4, 0.0 ),    // gold
            new ColorMorph( 0.5, 1.0, 0.0 ),    // yellow-green
            new ColorMorph( 0.0, 0.5, 1.0 ),    // light blue
        };

        public List<ColorMorph> CurrentPalette => _currentPalette;

        Animation.WalkerGroup _walkerGroup;

        Animation.SinePlot _sinePlot;

        double[] _noise;

        int[] _life, _life2;
        int[] _lifeGridIndex;

        public RealtimeMovie()
        {
        }

        //  Initializes devices and allocates framedata to match layout
        //  This should only be invoked from Start() to avoid semaphore lockouts
        async Task Initialize()
        {
            // first make sure we are authorized and have gestalt for all devices
            await ApiSemaphore.WaitAsync();

            try
            {
                // As necessary, load device metadata, reauthenticate, and switch mode to RT

                foreach (var device in Devices)
                {
                    if (device.Gestalt == null)
                        await device.Load();

                    await device.Login();

                    // get device details only if needed--this step can take a while (200-400 ms)
                    if(device.LedConfig == null)
                        await device.UpdateAuthModels(true);

                    // always set mode because device will automatically switch out of RT mode when idle
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

            Layout layout = new Layout { aspectXY=0, aspectXZ=0, source="2d", synthesized=true };
            layout.coordinates = new XYZ[n];

            int offset = 0;
            foreach (var device in Devices)
            {
                var deviceMetadata = App.Current.Settings.GetDeviceMetadata(device.UniqueName);
                var map = deviceMetadata.GetValueOrDefault("MappingName");
                if (map == null)
                {
                    if (device.Gestalt.number_of_led == 600)
                    {
                        map = "Grid600";
                        //map = "House";
                        deviceMetadata["MappingName"] = map;
                    }
                }

                switch (map)
                {
                    case "Grid600":
                        Layouts.Initialize600GridLayout(layout.coordinates, offset);
                        break;

                    case "House":
                        Layouts.InitializeHouseLayout(layout.coordinates, offset);
                        break;

                    default:
                        Layouts.InitializeDefaultLayout(layout.coordinates, offset, device.Gestalt.number_of_led);
                        break;
                }

                offset += device.Gestalt.number_of_led;
            }

            this.Layout = layout;
        }

        private void OnLayoutChanged()
        {
            Rect bounds = Rect.Empty;
            foreach (var point in Layout.coordinates)
            {
                bounds.Union(new System.Windows.Point(point.x, point.y));
            }
            _layoutBounds = bounds;

            _walkerGroup = new Animation.WalkerGroup(8, _layoutBounds.Left, _layoutBounds.Right, GoodPalette);
            _sinePlot = new Animation.SinePlot { palette = _currentPalette };
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
        int _randomBlackProbability = 0;

        private void HandlePianoKeyDownEvent(object s, EventArgs evt_)
        {
            if (!Running)
                return;

            var evt = (PianoKeyDownEventArgs)evt_;

            // change one of the palette colors to the played tone color
            int colorIndex = evt.NoteEvent.NoteNumber % GoodPalette.Length;
            //if (colorIndex < 0) colorIndex += GoodPalette.Length;            
            _currentPalette[_nextColorToChange % _currentPalette.Count].SetTarget(GoodPalette[colorIndex], 0.1);
            _nextColorToChange = (_nextColorToChange + 1) % _currentPalette.Count;

            // add note to circular buffer.
            // we're not worried about circular buffer overruns here (low stakes)
            _lastNotes[_lastNotesIndex] = new SpatialEvent { 
                t = CurrentTime, 
                noteEvent = evt.NoteEvent, 
                x = -2.0 + 4.0 * (_random.NextDouble()), 
                y = -0.2 + 0.4 * (_random.NextDouble())
            };
            _lastNotesIndex = (_lastNotesIndex + 1) % _lastNotes.Length;
        }


        // the no-piano idle timer
        private void OnIdleTimerElapsed(object sender, ElapsedEventArgs e)
        {
            var t = CurrentTime - LastInteractionTime;
            if (t > 3)
            {
                Piano_PianoIdleEvent(this, new PianoIdleEventArgs { LastInteractionTime = LastInteractionTime, CurrentTime = CurrentTime });
            }
        }

        // an IdleEvent means the RT has changed its settings due to user inactivity
        double _timeOfLastIdleEvent = double.NegativeInfinity;
        public double IdleEventTime => CurrentTime - _timeOfLastIdleEvent;

        private void Piano_PianoIdleEvent(object sender, EventArgs evt_)
        {
            // every 30 seconds the piano is idle, randomly change some/all of the colors

            var evt = (PianoIdleEventArgs)evt_;

            // If it's been 30 seconds since any MIDI or GUI input has been received
            // AND 30 seconds since the last idle event...
            if (_settings.IdleTimeoutEnabled &&
                Piano.IdleTime > _settings.IdleTimeout &&
                IdleTime       > _settings.IdleTimeout &&
                IdleEventTime  > _settings.IdleTimeout)
            {
                _timeOfLastIdleEvent = CurrentTime;

                if (_random.Next() % 50 < 1)
                {
                    // grayscale palette
                    for (int i = 0; i < _currentPalette.Count; ++i)
                    {
                        double brightness = _random.NextDouble();
                        _currentPalette[i].SetTarget(ColorMorph.Mix(Black, WarmWhite, brightness), _settings.ColorMorphTime);
                    }
                }
                else
                {
                    RandomizeOneColor();
                }

            }
        }

        //  Changes one of the palette colors. If {colorIndex} is negative, changes the oldest color.
        public void RandomizeOneColor(int colorIndex=-1)
        {
            if (colorIndex >= 0)
                _nextColorToChange = colorIndex;

            _nextColorToChange %= _currentPalette.Count;

            double[] color;
            if (_randomBlackProbability > 0 && _random.Next() % 10000 < _randomBlackProbability)
            {
                color = Black;
            }
            else
            {
                //var color = ColorMorph.HsvToRgb(_random.NextDouble(), 1.0, 1.0);
                color = GoodPalette[_random.Next() % GoodPalette.Length];
            }
            _currentPalette[_nextColorToChange].SetTarget(color, _settings.ColorMorphTime);

            _nextColorToChange++;

            LastInteractionTime = CurrentTime;
        }

        public void RandomizePalette(int numEntries=-1)
        {
            if (numEntries > 0)
            {
                while (_currentPalette.Count > numEntries)
                {
                    _currentPalette.RemoveAt(numEntries);
                }

                while (_currentPalette.Count < numEntries)
                {
                    _currentPalette.Add(new ColorMorph(WarmWhite));
                }
            }

            for (int i = 0; i < _currentPalette.Count; ++i)
            {
                int j = (_random.Next() & 0xFFFF) % GoodPalette.Length;
                _currentPalette[i].SetTarget(GoodPalette[j], _settings.ColorMorphTime);
            }
            LastInteractionTime = CurrentTime;
        }

        public void Purple()
        {
            _currentPalette[0].SetTarget(new double[3] { 0.2, 0.0, 0.4 }, _settings.ColorMorphTime);
            _currentPalette[1].SetTarget(new double[3] { 0.1, 0.0, 0.4 }, _settings.ColorMorphTime);
            _currentPalette[2].SetTarget(new double[3] { 0.0, 0.1, 0.3 }, _settings.ColorMorphTime);
            LastInteractionTime = CurrentTime;
        }

        protected virtual void DrawFrame()
        {
            double t = _stopwatch.ElapsedMilliseconds * 0.001;
            int fi = 0; // byte offset to start filling _frameData
            int n = Layout.coordinates.Length;

            Debug.Assert(_frameData.Length == n * 3);

            switch (_settings.ColorMode)
            {
                case 1: // trinity: 3-color palette changing sinusoidal
                    {
                        if (_currentPalette.Count != 3)
                        {
                            RandomizePalette(3);
                        }

                        _sinePlot.Update();

                        for (int j = 0; j < Layout.coordinates.Length; ++j)
                        {
                            if (Layout.coordinates[j].z == 3 || Layout.coordinates[j].z == 11) { fi += 3; continue; }

                            double x = Layout.coordinates[j].x;
                            double y = Layout.coordinates[j].y;

                            double[] color = _sinePlot.GetColorAt(x, y);

                            // if a note has recently been played...
                            if (/*j < 60 &&*/ Piano.CurrentTime - Piano.LastInteractionTime < _settings.IdleTimeout)
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

                        var colors = new double[3][];
                        colors[0] = _currentPalette[0].GetColor();
                        colors[1] = _currentPalette[1].GetColor();
                        colors[2] = _currentPalette[2].GetColor();

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

                            double u = Waveform.SpacedTriangle(x + Math.Sin(y+t*0.4) + t * 0.3, wavelength, 4*(1-Piano.Knobs[0]));
                            double v = Waveform.SpacedTriangle(x + Math.Sin(y+t*0.8) - t * 0.6, wavelength, 4*(1-Piano.Knobs[1]));
                            double w = Waveform.SpacedTriangle(x + Math.Sin(y+t*1.4) + t * 0.7, wavelength, 4*(1-Piano.Knobs[2]));

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
                        var colors = new double[3][];
                        colors[0] = _currentPalette[0].GetColor();
                        colors[1] = _currentPalette[1].GetColor();
                        colors[2] = _currentPalette[2].GetColor();

                        for (int j = 0; j < Layout.coordinates.Length; ++j)
                        {
                            if (Layout.coordinates[j].z == 3 || Layout.coordinates[j].z == 11) { fi += 3; continue; }

                            double x = Layout.coordinates[j].x;
                            double y = Layout.coordinates[j].y;

                            double r=0, g=0, b=0;

                            if (Piano.CurrentTime - Piano.LastInteractionTime > _settings.IdleTimeout)
                            {
                                // no interactivity for 30 seconds


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
                                            const double velocity = 1.5;    // units/second
                                            var dx = evt.x - x;
                                            var dy = evt.y - y;
                                            var dist = Math.Sqrt(dx * dx + dy * dy);
                                            var w = age * velocity - dist;
                                            if (w > 0)
                                            {
                                                var decay = 15.0 * Waveform.expgrowth(evt.noteEvent.Velocity / 127.0, age, -1);
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
                            if (coloridx < 0) coloridx += GoodPalette.Length;
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

                case 8: // old-school quantum
                    {
                        if(_currentPalette.Count != 5)
                        {
                            RandomizePalette(5);

                            if (Piano.Knobs[4]==0)
                            {
                                Piano.Knobs[4] = 0.1;
                                Piano.Knobs[5] = 0.5;
                                Piano.Knobs[6] = 0.1;
                            }
                        }

                        var colors = _currentPalette
                            .Select((colorMorph) => { return colorMorph.GetColor(); })
                            .ToArray();

                        double k = 3.0 * Piano.Knobs[4];    // 0.3 colors per meter?
                        double noiseLevel = 2.0 * Piano.Knobs[5];
                        double crawlSpeed = 5.0 * Piano.Knobs[6];
                        for (int j = 0; j < Layout.coordinates.Length; ++j)
                        {
                            double noise = Math.Cos((double)j * 32.7);
                            noise *= noise;
                            noise *= noiseLevel;
                            if (Layout.coordinates[j].z == 3 || Layout.coordinates[j].z == 11) { fi += 3; continue; }

                            double x = Layout.coordinates[j].x + Layout.coordinates[j].y;
                            //x %= _currentPalette.Length;

                            if (!(_currentPalette?.Count > 0))
                                return;

                            int coloridx = (((int)(x*k+t* crawlSpeed+noise)) % colors.Length);
                            if (coloridx < 0)
                                coloridx += colors.Length;

                            var color = colors[coloridx];
                            var r = (byte)(Math.Clamp(255.5 * color[0], 0, 255));
                            var g = (byte)(Math.Clamp(255.5 * color[1], 0, 255));
                            var b = (byte)(Math.Clamp(255.5 * color[2], 0, 255));
                            _frameData[fi++] = r;
                            _frameData[fi++] = g;
                            _frameData[fi++] = b;
                        }
                    }
                    return;

                case 9: // ambients / conics
                    {
                        if(_animationNeedsInit)
                        {
                            _randomBlackProbability = 1000;
                            _animationNeedsInit = false;
                        }

                        if (_currentPalette.Count != 4)
                        {
                            RandomizePalette(4);
                            Piano.Knobs[4] = 0.5;
                            Piano.Knobs[5] = 1.0;
                            Piano.Knobs[6] = 0.0;
                            Piano.Knobs[7] = 0.5;
                        }
                        if(Piano.IdleTime > _settings.IdleTimeout)
                        {
                            Piano.Knobs[0] = (0.25 + 0.25 * Math.Cos(t * 2.7)) + 0.21 * 0.21 * Math.Cos(t * 3.9);
                            Piano.Knobs[1] = 0.5;
                            Piano.Knobs[4] = (0.5 + 0.5 * Math.Cos(t * 0.35));
                            Piano.Knobs[5] = (0.5 + 0.5 * Math.Cos(t * 0.31));
                            Piano.Knobs[6] = (0.5 + 0.5 * Math.Cos(t * 0.32));
                            Piano.Knobs[7] = (0.5 + 0.5 * Math.Cos(t * 0.34));
                        }
                        if (_noise?.Length != Layout.coordinates.Length)
                        {
                            _noise = new double[Layout.coordinates.Length];
                            for (int i = 0; i < _noise.Length; ++i)
                                _noise[i] = _random.NextDouble();
                        }

                        var colors = _currentPalette
                            .Select((colorMorph) => { return colorMorph.GetColor(); })
                            .ToArray();

                        for (int j = 0; j < Layout.coordinates.Length; ++j)
                        {
                            if (Layout.coordinates[j].z == 3 || Layout.coordinates[j].z == 11) { fi += 3; continue; }

                            // map domain to approx. -1.5..1.5
                            double x = Layout.coordinates[j].x * 1.5;
                            double y = Layout.coordinates[j].y * 1.5;
                            //double wash = (y - _layoutBounds.Top) / (_layoutBounds.Bottom);
                            double f = (Piano.Knobs[4] - 0.5) * x * x * 2
                                     + (Piano.Knobs[5] - 0.5) * y * y * 2
                                     + (Piano.Knobs[6] - 0.5) * 4
                                     + (Piano.Knobs[7] - 0.5) * y * 4;
                            double hardness = Piano.Knobs[1] * 10.0 + 0.1;
                            double wash = 0.5 + 0.5 * Math.Tanh(hardness * f);
                            // scale noise range to 0..5, where 0.2 of the colors will be blended
                            double ab = 5.0 * _noise[j] - 1.0 - 5.0 * Piano.Knobs[0];  // if knob is between .25 and .75, all colors will be blended

                            double[] colorU = ColorMorph.Mix(colors[0], colors[1], ab);
                            double[] colorV = ColorMorph.Mix(colors[2], colors[3], ab);
                            double[] color = ColorMorph.Mix(colorU, colorV, wash);

                            var r = (byte)(Math.Clamp(255.5 * color[0], 0, 255));
                            var g = (byte)(Math.Clamp(255.5 * color[1], 0, 255));
                            var b = (byte)(Math.Clamp(255.5 * color[2], 0, 255));
                            _frameData[fi++] = r;
                            _frameData[fi++] = g;
                            _frameData[fi++] = b;
                        }
                    }
                    return;

                case 10:    // life!
                    {
                        const int alive = 40;
                        if (_animationNeedsInit || _life?.Length != Layout.coordinates.Length)
                        {
                            _life = new int[Layout.coordinates.Length];
                            _life2 = new int[Layout.coordinates.Length];
                            PointI[] junk;
                            Layouts.Initialize600GridLayoutIndex(out junk, out _lifeGridIndex);
                            // this algorithm is hardcoded to run on a 600 grid.
                            // for now, if there are more lights, duplicate the colors
                            while (_lifeGridIndex.Length < Layout.coordinates.Length)
                                _lifeGridIndex = _lifeGridIndex.Concat(_lifeGridIndex).ToArray();
                            for (int i = 0; i < _life.Length; ++i)
                                _life[i] = (_random.Next() % (alive*2));
                            _animationNeedsInit = false;
                        }

                        var livingColor = _currentPalette[0].GetColor();
                        var thrivingColor = _currentPalette[1].GetColor();
                        var dyingColor = _currentPalette[2].GetColor();

                        const int w = 24;
                        for (int j = 0; j < _life.Length; ++j)
                        {
                            int sum = (_life[(j + 600 - w - 1) % 600] >= alive ? 1 : 0)
                                    + (_life[(j + 600 - w)     % 600] >= alive ? 1 : 0)
                                    + (_life[(j + 600 - w + 1) % 600] >= alive ? 1 : 0)
                                    + (_life[(j + 600 - 1)     % 600] >= alive ? 1 : 0)
                                    + (_life[(j + 600 + 1)     % 600] >= alive ? 1 : 0)
                                    + (_life[(j + 600 + w - 1) % 600] >= alive ? 1 : 0)
                                    + (_life[(j + 600 + w)     % 600] >= alive ? 1 : 0)
                                    + (_life[(j + 600 + w + 1) % 600] >= alive ? 1 : 0);
                            if (_life[j] >= alive)
                                _life2[j] = (sum == 2 || sum == 3) ? Math.Min(2*alive, _life[j] + 1) : alive-1;
                            else
                                _life2[j] = (sum == 3) ? alive : Math.Max(0, _life[j] - 1);

                            var color = (_life2[j] >= alive)
                                ? ColorMorph.Mix(livingColor, thrivingColor, (double)(_life2[j] - alive) / alive)
                                : ColorMorph.Mix(Black, dyingColor, (double)_life2[j] / alive);
                            var r = (byte)(Math.Clamp(255.5 * color[0], 0, 255));
                            var g = (byte)(Math.Clamp(255.5 * color[1], 0, 255));
                            var b = (byte)(Math.Clamp(255.5 * color[2], 0, 255));
                            int fri = 3 * _lifeGridIndex[j];
                            _frameData[fri  ] = r;
                            _frameData[fri+1] = g;
                            _frameData[fri+2] = b;
                        }

                        _life2.CopyTo(_life, 0);
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
                /*for (int k = 0; k < 3; ++k)
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
                }*/

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

        // owner can replace this semaphore
        public System.Threading.SemaphoreSlim ApiSemaphore = new System.Threading.SemaphoreSlim(1, 1);

        #region Realtime test

        // renders frames but does not send to devices
        bool _previewMode = false;
        public bool PreviewMode
        {
            get => _previewMode;
            set
            {
                _previewMode = value;
                OnPropertyChanged();
            }
        }

        //  Use Start() and Stop() to change running status.
        //  Setter not implemented because start and stop are time-consuming and should be async.
        public bool Running
        {
            get => _frameTimer != null;
        }

        public async void Stop()
        {
            if (_stopwatch?.ElapsedMilliseconds > 0)
                Debug.WriteLine($"FPS: {FPS}   Frames: {FrameCounter} {_stopwatch.ElapsedMilliseconds * 0.001}");

            await _frameTimerSemaphore.WaitAsync();
            try
            {
                _frameTimer?.Stop();
                _frameTimer = null;
                _timeOfLastIdleEvent = double.NegativeInfinity;
                LastInteractionTime = double.NegativeInfinity;
                OnPropertyChanged("Running");
            }
            finally
            {
                _frameTimerSemaphore.Release();
            }
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

            await _frameTimerSemaphore.WaitAsync();
            try
            {
                await Initialize();

                // start timers
                _frameTimer = new System.Timers.Timer { AutoReset = true, Interval = _settings.FrameTimerInterval };
                _frameTimer.Elapsed += OnFrameTimerElapsed;
                _frameTimer.Start();
                _stopwatch = new Stopwatch();
                _stopwatch.Start();
                FrameCounter = 0;
                _timeOfLastIdleEvent = double.NegativeInfinity;
                LastInteractionTime = double.NegativeInfinity;


                // start piano listeners/interactivity timers
                if (Piano?.IsMonitoring == true)
                {
                    Piano.PianoKeyDownEvent += HandlePianoKeyDownEvent;
                    Piano.PianoIdleEvent += Piano_PianoIdleEvent;
                }
                else
                {
                    idleTimer = new System.Timers.Timer() { Enabled = true, AutoReset = true, Interval = 500 };
                    idleTimer.Elapsed += OnIdleTimerElapsed;
                    idleTimer.Start();
                    LastInteractionTime = CurrentTime;
                }

                OnPropertyChanged("Running");
            }
            finally
            {
                _frameTimerSemaphore.Release();
            }
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

            try
            {

                DrawFrame();

                FrameCounter++;

                // in preview mode, we don't need to actually send the image to the devices
                if (PreviewMode)
                    return;

                await ApiSemaphore.WaitAsync();

                try
                {
                    int offset = 0;
                    foreach (var device in Devices)
                    {
                        if (device.LedConfig == null || device.CurrentMode.mode != "rt")
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

                    //Debug.Assert(offset == _frameData.Length, "Device LED counts don't match RT. Check device modes and call Initialize");
                    if (offset != _frameData.Length)
                    {
                        Console.WriteLine($"Device LED counts ({offset}) doesn't match RT frame size ({_frameData.Length}). Switching to Preview mode. Check device modes and call Initialize.");
                        PreviewMode = true;
                    }

                    //if (FrameCounter % 100 == 0)
                    //    OnPropertyChanged("FPS");
                }
                finally
                {
                    ApiSemaphore.Release();
                }
            }
            finally
            {
                _frameTimerSemaphore.Release();
            }
        }

        #endregion
    }
}
