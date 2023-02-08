using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Twinkly_xled.JSONModels;
using Twinkly_xled;

namespace TwinklyWPF
{
    public class Device : INotifyPropertyChanged, IDataErrorInfo
    {
        public XLedAPI twinklyapi { get; private set; } = new XLedAPI();

        public event PropertyChangedEventHandler PropertyChanged;

        public Device(IPAddress ipAddress)
        {
            twinklyapi.IPAddress = ipAddress;
        }

        #region Gestalt shortcuts

        public string FriendlyName
        {
            get
            {
                if (Gestalt == null)
                    return twinklyapi.data.IPAddress.ToString();
                return $"{Gestalt.device_name} [{Gestalt.number_of_led}x{Gestalt.led_profile}]";
            }
        }

        //  should always be the device name. but if accessed before gestalt has been retrieved,
        //  all we have is the IP address
        public string UniqueName
        {
            get
            {
                if (Gestalt == null)
                    return twinklyapi.data.IPAddress.ToString();
                return Gestalt.device_name;
            }
        }

        public TimeSpan Uptime
        {
            get
            {
                if (Gestalt == null)
                    return TimeSpan.Zero;
                return TimeSpan.FromMilliseconds(double.Parse(Gestalt.uptime));
            }
        }

        #endregion

        #region overrides

        public override string ToString()
        {
            return FriendlyName;
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

        private GestaltResult _gestalt;
        public GestaltResult Gestalt
        {
            get { return _gestalt; }
            private set
            {
                _gestalt = value;
                OnPropertyChanged();
                OnPropertyChanged("Uptime");
                OnPropertyChanged("FriendlyName");
                OnPropertyChanged("UniqueName");
            }
        }

        private string _firmwareVersion;
        public string FirmwareVersion
        {
            get { return _firmwareVersion; }
            private set
            {
                _firmwareVersion = value;
                OnPropertyChanged();
            }
        }

        // Schedule Twinkly On and off

        private Twinkly_xled.JSONModels.Timer _timer;
        public Twinkly_xled.JSONModels.Timer Timer
        {
            get { return _timer; }
            set
            {
                Debug.Assert(value != null);
                _timer = value;
                OnPropertyChanged();
                if (string.IsNullOrWhiteSpace(ScheduleOffText))
                    ScheduleOffText = value.time_off == -1 ? "-1" : new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day).AddSeconds(value.time_off).ToString("HH:mm");
                if (string.IsNullOrWhiteSpace(ScheduleOnText))
                    ScheduleOnText = value.time_on == -1 ? "-1" : new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day).AddSeconds(value.time_on).ToString("HH:mm");
            }
        }

        private string _scheduleOnText;
        public string ScheduleOnText
        {
            get { return _scheduleOnText; }
            set
            {
                _scheduleOnText = value;
                OnPropertyChanged();
            }
        }

        private string _scheduleOffText;
        public string ScheduleOffText
        {
            get { return _scheduleOffText; }
            set
            {
                _scheduleOffText = value;
                OnPropertyChanged();
            }
        }



        private Mode _currentMode;
        public Mode CurrentMode
        {
            get { return _currentMode; }
            private set
            {
                if (value?.mode != _currentMode?.mode)
                {
                    _currentMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged("CurrentMode_Off");
                    OnPropertyChanged("CurrentMode_Color");
                    OnPropertyChanged("CurrentMode_Movie");
                    OnPropertyChanged("CurrentMode_Demo");
                    OnPropertyChanged("CurrentMode_Realtime");

                    //StopRealtimeTest();
                }
            }
        }

        public bool CurrentMode_Off { get { return _currentMode?.mode == "off"; } }
        public bool CurrentMode_Color { get { return _currentMode?.mode == "color"; } }
        public bool CurrentMode_Movie { get { return _currentMode?.mode == "movie"; } }
        public bool CurrentMode_Demo { get { return _currentMode?.mode == "demo"; } }
        public bool CurrentMode_Realtime { get { return _currentMode?.mode == "rt"; } }


        private MergedEffectsResult _effects;
        public MergedEffectsResult Effects
        {
            get { return _effects; }
            set
            {
                _effects = value;
                OnPropertyChanged();
            }
        }

        private MQTTConfigResult _mqttConfig;
        public MQTTConfigResult MQTTConfig
        {
            get { return _mqttConfig; }
            set
            {
                _mqttConfig = value;
                OnPropertyChanged();
            }
        }

        private LedConfigResult _ledConfig;
        public LedConfigResult LedConfig
        {
            get { return _ledConfig; }
            set
            {
                _ledConfig = value;
                OnPropertyChanged();
            }
        }

        private CurrentMovieConfig _currentMovie;// = new CurrentMovieConfig();
        public CurrentMovieConfig CurrentMovie
        {
            get { return _currentMovie; }
            set
            {
                _currentMovie = value;
                OnPropertyChanged();
            }
        }

