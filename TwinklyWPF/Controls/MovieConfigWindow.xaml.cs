using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using Twinkly_xled.JSONModels;

namespace TwinklyWPF
{
    /// <summary>
    /// Interaction logic for MovieConfigWindow.xaml
    /// </summary>
    public partial class MovieConfigWindow : Window, INotifyPropertyChanged
    {
        public MainViewModel MainViewModel => (MainViewModel)DataContext;

        private CurrentMovieConfig _movieConfig;

        public CurrentMovieConfig MovieConfig
        {
            get { return _movieConfig; }
            set { _movieConfig = value; OnPropertyChanged(); }
        }

        private string _movieConfigText;

        public string MovieConfigText
        {
            get { return _movieConfigText; }
            private set { _movieConfigText = value; OnPropertyChanged(); }
        }

        private string _filename;


        public MovieConfigWindow()
        {
            InitializeComponent();

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            OnPropertyChanged("MovieConfig");
            UpdateMovieConfigText();
        }


        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion

        private async void GetButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentMovieConfig config = await MainViewModel.ActiveDevice?.twinklyapi.GetMovieConfig();
            //if (config.IsOK)
                MovieConfig = config;

            UpdateMovieConfigText();
        }

        private async void SetButton_Click(object sender, RoutedEventArgs e)
        {
            VerifyResult result = await MainViewModel.ActiveDevice?.twinklyapi.SetMovieConfig(MovieConfig);
            if (result.code != 200)
                MessageBox.Show($"Result: {result.code}");
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
                    MovieConfig = JsonSerializer.Deserialize<CurrentMovieConfig>(str);
                    UpdateMovieConfigText();
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
                    JsonSerializer.Serialize(sw, MovieConfig);
                }
            }
        }

        private void UpdateMovieConfigText()
        {
            if (MovieConfig == null)
            {
                MovieConfigText = "empty";
                return;
            }

            MovieConfigText = JsonSerializer.Serialize(
                MovieConfig,
                new JsonSerializerOptions(JsonSerializerDefaults.General) { WriteIndented = true });

        }
    }
}
