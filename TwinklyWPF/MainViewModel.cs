using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media;
using Twinkly_xled;

namespace TwinklyWPF
{
    // --------------------------------------------------------------------------
    //       API docs - https://xled-docs.readthedocs.io/en/latest/rest_api.html
    // Python library - https://github.com/scrool/xled
    // --------------------------------------------------------------------------

    public class MainViewModel : INotifyPropertyChanged//, IDataErrorInfo
    {
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
                if (_activeDevice != null)
                {
                    App.Current.Settings.ActiveDeviceName = ActiveDevice.UniqueName;
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
            // should this be called Rescan?
            // since it can be called more than once, we should stop things first...
            StopRealtimeTest();


            Piano.Initialize();




            await _apiSemaphore.WaitAsync();

            try
            {
                // save current selection to re-select it if possible after the discovery
                var selectedDevice = ActiveDevice;

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

                // load all device data
                foreach(var device in Devices)
                {
                    // gestalts..
                    await device.Load();
                    // authentication, description and state
                    //await device.UpdateAuthModels(true);
                }

                // always set ActiveDevice, even if keeping same value.. need to update the API
                // try to keep the same device selected
                ActiveDevice = FindDevice(selectedDevice?.UniqueName);
                // .. or the device from the user settings
                if (ActiveDevice == null && !string.IsNullOrEmpty(App.Current.Settings.ActiveDeviceName))
                    ActiveDevice = FindDevice(App.Current.Settings.ActiveDeviceName);
                if (ActiveDevice == null)
                    ActiveDevice = Devices.FirstOrDefault();

                OnPropertyChanged("Devices");

                if (_arguments.Contains("AutoStart") || App.Current.Settings.AutoStart)
                {
                    App.Current.MainWindow.ShowLayoutWindow();

                    // do not await, since StartRealtimeTest can't execute until it has the semaphore
                    Task _ = StartRealtimeTest();
                }
                else
                {
                    //if (ActiveDevice == null)
                    //    await FakeLocate();

                    // only load if the API detected the Twinkly at startup
                    if (ActiveDevice != null)
                    {
                        await ActiveDevice.Load();
                        await ActiveDevice.Login();
                        // todo: load the full auth model as well?
                    }
                }
            }
            finally
            {
                _apiSemaphore.Release();
            }

            SaveDeviceList();
            App.Current.SaveSettings();
        }

        //  Invoked just before app exits
        public async Task Shutdown()
        {
            SaveDeviceList();

            var defaultMode = App.Current.Settings.SetModeOnExit;
            foreach (var device in Devices)
            {
                var mode = App.Current.Settings.GetDeviceMetadataEntry(device.UniqueName, "SetModeOnExit");
                if (string.IsNullOrEmpty(mode))
                {
                    mode = defaultMode;
                }
                if (!string.IsNullOrEmpty(mode))
                {
                    await device.ChangeMode(mode, false);
                }
            }
        }

        //  Adds all current devices to {KnownDevices} list in user settings
        public void SaveDeviceList()
        {
            if (App.Current.Settings.KnownDevices == null)
                App.Current.Settings.KnownDevices = new Dictionary<string, object>();


            foreach (var device in Devices)
            {
                if (device.Gestalt == null)
                {
                    Console.WriteLine($"ERROR: Device gestalt unknown");    // probably a coding error
                }

                var metadata = App.Current.Settings.GetDeviceMetadata(device.UniqueName);

                metadata["Name"]          = device.FriendlyName;
                metadata["IPAddress"]     = device.twinklyapi.data.IPAddressString;
                metadata["device_name"]   = device.Gestalt?.device_name;
                metadata["led_profile"]   = device.Gestalt?.led_profile;
                metadata["led_type"]      = device.Gestalt?.led_type.ToString();
                metadata["number_of_led"] = device.Gestalt?.number_of_led.ToString();
                metadata["Last Seen"]     = DateTime.Now.ToString();
                //metadata["SetModeOnExit"] = "";
            }

            if(ActiveDevice != null)
                App.Current.Settings.ActiveDeviceName = ActiveDevice.UniqueName;
        }

        #endregion

        #region Device management

        private async Task Discover()
        {
            // make sure we're locked
            Debug.Assert(_apiSemaphore.CurrentCount == 0);

            Message = "Searching for devices...";

            ActiveDevice = null;
            Devices.Clear();
            OnPropertyChanged("Devices");
            OnPropertyChanged("TwinklyDetected");

            var addresses = await Task.Run(() =>
            {
                return DataAccess.Discover();
            });

            Message = $"Adding {addresses.Count} devices...";

            foreach (var ip in addresses)
                Devices.Add(new Device(IPAddress.Parse(ip)));
            OnPropertyChanged("TwinklyDetected");

            if (TwinklyDetected)
            {

                Message = $"Found {Devices.Count()} devices.";
            }
            //else if (twinklyapi.Status == 0)
            //{
            //    Message = "Twinkly Not Found !";
            //}
            else
            {
                Message = $"Locate failed. Status={DataAccess.LastError}";
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

        private Device FindDevice(string uniqueName)
        {
            return Devices.FirstOrDefault(
                (device) => uniqueName == device.UniqueName
                         || uniqueName == device.twinklyapi.data.IPAddress.ToString()
                         || uniqueName == device.Gestalt?.device_name);
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

#endregion

        public double FPS
        {
            get => RTMovie?.FPS ?? 0;
        }

        #region GUI

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

                // get the device gestalt and firware data only if it hasn't been done yet
                if (ActiveDevice.ReloadNeeded)
                    await ActiveDevice.Load();

                // get all the device settings, authenticating if necessary
                await ActiveDevice.UpdateAuthModels(true);

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



        #endregion

        #region Realtime test

        RealtimeMovie _animation;
        public RealtimeMovie RTMovie
        {
            get => _animation;
            private set
            {
                _animation = value;
                OnPropertyChanged();
            }
        }

        public bool RealtimeMovieRunning => (RTMovie?.Running == true);

        public void StopRealtimeTest()
        {
            if (RTMovie?.FPS > 0)
            {
                OnPropertyChanged("FPS");
                Debug.WriteLine($"FPS: {RTMovie.FPS}   Frames: {RTMovie.FrameCounter}");
            }

            RTMovie?.Stop();
            OnPropertyChanged("RealtimeMovieRunning");
        }

        public async Task StartRealtimeTest()
        {
            if (RealtimeMovieRunning)
                return;

            Message = $"Setting up {Devices.Count()} devices...";

            List<Device> devices = new List<Device>();
            foreach (Device device in Devices)
            {
                if (App.Current.Settings.RTMovieDevices == null
                    || App.Current.Settings.RTMovieDevices.Contains(device.FriendlyName)
                    || App.Current.Settings.RTMovieDevices.Contains(device.Gestalt?.device_name))
                    devices.Add(device);
            }

            if(devices.Count == 0)
            {
                Message = $"Unable to find any devices for RTMovie. 0 of {Devices.Count} devices matched RTMovieDevices setting.";
                return;
            }

            RTMovie = new RealtimeMovie()
            {
                ApiSemaphore = _apiSemaphore,
                Devices = devices,
                Piano = Piano
            };

            App.Log("Starting RT");
            Message = "Starting RT";
            await RTMovie.Start();
            Message = "RT Animation Started";

            OnPropertyChanged("RealtimeMovieRunning");
        }

        #endregion

    }
}
