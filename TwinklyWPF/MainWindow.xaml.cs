﻿using System;
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

        private void Devices_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            //MainViewModel.twinklyapi.Devices
        }

        private async void GetLayoutTest_Click(object sender, RoutedEventArgs e)
        {
            var layout = await MainViewModel.twinklyapi.GetLayout();
            var content = JsonSerializer.Serialize(layout);
            MessageBox.Show(content);
        }

        private void AddIpAddress_Click(object sender, RoutedEventArgs e)
        {
        }

        private bool m_DevicesTextInput = false;

        private void Devices_TextInput(object sender, TextChangedEventArgs e)
        {
            try
            {
                //var ipAddress = IPAddress.Parse(((ComboBox)sender).Text);
                //MainViewModel.twinklyapi.Devices.Add(new Twinkly_xled.Device(ipAddress));
                m_DevicesTextInput = true;
            }
            catch (Exception err)
            {
                return;
            }
        }

        private void Devices_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (m_DevicesTextInput)
                {
                    var ipAddress = IPAddress.Parse(((ComboBox)sender).Text);
                    MainViewModel.AddDevice(ipAddress);
                }
            }
            catch (Exception err)
            {
            }

            m_DevicesTextInput = false;
        }
    }
}