        private BrightnessResult _brightness = null;// new BrightnessResult() { mode = "disabled", value = 100 };
        public BrightnessResult Brightness
        {
            get { return _brightness; }
            set
            {
                Debug.Assert(value != null);
                _brightness = value;
                OnPropertyChanged();
                OnPropertyChanged("ActiveDevice.SliderBrightness");
            }
        }

        public int SliderBrightness
        {
            get { return Brightness == null ? 0 : Brightness.enabled ? Brightness.value : 100; }
            set
            {
                if (value != Brightness?.value)
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


        double _hueSliderValue;
        public double HueSliderValue
        {
            get => _hueSliderValue;
            set
            {
                _hueSliderValue = value;
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

        public bool ReloadNeeded => Gestalt == null;

        //  Clear all view model fields to reset display
        internal void Unload()
        {
            //await _apiSemaphore.WaitAsync();

            try
            {
                Gestalt = null;
                FirmwareVersion = null;
                Timer = new Twinkly_xled.JSONModels.Timer() { time_on = -1, time_off = -1 };
                CurrentMode = null;
                Effects = null;
                Brightness = null;
                MQTTConfig = null;
                CurrentMovie = null;
                LedConfig = null;
                //Message = "Unloaded";
                //ReloadNeeded = true;
            }
            finally
            {
                //_apiSemaphore.Release();
            }
        }

        //  Update gestalt and firmware info, which does not require authentication.
        //  These values are not volatile or likely to change very often,
        //  with the exception of Gestalt.uptime
        internal async Task Load()
        {
            //Debug.Assert(_apiSemaphore.CurrentCount == 0);

            var stopwatch = Stopwatch.StartNew();

            Message = "Loading...";

            //Thread.Yield();
            //Thread.Sleep(500);

            //gestalt
            Gestalt = await twinklyapi.GetGestalt();
            if (twinklyapi.Status != (int)HttpStatusCode.OK)
            {
                Message = $"GetInfo failed ({twinklyapi.Status.ToString()})";
                throw new Exception(Message);
            }

            var fwResult = await twinklyapi.GetFirmwareVersion();
            if (twinklyapi.Status != (int)HttpStatusCode.OK)
            {
                Message = $"GetFirmware failed ({twinklyapi.Status.ToString()})";
                throw new Exception(Message);
            }
            FirmwareVersion = fwResult.version;

            Message = $"Loaded gestalt at {Gestalt.Timestamp} ({stopwatch.ElapsedMilliseconds} ms)";
        }

        //  Read device state and settings. Requires authentication.
        //  Reauthenticates as needed if {autoreauth} is true.
        //  This function must only be called within a _apiSemaphore lock
        //  Reading all this data can be slow (200-400 ms) so not a good choice for polling
        internal async Task UpdateAuthModels(bool autoreauth)
        {
            //Debug.Assert(_apiSemaphore.CurrentCount == 0);
            var stopwatch = Stopwatch.StartNew();

            Message = "Loading Auth Models...";
            //Thread.Yield();
            //Thread.Sleep(300);

            try
            {
                if (!twinklyapi.Authenticated)
                {
                    if (!autoreauth)
                        return;

                    await Login();
                }

                if(twinklyapi.data.ExpiresIn < TimeSpan.FromHours(3.9))
                {
                    if (autoreauth)
                        await Login();
                }

                //Gestalt = await twinklyapi.GetGestalt();

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

                Message = $"Success {stopwatch.ElapsedMilliseconds} ms";
            }
            catch (Exception err)
            {
                Message = $"Error during update: {err.Message}";
            }
        }

        public async Task Login()
        {
            Message = "Authenticating...";
            if (!await twinklyapi.Login())
            {
                Message = $"Login Fail {twinklyapi.Status}";
                throw new Exception(Message);
            }

            Message = $"Login Success until {twinklyapi.data.ExpiresAt:g}";
            OnPropertyChanged("LoginExpiresAt");
        }

        bool _autoReAuth = false;
        public bool AutoReAuth
        {
            get
            {
                return _autoReAuth;
            }
            set
            {
                _autoReAuth = value;
                OnPropertyChanged();
            }
        }

        public DateTime LoginExpiresAt { get { return twinklyapi.data.ExpiresAt; } }

        /// <summary>
        /// Command to call the API to change the mode
        /// </summary>
        public async Task ChangeMode(string mode)
        {
            if (mode == CurrentMode?.mode)
                return;

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
                    result = await twinklyapi.SetOperationMode(LedModes.rt);
                    break;

                default:
                    throw new ArgumentException($"Invalid mode: '{mode}'");
            }

            // refresh gui
            if (result.IsOK)
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
                if (result.IsOK)
                {
                    ScheduleOnText = null;
                    ScheduleOffText = null;
                    Timer = await twinklyapi.GetTimer();
                }
            }
        }

    }
}
