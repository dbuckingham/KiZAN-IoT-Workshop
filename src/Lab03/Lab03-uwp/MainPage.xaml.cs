using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Devices.Enumeration;
using Windows.Devices.Spi;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.PlayTo;
using Windows.System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Lab03_uwp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // TODO - Replace {iot hub hostname}
        private const string IotHubHostName = "KiZANIoTHub.azure-devices.net";

        // TODO - Replace {device id}
        private const string DeviceId = "kizan-pi-01";

        // TODO - Replace {device key}
        private const string DeviceKey = "Ix39iVFvSnhbE/uqlsEFU+E+ePxSF3rJtgKF19uBDcU=";

        private DispatcherTimer _dispatcherTimer;
        private SpiDevice _mcp3008;
        private DeviceClient _deviceClient;

        public MainPage()
        {
            this.InitializeComponent();

            Initialize();

            _dispatcherTimer = new DispatcherTimer();
            _dispatcherTimer.Tick += DispatcherTimerTick;
            _dispatcherTimer.Interval = TimeSpan.FromSeconds(5);
            _dispatcherTimer.Start();
        }

        private void DispatcherTimerTick(object sender, object e)
        {
            FindAndSetTemperature();
        }

        private void FindAndSetTemperature()
        {
            var tempC = ReadTemperature();
            var tempF = (tempC * 9.0 / 5.0) + 32;

            SetCurrentTempTextBox(tempF);

            Debug.WriteLine($"The temperature is {tempC} Celsius and {tempF} Farenheit");

            //SendTemperature(tempC, tempF);
        }

        private void SetCurrentTempTextBox(double tempF)
        {
            CurrentTempFTextBox.Text = string.Format("The current\r\ntemperature\r\nis {0} °F.", Math.Round(tempF,2));

            var thermometerHeight = ThermometerHeight.Height * (tempF / 100);
            var topMargin = 100.0 + (ThermometerHeight.Height - thermometerHeight);

            var margin = Thermometer.Margin;
            margin.Top = topMargin;

            Thermometer.Margin = margin;
            Thermometer.Height = thermometerHeight;
        }

        private double ReadTemperature()
        {
            //From data sheet -- 1 byte selector for channel 0 on the ADC
            // First Byte sends the Start bit for SPI
            // Second Byte is the Configuration Byte
            //1 - single ended (this is where the 8 below is added)
            //0 - d2
            //0 - d1
            //0 - d0
            //             S321XXXX <-- single-ended channel selection configure bits
            // Channel 0 = 10000000 = 0x80 OR (8+channel) << 4
            // Third Byte is empty
            var transmitBuffer = new byte[3] { 1, 0x80, 0x00 };
            var receiveBuffer = new byte[3];

            _mcp3008.TransferFullDuplex(transmitBuffer, receiveBuffer);

            //first byte returned is 0 (00000000), 
            //second byte returned we are only interested in the last 2 bits 00000011 ( &3) 
            //shift 8 bits to make room for the data from the 3rd byte (makes 10 bits total)
            //third byte, need all bits, simply add it to the above result 
            var result = ((receiveBuffer[1] & 3) << 8) + receiveBuffer[2];

            //LM36 == 10mV/1degC ... 3.3V = 3300.0, 10 bit chip # steps is 2 exp 10 == 1024
            var mv = result * (3300.0 / 1024.0);
            var tempC = (mv - 500.0) / 10.0;

            return tempC;
        }

        private void Initialize()
        {
            InitializeMcp3008().Wait(TimeSpan.FromSeconds(5));
            InitializeDeviceClient();
        }

        private async Task InitializeMcp3008()
        {
            var spiSettings = new SpiConnectionSettings(0);
            spiSettings.ClockFrequency = 3600000;
            spiSettings.Mode = SpiMode.Mode0;

            string spiQuery = SpiDevice.GetDeviceSelector("SPI0");
            var deviceInfo = await DeviceInformation.FindAllAsync(spiQuery);
            if (deviceInfo != null && deviceInfo.Count > 0)
            {
                _mcp3008 = await SpiDevice.FromIdAsync(deviceInfo[0].Id, spiSettings);
            }
        }

        private void InitializeDeviceClient()
        {
            _deviceClient = DeviceClient.Create(IotHubHostName,
                new DeviceAuthenticationWithRegistrySymmetricKey(DeviceId, DeviceKey));
        }

        private async void SendTemperature(double tempC, double tempF)
        {
            var dataPoints = new
            {
                deviceId = DeviceId,
                tempC = tempC,
                tempF = tempF
            };

            var messageString = JsonConvert.SerializeObject(dataPoints);
            var message = new Message(Encoding.ASCII.GetBytes(messageString));

            await _deviceClient.SendEventAsync(message);
        }
    }
}
