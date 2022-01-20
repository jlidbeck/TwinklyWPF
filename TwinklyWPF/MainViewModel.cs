using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Twinkly_xled.JSONModels;
using Twinkly_xled;
using System;
using System.Windows.Input;
using GalaSoft.MvvmLight.Command;
using System.Windows.Media;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;

namespace TwinklyWPF
{
    // --------------------------------------------------------------------------
    //       API docs - https://xled-docs.readthedocs.io/en/latest/rest_api.html
    // Python library - https://github.com/scrool/xled
    // --------------------------------------------------------------------------

    public class MainViewModel : INotifyPropertyChanged, IDataErrorInfo
    {
        public XLedAPI twinklyapi { get; private set; }

        public RelayCommand<string> ModeCommand { get; private set; }
        public RelayCommand UpdateTimerCommand { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private System.Timers.Timer _updateTimer;

        private bool _twinklyDetected = false;
        public bool TwinklyDetected
        {
            get { return _twinklyDetected; }
            private set
            {
                _twinklyDetected = value;
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
                m_CurrentMode = value;
                OnPropertyChanged();
                OnPropertyChanged("CurrentMode_Movie");
                OnPropertyChanged("CurrentMode_Off");
                OnPropertyChanged("CurrentMode_Demo");
                OnPropertyChanged("CurrentMode_Realtime");
            }
        }

        public bool CurrentMode_Movie { get { return m_CurrentMode.mode == "movie"; } }
        public bool CurrentMode_Off { get { return m_CurrentMode.mode == "off"; } }
        public bool CurrentMode_Demo { get { return m_CurrentMode.mode == "demo"; } }
        public bool CurrentMode_Realtime { get { return m_CurrentMode.mode == "rt"; } }


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

        private CurrentMovieConfig currentmovie = new CurrentMovieConfig();
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


        // Set by view so our colour are calculated from the same source
        public GradientStopCollection GradientStops { get; set; }

        private System.Timers.Timer m_colorSliderPauseTimer;
        private Color TargetColor;

        private double currentcolor;
        public double SliderColor
        {
            get { return currentcolor; }
            set
            {
                if (value != currentcolor)
                {
                    currentcolor = value;
                    TargetColor = GradientStops.GetRelativeColor(value);

                    // user can keep sliding - wait for 1sec of no movement to change color
                    if (m_colorSliderPauseTimer != null)
                        m_colorSliderPauseTimer.Dispose();
                    m_colorSliderPauseTimer = new System.Timers.Timer { Interval = 500, AutoReset = false };
                    m_colorSliderPauseTimer.Elapsed += ElapsedUpdateColor;
                    m_colorSliderPauseTimer.Start();
                }
            }
        }

        private void ElapsedUpdateColor(object sender, System.Timers.ElapsedEventArgs e)
        {
            Debug.WriteLine($"Slider Color {TargetColor}");
            UpdateColorAsync(TargetColor).Wait(100);
        }

        private async Task UpdateColorAsync(Color c)
        {
            await twinklyapi.SingleColor(new byte[3] { c.R, c.G, c.B });
        }




        System.Timers.Timer _frameTimer;
        Stopwatch _stopwatch;

        public void RealtimeTest_Click(object sender)
        {
            if (_frameTimer != null)
            {
                _frameTimer.Stop();
                _frameTimer = null;
                return;
            }

            _frameTimer = new System.Timers.Timer { AutoReset = true, Interval = 10 };
            _frameTimer.Elapsed += OnFrameTimerElapsed;
            _frameTimer.Start();
            _stopwatch = new Stopwatch();
            _stopwatch.Start();
            _random.NextBytes(frameData);
        }

        Random _random = new Random();

        private byte[] frameData = new byte[60];

        private void OnFrameTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //random.NextBytes(frameData);

            for (int i = 0; i < frameData.Length; ++i)
            {
                int v = frameData[i];
                frameData[i] = (byte)(((v&1)==1)
                    ? (frameData[i] > 1 ? frameData[i] - 2 : 0) 
                    : (frameData[i] < 254 ? frameData[i] + 2 : 255));
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

        public MainViewModel(IReadOnlyList<string> arguments)
        {
        }

        // one-time initialization
        async public Task Initialize()
        {
            twinklyapi = new XLedAPI();

            await Locate();   // TODO: move?

            ModeCommand = new RelayCommand<string>(async (x) => await ChangeMode(x));

            UpdateTimerCommand = new RelayCommand(async () => await ChangeTimer());


            m_updateTimer = new System.Timers.Timer(1000) { AutoReset = true };
            m_updateTimer.Elapsed += refreshGui;
            m_updateTimer.Start();

            // notify that twinklyapi.Devices has changed
            OnPropertyChanged("twinklyapi");

            // only load if the API detected the Twinkly at startup
            if (TwinklyDetected)
                await Load();
        }

        public async Task Locate()
        {
            Message = "Searching...";

            await twinklyapi.Locate();

            if (twinklyapi.Status == (int)HttpStatusCode.RequestTimeout)
            {
                Message = "Twinkly Not Found !";
            }

            TwinklyDetected = (twinklyapi.Status == 0 && twinklyapi.Devices?.Count() > 0);

            Message = $"Found {twinklyapi.Devices?.Count()} devices.";
        }

        private async Task Load()
        {
            try
            {
                Message = "Loading...";

                //gestalt
                Gestalt = await twinklyapi.Info();
                if (twinklyapi.Status == (int)HttpStatusCode.OK)
                {
                    FW = await twinklyapi.Firmware();
                }

                if (twinklyapi.Status == (int)HttpStatusCode.OK)
                {
                    if (!await twinklyapi.Login())
                        Message = $"Login Fail {twinklyapi.Status}";
                    else
                        Message = $"Login Success until {twinklyapi.ExpiresAt:g}";
                }
                else
                    Message = $"ERROR: {twinklyapi.Status}";

                // update the authenticated api models
                await UpdateAuthModels();

                _updateTimer = new System.Timers.Timer(1000) { AutoReset = true };
                _updateTimer.Elapsed += refreshGui;
                _updateTimer.Start();
            }
            catch (Exception ex)
            {
                Message = $"Exception Loading {ex.Message}";
            }
        }

        private void refreshGui(object sender, System.Timers.ElapsedEventArgs e)
        {
            UpdateAuthModels().Wait(100);
        }

        private async Task UpdateAuthModels()
        {
            Gestalt = await twinklyapi.Info();

            // update the authenticated api models
            if (twinklyapi.Authenticated)
            {
                Timer = await twinklyapi.GetTimer();
                CurrentMode = await twinklyapi.GetOperationMode();
                Effects = await twinklyapi.EffectsAllinOne();
                Brightness = await twinklyapi.GetBrightness();
                MQTTConfig = await twinklyapi.GetMQTTConfig();
                CurrentMovie = await twinklyapi.GetMovieConfig();
                LedConfig = await twinklyapi.GetLedConfig();
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// Command to call the API to change the mode
        /// </summary>
        private async Task ChangeMode(string mode)
        {
            VerifyResult result;
            switch (mode)
            {
                case "off":
                    result = await twinklyapi.SetOperationMode(LedModes.off);
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

        private async Task ChangeTimer()
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
