using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TwinklyWPF.Controls
{
    /// <summary>
    /// Interaction logic for DeviceDetailsWindow.xaml
    /// </summary>
    public partial class DeviceDetailsWindow : UserControl
    {
        public Device Device => (Device)DataContext;

        public DeviceDetailsWindow()
        {
            InitializeComponent();
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
        }

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

            //MainViewModel.ModeCommand.Execute("color");

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
                    var _ = Device?.UpdateColorAsync();
                    m_hueSliderValueChanged = false;

                    m_hueSliderPauseTimer = new Timer() { Enabled = true, AutoReset = false, Interval = 200 };
                    m_hueSliderPauseTimer.Elapsed += OnHueSliderTimerElapsed;
                    m_hueSliderPauseTimer.Start();
                }
            });
        }

        #endregion

        private async void MovieConfig_Click(object sender, RoutedEventArgs e)
        {
            var window = new MovieConfigWindow { DataContext = DataContext, Owner = this.Parent as Window };
            window.MovieConfig = Device?.CurrentMovie;

            if (window.ShowDialog() == true)
            {
            }
        }

        private void ShowDetails_Click(object sender, RoutedEventArgs e)
        {
            object o;
            switch (((Button)e.Source).Name)
            {
                case "GestaltDetails": o = Device?.Gestalt; break;
                case "CurrentMovieDetails": o = Device?.CurrentMovie; break;
                case "CurrentModeDetails": o = Device?.CurrentMode; break;
                default: return;
            }
            var json = JsonSerializer.Serialize(
                o,
                new JsonSerializerOptions(JsonSerializerDefaults.General) { WriteIndented = true });
            Console.WriteLine($"{((Button)e.Source).Name}:");
            Console.WriteLine(json);
            MessageBox.Show(json, ((Button)e.Source).Name);
        }

        private void UpdateTimerCommand(object sender, RoutedEventArgs e)
        {

        }
    }
}
