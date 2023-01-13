using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Twinkly_xled.JSONModels;
using Twinkly_xled;
using Timer = Twinkly_xled.JSONModels.Timer;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media;
using GalaSoft.MvvmLight.Command;

namespace TwinklyWPF
{
    // --------------------------------------------------------------------------
    //       API docs - https://xled-docs.readthedocs.io/en/latest/rest_api.html
    // Python library - https://github.com/scrool/xled
    // --------------------------------------------------------------------------

    public class MainViewModel : INotifyPropertyChanged//, IDataErrorInfo
    {
        public RelayCommand<string> ModeCommand { get; private set; }
        public RelayCommand UpdateTimerCommand { get; private set; }
        public RelayCommand<bool> RealtimeTestCommand { get; private set; }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion

        private System.Timers.Timer _refreshGuiTimer;

        //  Ensure that periodic updates and sync operations don't interfere with each other
        private readonly System.Threading.SemaphoreSlim _apiSemaphore = new System.Threading.SemaphoreSlim(1, 1);

        // Set by view so our colour are calculated from the same source
        public GradientStopCollection GradientStops { get; set; }

        public Piano Piano = new Piano();

        public bool TwinklyDetected
        {
            get { return Devices.Count() > 0; }
        }

        //  Devices found by Discover
        public ObservableCollection<Device> Devices { get; } = new ObservableCollection<Device>();

        Device _activeDevice;
        public Device ActiveDevice
        {
            get => _activeDevice;
            set
            {
                Debug.Assert(value == null || Devices.Contains(value));
                _activeDevice = value;
                //ActiveDevice?.Reload();
                if (_activeDevice != null)
                {
                    App.Current.Settings.ActiveDeviceName = ActiveDevice.Name;
                }
                OnPropertyChanged();
            }
        }

        private string _message = "";
        public string Message
        {
            get { return _message; }
            set
            {
                _message = value;
                OnPropertyChanged();
            }
        }

        #region Initialization

        private IReadOnlyList<string> _arguments;

        public MainViewModel(IReadOnlyList<string> arguments)
        {
            ModeCommand = new RelayCommand<string>(async (x) =>
            {
                StopRealtimeTest();
                await ActiveDevice?.ChangeMode(x);
            });

            UpdateTimerCommand = new RelayCommand(async () => await ActiveDevice.ChangeTimer());

            RealtimeTestCommand = new RelayCommand<bool>(
                async (isChecked) => {
                    if (isChecked == true)
                        await StartRealtimeTest();
                    else
                        StopRealtimeTest();
                },
                (isChecked) => {
                    return true;    // todo: figure out how to trigger: ActiveDevice?.CurrentMode=="rt";
                });


            _arguments = arguments;

            _refreshGuiTimer = new System.Timers.Timer(1000) { AutoReset = true };
            _refreshGuiTimer.Elapsed += RefreshGui;
            _refreshGuiTimer.Start();

            //if (arguments.Contains("Manual"))
            {
                _manualIpAddresses = new List<string>
                {
                    "192.168.0.18",
                    "192.168.0.19",
                    "192.168.0.20",
                    //"192.168.0.21"
                };
            }

        }

        List<string> _manualIpAddresses;

        async public Task Initialize()
        {

            Piano.Initialize();




            await _apiSemaphore.WaitAsync();

            try
            {
                if (true)
                {
                    await Discover();
                }
                else
                {
                    foreach (string ip in _manualIpAddresses)
                    {
                        Message = $"Adding device {ip}...";
                        await AddDevice(IPAddress.Parse(ip));
                    }
                }

                if (_arguments.Contains("RT") || App.Current.Settings.AutoStart)
                {
                    // do not await, since StartRealtimeTest can't execute until it has the semaphore
                    Task _ = StartRealtimeTest();
                }
                else
                {
                    //if (ActiveDevice == null)
                    //    await FakeLocate();

                    // only load if the API detected the Twinkly at startup
                    if (ActiveDevice != null)
                        await ActiveDevice.Load();
                }
            }
            finally
            {
                _apiSemaphore.Release();
            }
        }

        #endregion

        private async Task Discover()
        {
            // make sure we're locked
            Debug.Assert(_apiSemaphore.CurrentCount == 0);

            // store current selection
            var selectedDevice = ActiveDevice;

            //ActiveDevice?.Unload();
            ActiveDevice = null;
            Devices.Clear();
            OnPropertyChanged("Devices");
            OnPropertyChanged("TwinklyDetected");

            Message = "Discovering...";

            var addresses = await Task.Run(() =>
            {
                return DataAccess.Discover();
            });
            foreach (var ip in addresses)
                Devices.Add(new Device(IPAddress.Parse(ip)));
            OnPropertyChanged("TwinklyDetected");

            if (TwinklyDetected)
            {
                // always set ActiveDevice, even if keeping same value.. need to update the API
                ActiveDevice = Devices.FirstOrDefault((device) => device.Equals(selectedDevice));
                if (ActiveDevice == null)
                    ActiveDevice = Devices.FirstOrDefault((device) => device.Name == App.Current.Settings.ActiveDeviceName);
                if (ActiveDevice == null)
                    ActiveDevice = Devices.FirstOrDefault();

                Message = $"Found {Devices?.Count()} devices.";
            }
            //else if (twinklyapi.Status == 0)
            //{
            //    Message = "Twinkly Not Found !";
            //}
            else
            {
                Message = $"Locate failed. Status=?";
            }
        }

