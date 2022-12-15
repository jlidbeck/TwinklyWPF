using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Twinkly_xled.JSONModels;

namespace TwinklyWPF
{
    /// <summary>
    /// Interaction logic for LayoutWindow.xaml
    /// </summary>
    public partial class LayoutWindow : Window, INotifyPropertyChanged
    {
        public MainViewModel MainViewModel => (MainViewModel)DataContext;

        private GetLayoutResult _layoutResult;

        public GetLayoutResult LayoutResult
        {
            get { return _layoutResult; }
            private set { _layoutResult = value; OnPropertyChanged(); }
        }

        private Layout _layout;

        public Layout Layout
        {
            get { return _layout; }
            set { _layout = value; OnPropertyChanged(); }
        }

        private string _layoutText;

        public string LayoutText
        {
            get { return _layoutText; }
            private set { _layoutText = value; OnPropertyChanged(); }
        }

        private string _filename;

        private DispatcherTimer _updateTimer;

        public LayoutWindow()
        {
            InitializeComponent();

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            OnPropertyChanged("Layout");
            Redraw();
            _updateTimer = new DispatcherTimer(
                new System.TimeSpan(0, 0, 0, 0, 20),
                DispatcherPriority.Render,
                OnFrameTimerElapsed,
                Dispatcher.CurrentDispatcher);
            _updateTimer.Tag = this;
            _updateTimer.Start();

        }


        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion

        private void CenterButton_Click(object sender, RoutedEventArgs e)
        {
            Rect bounds = Rect.Empty;
            foreach (var point in Layout.coordinates)
            {
                bounds.Union(new Point(point.x, point.y));
            }

            ShiftPoints(-bounds.Left - bounds.Width / 2, -bounds.Top - bounds.Height / 2);
        }

        private void ShiftPoints(double dx, double dy)
        {
            for(int i=0; i<Layout.coordinates.Length;++i)
            {
                Layout.coordinates[i].x += dx;
                Layout.coordinates[i].y += dy;
            }
            LayoutResult = null;
            Redraw();
        }


        private async void GetButton_Click(object sender, RoutedEventArgs e)
        {
            GetLayoutResult result = await MainViewModel.ActiveDevice?.twinklyapi.GetLayout();

            LayoutResult = result;
            Layout = result;
            Redraw();
        }

        private async void SetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = await MainViewModel.ActiveDevice?.twinklyapi.SetLayout(LayoutResult);
            if (result.code != 200)
                MessageBox.Show($"Error {result.code}");
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
                    LayoutResult = null;
                    Layout = JsonSerializer.Deserialize<Layout>(str);
                    Redraw();
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
                    await JsonSerializer.SerializeAsync(stream, Layout, new JsonSerializerOptions { WriteIndented = true });
                }
            }
        }

        //  this is the point where the canvas size has been computed
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            App.Log($"{theCanvas.ActualWidth} x {theCanvas.ActualHeight}");
        }

        int _scale = 25;

        private void Redraw()
        {
            if(!(Layout?.coordinates?.Length > 0))
            {
                LayoutText = "empty";
                theCanvas.Children.Clear();
                return;
            }

            var msg = $"Points: {Layout.coordinates?.Length}";
            foreach (var pt in Layout.coordinates)
                msg += $"\n{pt.x}, {pt.y}, {pt.z}";
            LayoutText = msg;

            Rect bounds = Rect.Empty;
            theCanvas.Children.Clear();
            for (int i = -2; i <= 2; ++i)
            {
                theCanvas.Children.Add(new Line { Stroke = Brushes.DarkCyan, StrokeThickness = 1, X1 = i, Y1 = -1000, X2 = i, Y2 = 1000 });
                theCanvas.Children.Add(new Line { Stroke = Brushes.DarkCyan, StrokeThickness = 1, X1 = -1000, Y1 = i, X2 = 1000, Y2 = i });
            }
            foreach (var point in Layout.coordinates)
            {
                var dot = new Ellipse { Width = 3, Height = 3, Stroke = null, Fill = Brushes.YellowGreen };
                theCanvas.Children.Add(dot);
                // since spacing is typically 0.1, and canvas is fat pixely, scale up
                Canvas.SetLeft(dot, _scale * point.x);
                Canvas.SetTop(dot, _scale * point.y);
                bounds.Union(new Point(_scale * point.x, _scale * point.y));
            }
            bounds.Inflate(bounds.Width * 0.1, bounds.Width * 0.1);

            theCanvas.Width = bounds.Right;
            theCanvas.Height = bounds.Bottom;

            //double h = theCanvas.ActualHeight;
            //double w = theCanvas.ActualWidth;

            // flip vertically.
            // can't do much else with this transform matrix, since it affects the entire
            // canvas relative to its parent
            mt.Matrix = new Matrix(1, 0, 0, -1, 0, (bounds.Bottom > 0? bounds.Bottom : 300));
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
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch(e.Key)
            {
                case System.Windows.Input.Key.Escape:
                    this.DialogResult = false;
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
                --_scale;
            else
                ++_scale;
            Redraw();
        }

        private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Redraw();
        }

        private static void OnFrameTimerElapsed(object sender, EventArgs e)
        {
            ((LayoutWindow)((DispatcherTimer)sender).Tag).UpdateColors();
        }


    }
}
