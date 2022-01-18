using System;
using System.ComponentModel;
using System.Net;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace TwinklyWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MainViewModel MainViewModel => (MainViewModel)DataContext;

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

        private void RealtimeTest_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel.RealtimeTest_Click(sender);
        }

        private async void GetLayoutTest_Click(object sender, RoutedEventArgs e)
        {
            var layout = await MainViewModel.twinklyapi.GetLayout();
            var content = JsonSerializer.Serialize(layout);
            MessageBox.Show(content);
        }

    }
}
