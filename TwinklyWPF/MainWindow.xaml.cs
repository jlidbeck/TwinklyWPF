using System.ComponentModel;
using System.Windows;

namespace TwinklyWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public new MainViewModel DataContext
        {
            get { return (MainViewModel)base.DataContext; }
            set { base.DataContext = value; }
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext != null)
            {
                DataContext.Load();
                DataContext.GradientStops = SingleGradient.GradientStops.Clone();
            }
        }

        private void RealtimeTest_Click(object sender, RoutedEventArgs e)
        {
            var random = new System.Random();
            var frameData = new byte[60];
            random.NextBytes(frameData);
            //for (int i = 0; i < frameData.Length; ++i)
            //{
            //    frameData[i] = random.NextBytes(frameData);
            //}

            DataContext.SetFrameAsync(frameData);
        }
    }
}
