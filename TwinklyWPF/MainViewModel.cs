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

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion

        private System.Timers.Timer _updateTimer;

        //  Ensure that periodic updates and sync operations don't interfere with each other
        private readonly System.Threading.SemaphoreSlim m_apiSemaphore = new System.Threading.SemaphoreSlim(1, 1);

        // Set by view so our colour are calculated from the same source
        public GradientStopCollection GradientStops { get; set; }



        public bool TwinklyDetected
        {
            get { return Devices.Count() > 0; }
        }

        //  Devices found by Discover
        public ObservableCollection<Device> Devices { get; } = new ObservableCollection<Device>();

        Device m_activeDevice;
        public Device ActiveDevice
        {
            get => m_activeDevice;
            set
            {
                Debug.Assert(value == null || Devices.Contains(value));
                m_activeDevice = value;
                ActiveDevice?.Reload();
                OnPropertyChanged();
            }
        }

        private string message = "";
        public string Message
        {
            get { return message; }
            set
            {
                message = value;
                OnPropertyChanged();
            }
        }

        public MainViewModel(IReadOnlyList<string> arguments)
        {
            ModeCommand = new RelayCommand<string>(async (x) => await ActiveDevice?.ChangeMode(x));

            UpdateTimerCommand = new RelayCommand(async () => await ActiveDevice.ChangeTimer());


            _updateTimer = new System.Timers.Timer(1000) { AutoReset = true };
            _updateTimer.Elapsed += refreshGui;
            _updateTimer.Start();
        }

        async public Task Initialize()
        {
            await m_apiSemaphore.WaitAsync();

            try
            {
                await Discover();   // TODO: move?

                // only load if the API detected the Twinkly at startup
                await ActiveDevice?.Load();
            }
            finally
            {
                m_apiSemaphore.Release();
            }
        }

        private async Task Discover()
        {
            // make sure we're locked
            Debug.Assert(m_apiSemaphore.CurrentCount == 0);

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
                    ActiveDevice = Devices.FirstOrDefault();

                Message = $"Found {Devices?.Count()} devices.";
            }
            //else if (twinklyapi.Status == 0)
            //{
            //    Message = "Twinkly Not Found !";
            //}
            //else
            //{
            //    Message = $"Locate failed. Status={twinklyapi.Status}";
            //}
        }

        // TODO
        /*
        public async Task FakeLocate()
        {
            await m_apiSemaphore.WaitAsync();

            try
            {
                AddDevice(IPAddress.Parse("192.168.0.18"));
                AddDevice(IPAddress.Parse("192.168.0.19"));
                AddDevice(IPAddress.Parse("192.168.0.20"));
                AddDevice(IPAddress.Parse("192.168.0.21"));

                // do what Locate() does
                if (ActiveDevice == null)
                {
                    ActiveDevice = Devices.FirstOrDefault();
                }

                OnPropertyChanged("TwinklyDetected");

                await Load();
            }
            finally
            {
                m_apiSemaphore.Release();
            }
        }

        //todo
        /*public void AddDevice(IPAddress ipAddress)
        {
            if (Devices.Contains(ipAddress))
                return;

            Devices.Add(ipAddress);

            await m_apiSemaphore.WaitAsync();

            try
            {

                Message = $"Loading {ipAddress}...";

                twinklyapi.data.IPAddress = ipAddress;
                OnPropertyChanged("twinklyapi");

                twinklyapi.Devices.Add(ipAddress);
                // this is a cheat
                TwinklyDetected = true;
                await Load();

                //// notify that twinklyapi.Devices has changed
                //OnPropertyChanged();
                OnPropertyChanged("twinklyapi");

                Message = $"Loaded {ipAddress}.";

            }
            finally
            {
                m_apiSemaphore.Release();
            }
        }*/

        public async void refreshGui(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (m_apiSemaphore.CurrentCount == 0)
                return; // boring task anyway

            await m_apiSemaphore.WaitAsync();

            try
            {
                if (ActiveDevice?.twinklyapi.data?.HttpClient == null)
                    return;

                if (ActiveDevice.ReloadNeeded)
                {
                    Message = "Loading...";
                    await ActiveDevice.Load();
                }
                else
                {
                    await ActiveDevice.UpdateAuthModels();
                }
                OnPropertyChanged("ActiveDevice");
                OnPropertyChanged("Devices");
            }
            finally
            {
                m_apiSemaphore.Release();
            }
        }
    }





    public class Device : INotifyPropertyChanged, IDataErrorInfo
    {
        public XLedAPI twinklyapi { get; private set; } = new XLedAPI();

        public event PropertyChangedEventHandler PropertyChanged;

        public Device(IPAddress ipAddress)
        {
            twinklyapi.IPAddress = ipAddress;
        }

        #region overrides

        public override string ToString()
        {
            return $"{twinklyapi.data.IPAddress} ({CurrentMovie?.sync.mode})";
        }

        public override bool Equals(object obj)
        {
            return twinklyapi.data.IPAddress.Equals((obj as Device)?.twinklyapi.data.IPAddress);
        }

        public override int GetHashCode()
        {
            return twinklyapi.data.IPAddress.GetHashCode();
        }

        #endregion

        #region INotifyPropertyChanged

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion

        private string message = "";
        public string Message
        {
            get { return message; }
            set
            {
                message = value;
                OnPropertyChanged();
            }
        }

        private GestaltResult gestalt = new GestaltResult();
        public GestaltResult Gestalt
        {
            get { return gestalt; }
            private set
            {
                gestalt = value;
                OnPropertyChanged();
                OnPropertyChanged("Uptime");
            }
        }

        private FWResult fw = new FWResult();
        public FWResult FW
        {
            get { return fw; }
            private set
            {
                fw = value;
                OnPropertyChanged();
            }
        }

        // Schedule Twinkly On and off

        private Timer timer = new Timer() { time_on = -1, time_off = -1 };
        public Timer Timer
        {
            get { return timer; }
            set
            {
                Debug.Assert(value != null);
                timer = value;
                OnPropertyChanged();
                OnPropertyChanged("TimerNow");
                if (string.IsNullOrWhiteSpace(ScheduleOffText))
                    ScheduleOffText = value.time_off == -1 ? "-1" : new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day).AddSeconds(value.time_off).ToString("HH:mm");
                if (string.IsNullOrWhiteSpace(ScheduleOnText))
                    ScheduleOnText = value.time_on == -1 ? "-1" : new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day).AddSeconds(value.time_on).ToString("HH:mm");
            }
        }

        public DateTime TimerNow
        {
            get { return new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day).AddSeconds(timer.time_now); }
        }

        private string scheduleontext;
        public string ScheduleOnText
        {
            get { return scheduleontext; }
            set
            {
                scheduleontext = value;
                OnPropertyChanged();
            }
        }

        private string scheduleofftext;
        public string ScheduleOffText
        {
            get { return scheduleofftext; }
            set
            {
                scheduleofftext = value;
                OnPropertyChanged();
            }
        }



        private Mode m_CurrentMode = new Mode() { mode = "unknown" };
        public Mode CurrentMode
        {
            get { return m_CurrentMode; }
            private set
            {
                if (value?.mode != m_CurrentMode?.mode)
                {
                    m_CurrentMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged("CurrentMode_Movie");
                    OnPropertyChanged("CurrentMode_Off");
                    OnPropertyChanged("CurrentMode_Demo");
                    OnPropertyChanged("CurrentMode_Realtime");

                    StopRealtimeTest();
                }
            }
        }

        public bool CurrentMode_Off { get { return m_CurrentMode?.mode == "off"; } }
        public bool CurrentMode_Color { get { return m_CurrentMode?.mode == "color"; } }
        public bool CurrentMode_Movie { get { return m_CurrentMode?.mode == "movie"; } }
        public bool CurrentMode_Demo { get { return m_CurrentMode?.mode == "demo"; } }
        public bool CurrentMode_Realtime { get { return m_CurrentMode?.mode == "rt"; } }


        private MergedEffectsResult effects;
        public MergedEffectsResult Effects
        {
            get { return effects; }
            set
            {
                effects = value;
                OnPropertyChanged();
            }
        }

        private MQTTConfigResult mqttconfig;
        public MQTTConfigResult MQTTConfig
        {
            get { return mqttconfig; }
            set
            {
                mqttconfig = value;
                OnPropertyChanged();
            }
        }

        private LedConfigResult ledconfg = new LedConfigResult();
        public LedConfigResult LedConfig
        {
            get { return ledconfg; }
            set
            {
                ledconfg = value;
                OnPropertyChanged();
            }
        }

        private CurrentMovieConfig currentmovie;// = new CurrentMovieConfig();
        public CurrentMovieConfig CurrentMovie
        {
            get { return currentmovie; }
            set
            {
                currentmovie = value;
                OnPropertyChanged();
            }
        }

        private BrightnessResult brightness = new BrightnessResult() { mode = "disabled", value = 100 };
        public BrightnessResult Brightness
        {
            get { return brightness; }
            set
            {
                Debug.Assert(value != null);
                brightness = value;
                OnPropertyChanged();
                OnPropertyChanged("SliderBrightness");
            }
        }

        public int SliderBrightness
        {
            get { return Brightness.value; }
            set
            {
                if (value != Brightness.value)
                {
                    updateBrightness((byte)value).Wait(100);
                }
            }
        }
        private async Task updateBrightness(byte b)
        {
            VerifyResult result = await twinklyapi.SetBrightness(b);
            if (result.code != 1000)
                Debug.WriteLine($"Set Brightness fail - {result.code}");
            Brightness = await twinklyapi.GetBrightness();
        }


        double m_HueSliderValue;
        public double HueSliderValue
        {
            get => m_HueSliderValue;
            set
            {
                m_HueSliderValue = value;
                OnPropertyChanged();
            }
        }

        //  Update device from slider value
        public async Task UpdateColorAsync()
        {
            VerifyResult result = await twinklyapi.SetLedColor(new HSV() { hue = (int)(HueSliderValue * 360.0), saturation = 255, value = 255 });
            if (!result.IsOK)
                Debug.WriteLine($"Set Color fail - {result.code}");
        }




        System.Timers.Timer _frameTimer;
        Stopwatch _stopwatch;

        public bool RealtimeTestRunning
        {
            get => _frameTimer != null;
            set
            {
                RealtimeTest_Click(this);
                OnPropertyChanged();
            }
        }

        public void RealtimeTest_Click(object sender)
        {
            if (RealtimeTestRunning)
                StopRealtimeTest();
            else
                StartRealtimeTest();
        }

        public void StopRealtimeTest()
        {
            _frameTimer?.Stop();
            _frameTimer = null;
        }

        public void StartRealtimeTest()
        {
            _frameTimer = new System.Timers.Timer { AutoReset = true, Interval = 10 };
            _frameTimer.Elapsed += OnFrameTimerElapsed;
            _frameTimer.Start();
            _stopwatch = new Stopwatch();
            _stopwatch.Start();
            _random.NextBytes(frameData);
        }

        Random _random = new Random();

        private byte[] frameData = new byte[660*3];

        private void OnFrameTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //_random.NextBytes(frameData);

            for (int i = 0; i < frameData.Length; ++i)
            {
                //int v = frameData[i];
                //frameData[i] = (byte)(((v&1)==1)
                //    ? (frameData[i] > 1 ? frameData[i] - 2 : 0) 
                //    : (frameData[i] < 254 ? frameData[i] + 2 : 255));

                double t = _stopwatch.ElapsedMilliseconds * 0.001;
                int n = i / 3;
                frameData[i] = (byte)(127.5 + 127.5 * Math.Sin(t + n * 0.5));
                frameData[++i] = (byte)(127.5 + 127.5 * Math.Sin(t * 0.95 + n * 0.5));
                frameData[++i] = (byte)(127.5 + 127.5 * Math.Sin(t * 0.90 + n * 0.5));
            }

            twinklyapi.SendFrame(frameData);
        }


        #region IDataErrorInfo

        private string errtext = string.Empty;
        string IDataErrorInfo.Error
        {
            get { return errtext; }
        }

        public object ErrorContent { get; set; }

        string IDataErrorInfo.this[string columnName]
        {
            get
            {
                if (columnName == "ScheduleOnText")
                {
                    if (isScheduleOnTextValid())
                    {
                        return null;
                    }
                    else
                    {
                        return "What Time?";
                    }
                }

                if (columnName == "ScheduleOffText")
                {
                    if (isScheduleOffTextValid())
                    {
                        return null;
                    }
                    else
                    {
                        return "What Time?";
                    }
                }

                // If there's no error, null gets returned
                return null;
            }
        }

        private bool isScheduleOnTextValid()
        {
            if (string.IsNullOrWhiteSpace(ScheduleOnText))
                return false;

            if (ScheduleOnText.Trim() == "-1")
                return true;
            if (DateTime.TryParse(ScheduleOnText, out _))
                return true;

            return false;
        }

        private bool isScheduleOffTextValid()
        {
            if (string.IsNullOrWhiteSpace(ScheduleOffText))
                return false;

            if (ScheduleOffText.Trim() == "-1")
                return true;
            if (DateTime.TryParse(ScheduleOffText, out _))
                return true;

            return false;
        }

        #endregion

        public bool ReloadNeeded { get; private set; } = false;

        public void Reload()
        {
            //twinklyapi.data = new DataAccess() { IPAddress = value };
            ReloadNeeded = true;
            //OnPropertyChanged();
            OnPropertyChanged("twinklyapi");
            Unload();
        }

        //  Clear all view model fields to reset display
        internal void Unload()
        {
            //await m_apiSemaphore.WaitAsync();

            try
            {
                Gestalt = null;
                FW = null;
                Timer = new Timer() { time_on = -1, time_off = -1 };
                CurrentMode = null;
                Effects = null;
                Brightness = new BrightnessResult() { mode = "disabled", value = 100 };
                MQTTConfig = null;
                CurrentMovie = null;
                LedConfig = null;
                //Message = "Unloaded";
            }
            //catch (Exception err)
            //{
            //    Message = $"Error during update: {err.Message}";
            //}
            finally
            {
                //m_apiSemaphore.Release();
            }
        }

        //  Performs all initial queries (not requiring authentication),
        //  and authenticates
        internal async Task Load()
        {
            //Debug.Assert(m_apiSemaphore.CurrentCount == 0);

            //Debug.Assert(TwinklyDetected);

            //Message = "Loading...";

            //gestalt
            Gestalt = await twinklyapi.Info();
            if (twinklyapi.Status != (int)HttpStatusCode.OK)
            {
                Message = $"GetInfo failed ({twinklyapi.Status.ToString()})";
                return;
            }

            FW = await twinklyapi.Firmware();
            if (twinklyapi.Status != (int)HttpStatusCode.OK)
            {
                Message = $"GetFirmware failed ({twinklyapi.Status.ToString()})";
                return;
            }


            if (twinklyapi.Authenticated)
            {
                Message = $"Login Success until {twinklyapi.data.ExpiresAt:g}";
            }
            else
            {
                Message = "Authenticating...";
                if (!await twinklyapi.Login())
                    Message = $"Login Fail {twinklyapi.Status}";
                else
                    Message = $"Login Success until {twinklyapi.data.ExpiresAt:g}";
            }

            // update the authenticated api models
            //await UpdateAuthModels();

            ReloadNeeded = false;
        }

        internal async Task UpdateAuthModels()
        {
            //Debug.Assert(m_apiSemaphore.CurrentCount == 0);

            try
            {
                if (!twinklyapi.Authenticated)
                    return;

                //Gestalt = await twinklyapi.Info();

                // update the authenticated api models
                Timer = await twinklyapi.GetTimer();
                CurrentMode = await twinklyapi.GetOperationMode();
                if (CurrentMode_Color)
                {
                    var ledColorResult = await twinklyapi.GetLedColor();
                    HueSliderValue = ledColorResult.hue / 360.0;
                }
                Effects = await twinklyapi.EffectsAllinOne();
                Brightness = await twinklyapi.GetBrightness();
                MQTTConfig = await twinklyapi.GetMQTTConfig();
                CurrentMovie = await twinklyapi.GetMovieConfig();
                LedConfig = await twinklyapi.GetLedConfig();
            }
            catch (Exception err)
            {
                Message = $"Error during update: {err.Message}";
            }
        }

        /// <summary>
        /// Command to call the API to change the mode
        /// </summary>
        public async Task ChangeMode(string mode)
        {
            VerifyResult result;
            switch (mode)
            {
                case "off":
                    result = await twinklyapi.SetOperationMode(LedModes.off);
                    break;

                case "color":
                    result = await twinklyapi.SetOperationMode(LedModes.color);
                    break;

                case "demo":
                    result = await twinklyapi.SetOperationMode(LedModes.demo);
                    break;

                case "movie":
                    result = await twinklyapi.SetOperationMode(LedModes.movie);
                    break;

                case "rt":
                default:
                    result = await twinklyapi.SetOperationMode(LedModes.rt);
                    break;
            }

            // refresh gui
            if (result.code == 1000)
                CurrentMode = await twinklyapi.GetOperationMode();
        }

        public async Task ChangeTimer()
        {

            if (isScheduleOnTextValid() && isScheduleOffTextValid())
            {
                int on;
                int off;
                if (ScheduleOnText.Trim() == "-1")
                    on = -1;
                else
                {
                    var dton = DateTime.Parse(ScheduleOnText);
                    on = (int)(dton - DateTime.Today).TotalSeconds;
                }

                if (ScheduleOffText.Trim() == "-1")
                    off = -1;
                else
                {
                    var dtoff = DateTime.Parse(ScheduleOffText);
                    off = (int)(dtoff - DateTime.Today).TotalSeconds;
                }
                VerifyResult result = await twinklyapi.SetTimer(DateTime.Now, on, off);

                // refresh gui
                if (result.code == 1000)
                {
                    ScheduleOnText = null;
                    ScheduleOffText = null;
                    Timer = await twinklyapi.GetTimer();
                }
            }
        }

    }
}
