using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using Twinkly_xled.JSONModels;
using TwinklyWPF.Utilities;

namespace TwinklyWPF
{
    public class RealtimeMovie: INotifyPropertyChanged
    {
        private System.Timers.Timer _frameTimer;
        protected Stopwatch _stopwatch;
        public int FrameCounter { get; private set; }
        
        protected byte[] _frameData;
        Random _random = new Random();

        public Piano Piano;
        public int Inputs = 0;

        protected double[] KeysDownTimes = new double[3];
        protected double[] KeysUpTimes = new double[3];

        public int ColorMode = 3;

        public static double[][] GoodPalette = { 
            new double[3] { 1.0, 0.0, 0.5 },    // hot pink
            new double[3] { 1.0, 0.0, 0.2 },    // magenta
            new double[3] { 1.0, 0.2, 0.0 },    // orange
            new double[3] { 1.0, 0.4, 0.0 },    // gold
            new double[3] { 1.0, 0.7, 0.0 },    // basically yellow
            new double[3] { 0.5, 1.0, 0.0 },    // yellow-green
            new double[3] { 0.1, 1.0, 0.0 },    // green
            new double[3] { 0.0, 0.5, 1.0 },    // light blue
            new double[3] { 0.0, 0.1, 1.0 },    // blue blue
            new double[3] { 0.5, 0.0, 1.0 },    // purple
            new double[3] { 0.5, 0.5, 0.5 },    // low white
        };

        ColorMorph[] _currentPalette = { 
            new ColorMorph( 1.0, 0.0, 0.2 ),    // magenta
            new ColorMorph( 0.5, 1.0, 0.0 ),    // yellow-green
            new ColorMorph( 0.0, 0.5, 1.0 ),    // light blue
        };

        public Layout Layout { get; set; }
        public RealtimeMovie()
        {
            InitializeLayout(600);
        }

