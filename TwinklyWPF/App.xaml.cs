using System;
using System.IO;
using System.Windows;

namespace TwinklyWPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public const string UserSettingsFilename = "settings.json";
        public string _DefaultSettingspath = $"{AppContext.BaseDirectory}Settings\\{UserSettingsFilename}";
        public string _UserSettingsPath    = $"{AppContext.BaseDirectory}Settings\\UserSettings\\{UserSettingsFilename}";

        public AppSettings Settings { get; private set; } = new AppSettings();

        // replace base class functions with more specific types

        new public static App Current => (App)Application.Current;

        new public MainWindow MainWindow
        {
            get { return (MainWindow)(base.MainWindow); }
            set { Application.Current.MainWindow = value; }
        }

        MainViewModel MainViewModel;
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            MainViewModel = new MainViewModel(e.Args);

            try
            {
                // if default settings exist
                if (File.Exists(_UserSettingsPath))
                {
                    App.Log($"Reading settings [USER] from {_UserSettingsPath}");
                    this.Settings = AppSettings.Read(_UserSettingsPath);
                }
                else
                {
                    App.Log($"Reading settings [DEFAULT] from {_DefaultSettingspath}");
                    this.Settings = AppSettings.Read(_DefaultSettingspath);
                }
            }
            catch (Exception ex)
            {
                App.Log($"Error: {ex.Message}");
                App.Log($"Error occurred reading settings from {_UserSettingsPath}: using defaults");
            }

            MainWindow = new MainWindow() { DataContext = MainViewModel };
            MainWindow.Show();
        }

        public static void Log(string v)
        {
            Console.WriteLine(v);
            App.Current.MainViewModel.AddMessage(v);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SaveSettings();
            base.OnExit(e);
        }

        public void SaveSettings()
        {
            Settings.Save(_UserSettingsPath);
        }

    }
}
