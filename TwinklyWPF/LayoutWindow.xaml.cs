using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
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


        public LayoutWindow()
        {
            InitializeComponent();

        }

        private void ThisLayoutWindow_Loaded(object sender, RoutedEventArgs e)
        {
            OnPropertyChanged("Layout");
            UpdateLayoutText();
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
            UpdateLayoutText();
        }


        private async void GetButton_Click(object sender, RoutedEventArgs e)
        {
            GetLayoutResult result = await MainViewModel.ActiveDevice?.twinklyapi.GetLayout();

            LayoutResult = result;
            Layout = result;
            UpdateLayoutText();
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
                    UpdateLayoutText();
                }
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { DefaultExt="json", AddExtension=true, Filter= "json|*.json", FileName= _filename };
            if (dialog.ShowDialog() == true)
            {
                var stream = dialog.OpenFile();
                using (var sw = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    JsonSerializer.Serialize(sw, Layout);
                }
            }
        }

        //  this is the point where the canvas size has been computed
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            App.Log($"{theCanvas.ActualWidth} x {theCanvas.ActualHeight}");
        }

        private void UpdateLayoutText()
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
                Canvas.SetLeft(dot, 20*point.x);
                Canvas.SetTop(dot, 20*point.y);
                bounds.Union(new Point(point.x, point.y));
            }
            bounds.Inflate(bounds.Width * 0.1, bounds.Width * 0.1);

            double h = theCanvas.ActualHeight;
            double w = theCanvas.ActualWidth;
            mt.Matrix = new Matrix(1, 0, 0, -1, 0, (h>0?h:300));
        }

        private void ThisLayoutWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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

        }

        private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateLayoutText();
        }
    }
}