        void InitializeLayout(int n)
        {
            if (n != 600) throw new Exception($"Bad n! {n}");

            Layout houseLayout = new Layout { aspectXY=0, aspectXZ=0, source="2d", synthesized=true };
            houseLayout.coordinates = new XYZ[n];
            int zone = 0;
            var xyz = new XYZ { x = 0, y = 10.0, z = 0.0 };
            for (int j = 0, zi = 0; j < n; ++j, ++zi)
            {
                switch (j)
                {
                    case  90: ++zone; xyz.z = zone; zi = 0; break;  // 1: short run
                    case 136: ++zone; xyz.z = zone; zi = 0; break;  // 2: main
                    case 187: ++zone; xyz.z = zone; zi = 0; break;  // 3: dead
                    case 198: ++zone; xyz.z = zone; zi = 0; break;  // 4: doorframe L down
                    case 224: ++zone; xyz.z = zone; zi = 0; break;  // 5: doorframe L up
                    case 248: ++zone; xyz.z = zone; zi = 0; break;  // 6: doorframe top
                    case 271: ++zone; xyz.z = zone; zi = 0; break;  // 7: doorframe R down
                    case 300: ++zone; xyz.z = zone; zi = 0; xyz = new XYZ { x = 0.05, y = 10.0, z = 5.0 }; break; // back main
                    case 395: ++zone; xyz.z = zone; zi = 0; break;  // 9: short run
                    case 443: ++zone; xyz.z = zone; zi = 0; break;  // 10: main
                    case 557: ++zone; xyz.z = zone; zi = 0; break;  // 11: leftover/downspout
                }

                houseLayout.coordinates[j] = xyz;
                
                switch (zone)
                {
                    case 0: // back main #1
                    case 1:
                    case 2:
                        xyz.x += 0.1;
                        break;
                    case 3: // dead to doorframe
                        xyz.x += 0.08;
                        xyz.y -= 0.05;
                        break;
                    case 4: // doorframe L down
                        xyz.y -= 0.1;
                        break;
                    case 5: // doorframe L up
                        xyz.y += 0.1;
                        break;
                    case 6: // doorframe top
                        xyz.x += 0.1;
                        break;
                    case 7: // doorframe R down
                        xyz.y -= 0.1;
                        break;
                    case 8: // strand #2 back main
                    case 9:
                    case 10:
                    case 11: // dead return
                        xyz.x += 0.1;
                        break;
                }
            }

            this.Layout = houseLayout;
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

        public void ChangeColors()
        {
            for (int i = 0; i < _currentPalette.Length; ++i)
            {
                int j = (_random.Next() & 0xFFFF) % GoodPalette.Length;
                _currentPalette[i].SetTarget(GoodPalette[j]);
            }
        }

        protected virtual void Draw()
        {
            double t = _stopwatch.ElapsedMilliseconds * 0.001;

            switch (ColorMode % 4)
            {
                case 1:
                {
                    var color = new byte[3];
                    for (int i = 0; i < _frameData.Length;)
                    {
                        int j = i / 3;

                        switch ((int)Math.Round(Layout.coordinates[j].z))
                        {

                            case  0: color = new byte[3] { 255, 255, 255 };  break; // 
                            case  1: color = new byte[3] { 255,   0,   0 };  break; // 
                            case  2: color = new byte[3] { 255, 255, 255 };  break; // 
                            case  3: color = new byte[3] {   0,   0,   0 };  break; // dead
                            case  4: color = new byte[3] { 255, 255,   0 };  break; // doorframe L
                            case  5: color = new byte[3] {   0, 127, 255 };  break; // doorframe L
                            case  6: color = new byte[3] { 255,   0,   0 };  break; // doorframe T
                            case  7: color = new byte[3] {   0, 255,   0 };  break; // doorframe R
                            case  8: color = new byte[3] {   0, 255, 255 };  break; // back
                            case  9: color = new byte[3] {   0,   0, 255 };  break; // short
                            case 10: color = new byte[3] { 255,   0, 255 };  break; // main
                            case 11: color = new byte[3] {   0,   0,   0 };  break; // leftover/downspout
                            default: color = new byte[3] { 125, 255, 255 };  break;
                        }

                        _frameData[i++] = color[0];
                        _frameData[i++] = color[1];
                        _frameData[i++] = color[2];

                    }

                }
                return;

                case 2:
                {
                    int i = 0;
                    for (int j = 0; j < _frameData.Length/3; ++j)
                    {
                        if (Layout.coordinates[j].z == 3 || Layout.coordinates[j].z == 11) { i += 3; continue; }

                        double v = Math.Sin(0.5 * Layout.coordinates[j].x + 1.5 * Layout.coordinates[j].y + 1.1 * t);

                        _frameData[i++] = (byte)(Math.Clamp(255.5 * v, 0, 255));
                        _frameData[i++] = (byte)(Math.Clamp(255.5 * v, 0, 255));
                        _frameData[i++] = (byte)(Math.Clamp(255.5 * v, 0, 255));
                    }
                }
                return;

                case 3:
                    {
                        int i = 0;

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

                        for (int j = 0; j*3 < _frameData.Length; ++j)
                        {
                            if (Layout.coordinates[j].z == 3 || Layout.coordinates[j].z == 11) { i += 3; continue; }

                            double x = Layout.coordinates[j].x - 20.5;
                            double y = Layout.coordinates[j].y - 10.0;

                            //double v = Math.Sin(0.5 * x + 1.5 * y + 1.1 * t);
                            //v *= Math.Abs(v);
                            //double w = Math.Sin(2.3 * x + 6.9 * y - 2.3 * t);
                            //w *= Math.Abs(w);

                            const double wavelength = 5;

                            double u = Waveform.SpacedTriangle(x+y + t * 0.3, wavelength, 4*(1-Piano.Knobs[0]));
                            double v = Waveform.SpacedTriangle(x+y - t * 0.6, wavelength, 4*(1-Piano.Knobs[1]));
                            double w = Waveform.SpacedTriangle(x+y + t * 0.7, wavelength, 4*(1-Piano.Knobs[2]));

                            _frameData[i++] = (byte)(Math.Clamp(255.5 * (v*colors[0][0] + w*colors[1][0] + u*colors[2][0]), 0, 255));
                            _frameData[i++] = (byte)(Math.Clamp(255.5 * (v*colors[0][1] + w*colors[1][1] + u*colors[2][1]), 0, 255));
                            _frameData[i++] = (byte)(Math.Clamp(255.5 * (v*colors[0][2] + w*colors[1][2] + u*colors[2][2]), 0, 255));
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


            for (int i = 0; i < _frameData.Length;)
            {
                //int v = frameData[i];
                //frameData[i] = (byte)(((v&1)==1)
                //    ? (frameData[i] > 1 ? frameData[i] - 2 : 0) 
                //    : (frameData[i] < 254 ? frameData[i] + 2 : 255));

                // old mapping:
                //double x = sc * (double)((i / 3) % 300);// / _frameData.Length);

                // new mapping:
                int j = i / 3;
                if (Layout.coordinates[j].z == 3 || Layout.coordinates[j].z == 11)
                {
                    _frameData[i++] = 0;
                    _frameData[i++] = 0;
                    _frameData[i++] = 0;
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

                _frameData[i++] = (byte)(Math.Clamp(255.9 * c[0], 0, 255));
                _frameData[i++] = (byte)(Math.Clamp(255.9 * c[1], 0, 255));
                _frameData[i++] = (byte)(Math.Clamp(255.9 * c[2], 0, 255));
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

            await ApiSemaphore.WaitAsync();

            try
            {
                // make sure we are authorized and have gestalt for all devices
                foreach (var device in Devices)
                {
                    if (device.Gestalt == null)
                        await device.Load();

                    if (device.LedConfig == null)
                        await device.UpdateAuthModels();

                    if (!device.CurrentMode_Realtime)
                        await device.ChangeMode("rt");
                }

                int frameSize = 0;
                foreach (var device in Devices)
                {
                    frameSize += device.Gestalt.bytes_per_led * device.Gestalt.number_of_led;
                }
                App.Log($"RT movie framesize: {frameSize} bytes over {Devices.Count()} devices");
                _frameData = new byte[frameSize];

                _frameTimer = new System.Timers.Timer { AutoReset = true, Interval = 10 };
                _frameTimer.Elapsed += OnFrameTimerElapsed;
                _frameTimer.Start();
                _stopwatch = new Stopwatch();
                _stopwatch.Start();
                FrameCounter = 0;

                //_random.NextBytes(_frameData);
            }
            finally
            {
                ApiSemaphore.Release();
            }
        }

        private System.Threading.SemaphoreSlim _frameTimerSemaphore = new System.Threading.SemaphoreSlim(1, 1);

        private async void OnFrameTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //random.NextBytes(frameData);
            if (_frameTimer == null)
                return;

            if (!_frameTimerSemaphore.Wait(TimeSpan.Zero))
            {
                return;
            }

            Draw();

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
