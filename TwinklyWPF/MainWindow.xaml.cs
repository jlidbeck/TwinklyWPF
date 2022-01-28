using System;
using System.Text.Json;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TwinklyWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainViewModel MainViewModel => (MainViewModel)DataContext;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext != null)
            {
                await MainViewModel.Initialize();

                MainViewModel.GradientStops = SingleGradient.GradientStops.Clone();
            }
        }

        #region Device locate

        private async void Rescan_Click(object sender, RoutedEventArgs e)
        {
            await MainViewModel.Initialize();
        }

        private async void AddIpAddress_Click(object sender, RoutedEventArgs e)
        {
            await MainViewModel.FakeLocate();
        }
        
        private bool _devicesComboTextInputChanged = false;

        private void Devices_TextInput(object sender, TextChangedEventArgs e)
        {
            try
            {
                //var ipAddress = IPAddress.Parse(((ComboBox)sender).Text);
                //MainViewModel.twinklyapi.Devices.Add(new Twinkly_xled.Device(ipAddress));
                _devicesComboTextInputChanged = true;
            }
            catch (Exception _)
            {
                return;
            }
        }

        private async void Devices_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_devicesComboTextInputChanged)
                {
                    var ipAddress = System.Net.IPAddress.Parse(((ComboBox)sender).Text);
                    await MainViewModel.AddDevice(ipAddress);
                }
            }
            catch (Exception _)
            {
            }

            _devicesComboTextInputChanged = false;
        }

        #endregion

        #region Hue slider

        private Timer m_hueSliderPauseTimer;
        private bool m_hueSliderValueChanged = false;

        private void HueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (m_hueSliderPauseTimer != null)
            {
                // too soon, the timer is running so just set the dirty flag
                m_hueSliderValueChanged = true;
                return;
            }

            MainViewModel.ModeCommand.Execute("color");

            m_hueSliderValueChanged = true;
            OnHueSliderTimerElapsed(null, null);
        }

        //  If slider has moved since the last call,
        //  update the device immediately and reset the timer.
        //  Otherwise, let the timer expire.
        private void OnHueSliderTimerElapsed(object sender, ElapsedEventArgs e)
        {
            this.Dispatcher.Invoke(delegate ()
            {
                m_hueSliderPauseTimer?.Stop();
                m_hueSliderPauseTimer = null;

                if (m_hueSliderValueChanged)
                {
                    var v = HueSlider.Value;
                    var _ = MainViewModel.ActiveDevice?.UpdateColorAsync();
                    m_hueSliderValueChanged = false;

                    m_hueSliderPauseTimer = new Timer() { Enabled = true, AutoReset = false, Interval = 200 };
                    m_hueSliderPauseTimer.Elapsed += OnHueSliderTimerElapsed;
                    m_hueSliderPauseTimer.Start();
                }
            });
        }

        #endregion

        private async void GetLayoutTest_Click(object sender, RoutedEventArgs e)
        {
            var window = new LayoutWindow { DataContext = DataContext, Owner = this };
            window.Layout = MainViewModel.RTMovie?.Layout;

            if (window.ShowDialog() == true)
            {
                var result = await MainViewModel.ActiveDevice?.twinklyapi.GetLayout();
                var layout = result.coordinates;
                var msg = $"Points: {layout.Length} Source: {result.source} Synthesized: {result.synthesized}";
                foreach (var pt in layout)
                    msg += $"\n{pt.x}, {pt.y}, {pt.z}";

                MessageBox.Show(msg);

            }
        }

        private async void GetMovieConfigTest_Click(object sender, RoutedEventArgs e)
        {
            var config = await MainViewModel.ActiveDevice?.twinklyapi.GetMovieConfig();
            var content = JsonSerializer.Serialize(
                config,
                new JsonSerializerOptions(JsonSerializerDefaults.General) { WriteIndented = true });
            MessageBox.Show(content, $"GetMovieConfig: {(config.IsOK ? "Ok" : "FAILED")}");
            if (config.IsOK)
                MainViewModel.ActiveDevice.CurrentMovie = config;
        }

        private async void SetMovieConfigTest_Click(object sender, RoutedEventArgs e)
        {
            var config = MainViewModel.ActiveDevice?.CurrentMovie;
            var result = await MainViewModel.ActiveDevice?.twinklyapi.SetMovieConfig(config);
            MessageBox.Show($"SetMovieConfig result: {result.ToString()}");
        }

        private void ShowDetails_Click(object sender, RoutedEventArgs e)
        {
            object o;
            switch (((Button)e.Source).Name)
            {
                case "GestaltDetails":      o = MainViewModel.ActiveDevice?.Gestalt; break;
                case "CurrentMovieDetails": o = MainViewModel.ActiveDevice?.CurrentMovie; break;
                case "CurrentModeDetails":  o = MainViewModel.ActiveDevice?.CurrentMode; break;
                default: return;
            }
            var json = JsonSerializer.Serialize(
                o, 
                new JsonSerializerOptions(JsonSerializerDefaults.General) { WriteIndented = true });
            Console.WriteLine($"{((Button)e.Source).Name}:");
            Console.WriteLine(json);
            MessageBox.Show(json, ((Button)e.Source).Name);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (MainViewModel.RealtimeMovieRunning)
            {
                switch(e.Key)
                {
                    case Key.LeftCtrl: MainViewModel.RTMovie.KeyDown(0); break;
                    case Key.LeftShift: MainViewModel.RTMovie.KeyDown(1); break;
                    case Key.LeftAlt: MainViewModel.RTMovie.KeyDown(2); break;
                    case Key.Z: MainViewModel.RTMovie.ColorMode++; break;
                    case Key.X: MainViewModel.RTMovie.ChangeColors(); break;
                }

                MainViewModel.RTMovie.Inputs = (
                    (Keyboard.IsKeyDown(Key.LeftCtrl ) ? 1 : 0) |
                    (Keyboard.IsKeyDown(Key.LeftShift) ? 2 : 0) |
                    (Keyboard.IsKeyDown(Key.LeftAlt  ) ? 4 : 0)
                    );
            }
        }

        private void Window_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (MainViewModel.RealtimeMovieRunning)
            {
                switch (e.Key)
                {
                    case Key.LeftCtrl: MainViewModel.RTMovie.KeyUp(0); break;
                    case Key.LeftShift: MainViewModel.RTMovie.KeyUp(1); break;
                    case Key.LeftAlt: MainViewModel.RTMovie.KeyUp(2); break;
                }

                MainViewModel.RTMovie.Inputs = (
                    (Keyboard.IsKeyDown(Key.LeftCtrl ) ? 1 : 0) |
                    (Keyboard.IsKeyDown(Key.LeftShift) ? 2 : 0) |
                    (Keyboard.IsKeyDown(Key.LeftAlt  ) ? 4 : 0)
                    );
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                var placement = WindowPlacement.WindowPlacement.GetPlacement(this);
                App.Current.Settings.MainWindowPlacement = placement;
            }
            catch(Exception ex)
            {
                // non-critical failure
                Console.WriteLine($"Failed to save window placement settings: {ex.Message}");
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            if(App.Current.Settings?.MainWindowPlacement.IsValid == true)
                WindowPlacement.WindowPlacement.SetPlacement(this, App.Current.Settings.MainWindowPlacement);
        }
    }
}