        // TODO

        public async Task FakeLocate()
        {
            await _apiSemaphore.WaitAsync();

            try
            {
                foreach (string ip in _manualIpAddresses)
                {
                    Message = $"Adding device {ip}...";
                    await AddDevice(IPAddress.Parse(ip));
                }

                //await AddDevice(IPAddress.Parse("192.168.0.18"));
                //await AddDevice(IPAddress.Parse("192.168.0.19"));
                //await AddDevice(IPAddress.Parse("192.168.0.20"));
                //await AddDevice(IPAddress.Parse("192.168.0.21"));

                // do what Locate() does
                if (ActiveDevice == null)
                {
                    ActiveDevice = Devices.FirstOrDefault();
                }

                OnPropertyChanged("TwinklyDetected");

                //await Load();
            }
            finally
            {
                _apiSemaphore.Release();
            }
        }

        private Device FindDevice(IPAddress ipAddress)
        {
            return Devices.FirstOrDefault((device) => device.twinklyapi.data.IPAddress.Equals(ipAddress));
        }

        //todo
        public async Task<Device> AddDevice(IPAddress ipAddress)
        {
            // make sure not duplicate
            if (FindDevice(ipAddress) != null)
                return null;

            //await _apiSemaphore.WaitAsync();

            try
            {
                var device = new Device(ipAddress);
                Devices.Add(device);

                Message = $"Loading {ipAddress}...";

                //twinklyapi.data.IPAddress = ipAddress;
                //OnPropertyChanged("twinklyapi");

                //twinklyapi.Devices.Add(ipAddress);
                // this is a cheat
                //TwinklyDetected = true;
                OnPropertyChanged("TwinklyDetected");

                // always set ActiveDevice, even if keeping same value.. need to update the API
                if (ActiveDevice == null)
                    ActiveDevice = device;

                //await Load();

                //// notify that twinklyapi.Devices has changed
                //OnPropertyChanged();
                //OnPropertyChanged("twinklyapi");

                Message = $"Added {ipAddress}.";

                return device;
            }
            finally
            {
                //_apiSemaphore.Release();
            }
        }

        public double FPS
        {
            get => RTMovie?.FPS ?? 0;
        }

        public bool PreviewMode
        {
            get => RTMovie?.PreviewMode==true;
            set
            {
                RTMovie.PreviewMode = value;
                OnPropertyChanged();
            }
        }


        public async void RefreshGui(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (_apiSemaphore.CurrentCount == 0)
                return; // boring task anyway

            if (RealtimeMovieRunning)
            {
                // realtime test--don't hog the API with updates.
                // Do update the FPS field
                OnPropertyChanged("FPS");
                return;
            }

            await _apiSemaphore.WaitAsync();

            try
            {
                if (ActiveDevice?.twinklyapi.data?.IPAddress == null)
                    return;

                if (ActiveDevice.ReloadNeeded)
                    await ActiveDevice.Load();

                await ActiveDevice.UpdateAuthModels();

                OnPropertyChanged("ActiveDevice");
                OnPropertyChanged("Devices");
            }
            catch (Exception err)
            {
                Message = $"Refresh failed: {err.Message}";
            }
            finally
            {
                _apiSemaphore.Release();
            }
        }


        #region Realtime test

        public RealtimeMovie RTMovie { get; private set; }

        public bool RealtimeMovieRunning => (RTMovie?.Running == true);

        public void StopRealtimeTest()
        {
            if (RTMovie?.FPS > 0)
            {
                OnPropertyChanged("FPS");
                Debug.WriteLine($"FPS: {RTMovie.FPS}   Frames: {RTMovie.FrameCounter}");
            }

            RTMovie?.Stop();
        }

        public Task StartRealtimeTest()
        {
            if (RealtimeMovieRunning)
                return Task.CompletedTask;

            Message = $"Setting up {Devices.Count()} devices...";

            RTMovie = new RealtimeMovie()
            {
                ApiSemaphore = _apiSemaphore,
                Devices = Devices,
                Piano = Piano
            };

            Message = "Starting RT";
            return RTMovie.Start();
        }

        #endregion

    }
}
