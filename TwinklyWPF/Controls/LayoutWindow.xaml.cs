using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Twinkly_xled.JSONModels;
using TwinklyWPF.Utilities;

namespace TwinklyWPF
{
    /// <summary>
    /// Interaction logic for LayoutWindow.xaml
    /// </summary>
    public partial class LayoutWindow : Window, INotifyPropertyChanged
    {
        public MainViewModel MainViewModel => (MainViewModel)DataContext;

        // if dialog is currently displaying coordinates from GetLayout from a device, the raw response is stored here
        private GetLayoutResult _layoutResult;

        public GetLayoutResult LayoutResult
        {
            get { return _layoutResult; }
            private set { _layoutResult = value; OnPropertyChanged(); }
        }

        // concatenation of all device coordinates
        private XYZ[] _coordinates;

        public XYZ[] Coordinates
        {
            get { return _coordinates; }
            set
            {
                _coordinates = (XYZ[])value?.Clone();
                OnPropertyChanged();
                // todo: apply layout changes from dialog to realtime.
                //if (MainViewModel.RTMovie != null)
                //    MainViewModel.RTMovie.Layout = value;
            }
        }

        private string _layoutText;

        public string LayoutText
        {
            get { return _layoutText; }
            private set { _layoutText = value; OnPropertyChanged(); }
        }

        private string _layoutBoundsText;

        public string LayoutBoundsText
        {
            get { return _layoutBoundsText; }
            private set { _layoutBoundsText = value; OnPropertyChanged(); }
        }

        private string _filename;

        // polling UI update timer
        private DispatcherTimer _updateTimer;

        #region Initialization

        public LayoutWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Coordinates = MainViewModel.RTMovie?.Layout?.coordinates;

            _updateTimer = new DispatcherTimer(
                new TimeSpan(0, 0, 0, 0, 20),   // ms
                DispatcherPriority.Render,
                OnUpdateTimerElapsed,
                Dispatcher.CurrentDispatcher);
            _updateTimer.Tag = this;
            _updateTimer.Start();

            MainViewModel.PropertyChanged += MainViewModel_PropertyChanged;
        }

