using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Devices.Enumeration;
using Windows.Devices.Gpio;
using Windows.Devices.Spi;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using ppatierno.AzureSBLite;
using ppatierno.AzureSBLite.Messaging;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Lab03_uwp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // TODO - Replace {iot hub hostname}
        private const string IotHubHostName = "{iot hub hostname}";

        // TODO - Replace {device id}
        private const string DeviceId = "{device id}";

        // TODO - Replace {device key}
        private const string DeviceKey = "{device key}";

        private const int RED_LED_PIN = 4; // GPIO pin G4
        private const int YLW_LED_PIN = 5; // GPIO pin G5
        private const int IotHubSendInterval = 5;

        private DispatcherTimer _dispatcherTimer;
        private SpiDevice _mcp3008;
        private DeviceClient _deviceClient;
        private int _iotHubToggle = -1;
        private double _avgTemperature;

        private GpioController _gpio = null;
        private GpioPin _redLedPin;
        private GpioPin _yellowLedPin;

        public MainPage()
        {
            this.InitializeComponent();

            Initialize();

            _dispatcherTimer = new DispatcherTimer();
            _dispatcherTimer.Tick += DispatcherTimerTick;
            _dispatcherTimer.Interval = TimeSpan.FromSeconds(1);
            _dispatcherTimer.Start();

            var t = Task.Run(() => ReceiveAverageTemperature());
        }

        private void Initialize()
        {
            SetCurrentTime();

            DeviceIdTextBox.Text = DeviceId;

            ThermometerValue.Visibility =
                AverageTempFTextBox.Visibility =
                Visibility.Collapsed;

            InitializeGpio();
            InitializeMcp3008().Wait(TimeSpan.FromSeconds(5));
            InitializeDeviceClient();
        }

        private void InitializeGpio()
        {
            _gpio = GpioController.GetDefault();
            if (_gpio == null) return;

            // Initialize Red Led
            _redLedPin = _gpio.OpenPin(RED_LED_PIN);
            _redLedPin.Write(GpioPinValue.High);
            _redLedPin.SetDriveMode(GpioPinDriveMode.Output);

            // Initialize Yellow Led
            _yellowLedPin = _gpio.OpenPin(YLW_LED_PIN);
            _yellowLedPin.Write(GpioPinValue.High);
            _yellowLedPin.SetDriveMode(GpioPinDriveMode.Output);
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

        private void DispatcherTimerTick(object sender, object e)
        {
            SetCurrentTime();

            var temperatureRecord = ReadTemperature();
            Debug.WriteLine($"The temperature is {temperatureRecord.Celsious} Celsius and {temperatureRecord.Fahrenheit} Farenheit.");

            UpdateTemperatureControls(temperatureRecord);
            UpdateCircuit(temperatureRecord);

            if(TemperatureShouldBeSentToIotHub())
            {
                SendTemperature(temperatureRecord);
            }
        }

        private void SetCurrentTime()
        {
            CurrentTime.Text = DateTime.Now.ToString("h:mm tt");
        }

        private TemperatureRecord ReadTemperature()
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

            return new TemperatureRecord(tempC);
        }
        
        private void UpdateTemperatureControls(TemperatureRecord temperatureRecord)
        {
            ThermometerValue.Visibility = Visibility.Visible;

            CurrentTempFTextBox.Text = string.Format("The current\r\ntemperature\r\nis {0} °F.", Math.Round(temperatureRecord.Fahrenheit,2));

            if (_avgTemperature > 0.0)
            {
                AverageTempFTextBox.Text = string.Format("The average\r\ntemperature\r\nis {0} °F.", Math.Round(_avgTemperature, 2));
                AverageTempFTextBox.Visibility = Visibility.Visible;
            }
            else
            {
                AverageTempFTextBox.Visibility = Visibility.Collapsed;
            }

            UpdateThermometerControls(temperatureRecord);
        }

        private void UpdateThermometerControls(TemperatureRecord temperatureRecord)
        {
            var thermometerHeight = ThermometerScale.Height * (temperatureRecord.Fahrenheit / 100);
            var topMargin = 100.0 + (ThermometerScale.Height - thermometerHeight);

            var margin = ThermometerValue.Margin;
            margin.Top = topMargin;

            ThermometerValue.Margin = margin;
            ThermometerValue.Height = thermometerHeight;
        }

        private void UpdateCircuit(TemperatureRecord temperatureRecord)
        {
            var redValue = (temperatureRecord.Fahrenheit > _avgTemperature) ? GpioPinValue.Low : GpioPinValue.High;
            _redLedPin.Write(redValue);

            var yellowValue = (temperatureRecord.Fahrenheit <= _avgTemperature) ? GpioPinValue.Low : GpioPinValue.High;
            _yellowLedPin.Write(yellowValue);
        }

        private bool TemperatureShouldBeSentToIotHub()
        {
            _iotHubToggle++;

            return ((_iotHubToggle %= IotHubSendInterval) == 0);
        }

        private async void SendTemperature(TemperatureRecord temperatureRecord)
        {
            var dataPoints = new
            {
                deviceId = DeviceId,
                tempC = temperatureRecord.Celsious,
                tempF = temperatureRecord.Fahrenheit
            };

            var messageString = JsonConvert.SerializeObject(dataPoints);
            var message = new Message(Encoding.ASCII.GetBytes(messageString));

            Debug.WriteLine($">> Sending current temperature to Iot Hub... {messageString}");

            await _deviceClient.SendEventAsync(message);
        }

        private async void ReceiveAverageTemperature()
        {
            while (true)
            {
                ServiceBusConnectionStringBuilder builder = new ServiceBusConnectionStringBuilder("Endpoint=sb://kizaniotworkshop.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=QAJrGyephdqNLOXmBOGtNGS07jbd5EJ298N6Ap/Mn0U=");
                builder.TransportType = ppatierno.AzureSBLite.Messaging.TransportType.Amqp;

                MessagingFactory factory = MessagingFactory.CreateFromConnectionString("Endpoint=sb://kizaniotworkshop.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=QAJrGyephdqNLOXmBOGtNGS07jbd5EJ298N6Ap/Mn0U=");

                SubscriptionClient client = factory.CreateSubscriptionClient("avgtempnotification", "iot");

                while (true)
                {
                    try
                    {
                        BrokeredMessage message = client.Receive();
                        if (message != null)
                        {
                            var messageText = Encoding.ASCII.GetString(message.GetBytes());
                            var startingIndex = messageText.IndexOf("?", StringComparison.Ordinal);
                            var messageBody = messageText.Substring(startingIndex + 2);
                            
                            var definition = new { timestamp = DateTime.Now, avgtempc = 0.0, avgtempf = 0.0 };
                            var result = JsonConvert.DeserializeAnonymousType(messageBody, definition);

                            _avgTemperature = result.avgtempf;

                            message.Complete();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }
            }
        }
    }

    internal class TemperatureRecord
    {
        public DateTime Timestamp { get; private set; }
        public double Celsious { get; private set; }
        public double Fahrenheit { get; private set; }

        public TemperatureRecord(double celsius)
        {
            Timestamp = DateTime.UtcNow;
            Celsious = celsius;
            Fahrenheit = (celsius * 9.0 / 5.0) + 32;
        }
    }
}
