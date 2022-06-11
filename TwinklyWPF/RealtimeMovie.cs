using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace TwinklyWPF
{
    public class RealtimeMovie: INotifyPropertyChanged
    {
        private System.Timers.Timer _frameTimer;
        protected Stopwatch _stopwatch;
        public int FrameCounter { get; private set; }
        
        protected byte[] _frameData;
        Random _random = new Random();

        public int Inputs = 0;

        protected virtual void Draw()
        {
            double t = _stopwatch.ElapsedMilliseconds * 0.001;

            for (int i = 0; i < _frameData.Length; ++i)
            {
                //int v = frameData[i];
                //frameData[i] = (byte)(((v&1)==1)
                //    ? (frameData[i] > 1 ? frameData[i] - 2 : 0) 
                //    : (frameData[i] < 254 ? frameData[i] + 2 : 255));

                double n = (i / 3);
                _frameData[  i] = (byte)(Math.Max(0.0, 255.9 * Math.Sin(t + n * 0.05)));
                _frameData[++i] = (byte)(Math.Max(0.0, 255.9 * Math.Sin(t * 0.97 + n * 0.05)));
                _frameData[++i] = (byte)(Math.Max(0.0, 255.9 * Math.Sin(t * 0.94 + n * 0.05)));

                if ((Inputs & 1) != 0)
                {
                    _frameData[i - 2] = 255;
                }
                if ((Inputs & 2) != 0)
                {
                    _frameData[i - 1] = 255;
                }
                if ((Inputs & 4) != 0)
                {
                    _frameData[i - 0] = 255;
                }
                //frameData[  i] = (byte)(((i + t) % 10.0)*(255.0/ 9.0));
                //frameData[++i] = (byte)(((i + t) % 12.0)*(255.0/11.0));
                //frameData[++i] = (byte)(((i + t) % 16.0)*(255.0/15.0));
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
                _frameData = new byte[frameSize];

                _frameTimer = new System.Timers.Timer { AutoReset = true, Interval = 10 };
                _frameTimer.Elapsed += OnFrameTimerElapsed;
                _frameTimer.Start();
                _stopwatch = new Stopwatch();
                _stopwatch.Start();
                FrameCounter = 0;

                _random.NextBytes(_frameData);
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