        private void MainViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // What we're listening for is any change affecting RTMovie.Layout,
            // so we know to update our coordinates and redraw the canvas
            if (e.PropertyName == "RTMovie" || e.PropertyName == "RealtimeMovieRunning")
            {
                if (MainViewModel.RTMovie?.Layout?.coordinates != null)
                {
                    Coordinates = MainViewModel.RTMovie?.Layout?.coordinates;
                }
            }
        }

        #endregion

        #region INotifyPropertyChanged boilerplate

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

            switch(name)
            {
                case "Coordinates":
                    Redraw();
                break;
            }
        }

        #endregion

        #region Layout controls

        private void CenterButton_Click(object sender, RoutedEventArgs e)
        {
            Rect bounds = GetLayoutBounds();

            ShiftPoints(-bounds.Left - bounds.Width / 2, -bounds.Top - bounds.Height / 2);
        }

        public Rect GetLayoutBounds()
        {
            Rect bounds = Rect.Empty;
            foreach (var point in Coordinates)
            {
                bounds.Union(new Point(point.x, point.y));
            }
            LayoutBoundsText = $"({bounds.Left}, {bounds.Bottom} - {bounds.Right}, {bounds.Top}";
            return bounds;
        }

        private void ShiftPoints(double dx, double dy)
        {
            for(int i=0; i<Coordinates.Length;++i)
            {
                Coordinates[i].x += dx;
                Coordinates[i].y += dy;
            }
            LayoutResult = null;
            Redraw();
        }

        private void NoiseButton_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < Coordinates.Length; ++i)
            {
                double dx = 0.1 * Gaussian.NextDouble();
                double dy = 0.1 * Gaussian.NextDouble();
                Coordinates[i].x += dx;
                Coordinates[i].y += dy;
            }
            LayoutResult = null;
            Redraw();
        }

        private async void GetButton_Click(object sender, RoutedEventArgs e)
        {
            LayoutResult = await MainViewModel.ActiveDevice?.twinklyapi.GetLayout();
            Coordinates = (XYZ[])LayoutResult.coordinates.Clone();
            Redraw();
        }

        private void SetButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: send modified layout(s) to devices
            //var result = await MainViewModel.ActiveDevice?.twinklyapi.SetLayout(Layout);
            //ResultCode code = (ResultCode)result.code;
            //if (code == ResultCode.Ok)
            //    MessageBox.Show($"Success. {result.parsed_coordinates} coordinates parsed.");
            //else
            //    MessageBox.Show($"Error {result.code}");
        }

        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { DefaultExt = "json", Filter = "json|*.json" };
            if (dialog.ShowDialog() == true)
            {
                _filename = dialog.FileName;
                var stream = dialog.OpenFile();
                using (var sr = new StreamReader(stream))
                {
                    var str = await sr.ReadToEndAsync();
                    var layout = JsonSerializer.Deserialize<Layout>(str);
                    if (layout?.IsValid != true)
                    {
                        MessageBox.Show("JSON deserialize failed:\n\n" + str);
                        return;
                    }

                    LayoutResult = null;
                    Coordinates = (XYZ[])layout.coordinates.Clone();
                }
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { DefaultExt="json", AddExtension=true, Filter= "json|*.json", FileName= _filename };
            if (dialog.ShowDialog() == true)
            {
                var stream = dialog.OpenFile();
                //using (var sw = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    await JsonSerializer.SerializeAsync(stream, Coordinates, new JsonSerializerOptions { WriteIndented = true });
                }
            }
        }

        #endregion

        #region Drawing

        //  this is the point where the canvas size has been computed
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            App.Log($"{theCanvas.ActualWidth} x {theCanvas.ActualHeight}");
        }

        int _scale = 25;

        private void Redraw()
        {
            if(!(Coordinates?.Length > 0))
            {
                LayoutText = "empty";
                theCanvas.Children.Clear();
                return;
            }

            var msg = $"Points: {Coordinates?.Length}";
            foreach (var pt in Coordinates)
                msg += $"\n{pt.x}, {pt.y}, {pt.z}";
            LayoutText = msg;// $"Actual size: {ActualWidth} x {ActualHeight}";

            // update text
            GetLayoutBounds();

            Rect bounds = Rect.Empty;

            theCanvas.Children.Clear();
            foreach (var point in Coordinates)
            {
                var dot = new Ellipse { Width = 2, Height = 2, Stroke = null, Fill = Brushes.YellowGreen };
                theCanvas.Children.Add(dot);
                // since spacing is typically 0.1, and canvas is fat pixely, scale up
                Canvas.SetLeft(dot, _scale * point.x);
                Canvas.SetTop(dot, _scale * point.y);
                bounds.Union(new Point(_scale * point.x, _scale * point.y));
            }

            // Create a background rectangle and place it at lowest Z-order
            bounds.Inflate(bounds.Width * 0.05, bounds.Width * 0.05);
            var background = new Rectangle { Width=bounds.Width, Height=bounds.Height, Fill=Brushes.Black };
            theCanvas.Children.Insert(0, background);
            Canvas.SetLeft(background, bounds.X);
            Canvas.SetTop(background, bounds.Y);

            // Create axis lines and place them just above background
            int i = 0;

            var line = new Line { Stroke = Brushes.DarkSlateBlue, StrokeThickness = 0.8, SnapsToDevicePixels = false };
            line.X1 = line.X2 = 0.5 + i; line.Y1 = bounds.Top; line.Y2 = bounds.Bottom;
            theCanvas.Children.Insert(1, line);
                
            line = new Line { Stroke = Brushes.DarkSlateBlue, StrokeThickness = 1.0, SnapsToDevicePixels = false };
            line.Y1 = line.Y2 = 0.5 + i; line.X1 = bounds.Left; line.X2 = bounds.Right;
            theCanvas.Children.Insert(2, line);

            theCanvas.RenderTransform = new MatrixTransform(1, 0, 0, -1, -bounds.Left, -bounds.Top);
            theCanvas.Width = bounds.Width;
            theCanvas.Height = bounds.Height;
        }

        private static void OnUpdateTimerElapsed(object sender, EventArgs e)
        {
            ((LayoutWindow)((DispatcherTimer)sender).Tag).UpdateColors();
        }

        private void UpdateColors()
        {
            if (!(MainViewModel.RTMovie?.FrameData?.Length > 0))
            {
                return;
            }

            var frame = MainViewModel.RTMovie.FrameData;
            int i = 0;
            foreach (FrameworkElement elm in theCanvas.Children)
            {
                var dot = elm as Ellipse;
                if (dot != null)
                {
                    dot.Fill = new SolidColorBrush(Color.FromRgb(frame[i], frame[i + 1], frame[i + 2]));
                    i += 3;
                }
            }

            // update the text fields
            MovieRunningText.Text = MainViewModel.RTMovie?.Running == true
                ? (MainViewModel.RTMovie.PreviewMode ? "Running*" : "Running")
                : "Stopped";
            StartStopButton.Content = MainViewModel.RTMovie?.Running == true ? "■ Stop" : "► Start";
            MovieTimeText.Text = String.Format("{0:0.00}", MainViewModel.RTMovie.CurrentTime);

            var palette = MainViewModel.RTMovie.CurrentPalette;
            if (palette?.Count > 0)
            {
                var paletteControls = new Run[] { MoviePalette0, MoviePalette1, MoviePalette2, MoviePalette3, MoviePalette4 };
                int j = 0;
                for (; j < palette.Count && j < paletteControls.Length; j++)
                {
                    paletteControls[j].Background = ColorToBrush(palette[j].GetColor());
                    paletteControls[j].Foreground = ColorToBrush(palette[j].TargetColor);
                }
                for (; j < paletteControls.Length; j++)
                {
                    paletteControls[j].Background = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                    paletteControls[j].Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                }
            }

            IdleEventTimeText.Text = $"{MainViewModel.RTMovie.IdleEventTime:0.0}";
            IdleTimeText.Text = $"{MainViewModel.RTMovie.IdleTime:0.0}";
            PianoIdleTimeText.Text = $"{MainViewModel.RTMovie.Piano?.IdleTime:0.0}";
        }

        #endregion

        #region Canvas controls

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch(e.Key)
            {
                case System.Windows.Input.Key.Escape:
                    this.Close();
                    break;
                case System.Windows.Input.Key.Left:
                    ShiftPoints(-0.1, 0);
                    break;
                case System.Windows.Input.Key.Right:
                    ShiftPoints(0.1, 0);
                    break;
                case System.Windows.Input.Key.Up:
                    ShiftPoints(0, 0.1);
                    break;
                case System.Windows.Input.Key.Down:
                    ShiftPoints(0, -0.1);
                    break;
            }
        }

        private void Canvas_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (e.Delta < 0)
                _scale = Math.Max(1, (_scale * 4) / 5);     // *=4/5
            else
                _scale = _scale + Math.Max(1, _scale / 4);  // *=5/4
            Redraw();
        }

        private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Redraw();
        }

        #endregion

        #region Animation controls

        public static Color ConvertColor(double[] rgb)
        {
            return Color.FromScRgb(1, (float)rgb[0], (float)rgb[1], (float)rgb[2]);
        }

        public static SolidColorBrush ColorToBrush(double[] rgb)
        {
            return new SolidColorBrush(Color.FromRgb(
                (byte)(Math.Clamp(rgb[0] * 255.0, 0, 255)), 
                (byte)(Math.Clamp(rgb[1] * 255.0, 0, 255)), 
                (byte)(Math.Clamp(rgb[2] * 255.0, 0, 255))
                ));
        }

        private async void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainViewModel.RealtimeMovieRunning)
                MainViewModel.StopRealtimeTest();
            else
                await MainViewModel.StartRealtimeTest();
        }

        private void PrevColorMode_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MainViewModel.RTMovie?.NextColorMode(-1);
        }

        private void NextColorMode_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MainViewModel.RTMovie?.NextColorMode();
        }

        private void MoviePalette_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton== System.Windows.Input.MouseButtonState.Pressed)
            {
                int colorIndex = -1;
                var button = (Run)sender;
                switch (button.Name)
                {
                    case "MoviePalette0": colorIndex = 0; break;
                    case "MoviePalette1": colorIndex = 1; break;
                    case "MoviePalette2": colorIndex = 2; break;
                    case "MoviePalette3": colorIndex = 3; break;
                    case "MoviePalette4": colorIndex = 4; break;
                }
                MainViewModel.RTMovie.RandomizeOneColor(colorIndex);
            }
            else
            {
                MainViewModel.RTMovie?.RandomizePalette();
            }
        }

        #endregion
    }
}
