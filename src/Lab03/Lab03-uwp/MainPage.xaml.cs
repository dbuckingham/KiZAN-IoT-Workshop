using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Gpio;
using Windows.Devices.Spi;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Microsoft.Azure.Devices.Client;
using Microsoft.Devices.Tpm;
using Newtonsoft.Json;
using ppatierno.AzureSBLite;
using ppatierno.AzureSBLite.Messaging;
using Windows.ApplicationModel.Background;
using Windows.System.Threading;
using TransportType = Microsoft.Azure.Devices.Client.TransportType;
using System.Runtime.InteropServices;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Lab03_uwp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const int RED_LED_PIN = 4; // GPIO pin G4
        private const int YLW_LED_PIN = 5; // GPIO pin G5
        private const int IotHubSendInterval = 5;

        private string _deviceId;

        private DispatcherTimer _dispatcherTimer;
        private SpiDevice _mcp3008;
        private DeviceClient _deviceClient;
        private int _iotHubToggle = -1;
        private double _avgTemperature;

        private GpioController _gpio = null;
        private GpioPin _redLedPin;
        private GpioPin _yellowLedPin;

        //Activity Pin
        private const int ACT_LED_PIN = 47;
        private GpioPin _actLedPin;
        private GpioPinValue _actLedValue = GpioPinValue.High;
        private ThreadPoolTimer _activityTimer;

        //Runtime
        private static readonly DateTime StartTime = DateTime.UtcNow;
        private double _baseRuntimeHours; 

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

            Application.Current.UnhandledException += Current_UnhandledException;

            ThermometerValue.Visibility =
                AverageTempFTextBox.Visibility =
                Visibility.Collapsed;

            InitializeGpio();
            InitializeActivityGpio();
            InitializeMcp3008().Wait(TimeSpan.FromSeconds(5));
            InitializeDeviceClient();

            DeviceIdTextBox.Text = _deviceId;

            //Runtime Initialization
            var r = new Random();
            _baseRuntimeHours = 10000.0 + Math.Round(r.NextDouble() * (1000 - 0) + 0, 2);

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

        private void InitializeActivityGpio()
        {
            _actLedPin = _gpio.OpenPin(ACT_LED_PIN);
            _actLedPin.Write(GpioPinValue.High);
            _actLedPin.SetDriveMode(GpioPinDriveMode.Output);

            _activityTimer = ThreadPoolTimer.CreatePeriodicTimer(ActivityTimer_Tick, TimeSpan.FromMilliseconds(1000));
        }

        private void ActivityTimer_Tick(ThreadPoolTimer timer)
        {
            _actLedValue = (_actLedValue == GpioPinValue.High) ? GpioPinValue.Low : GpioPinValue.High;

            _actLedPin.Write(_actLedValue);
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

        private DeviceClient InitializeDeviceClient()
        {
            TpmDevice device = new TpmDevice(0);
            string hubUri = device.GetHostName();
            string deviceId = device.GetDeviceId();
            string sasToken = device.GetSASToken();

            _deviceId = deviceId;

            return DeviceClient.Create(
                hubUri,
                AuthenticationMethodFactory.CreateAuthenticationWithToken(deviceId, sasToken));
        }

        private void DispatcherTimerTick(object sender, object e)
        {
            SetCurrentTime();

            var temperatureRecord = ReadTemperature();
            Debug.WriteLine($"The temperature is {temperatureRecord.Celsius} Celsius and {temperatureRecord.Fahrenheit} Farenheit.");

            UpdateTemperatureControls(temperatureRecord);
            UpdateCircuit(temperatureRecord);

            if (TemperatureShouldBeSentToIotHub())
            {
                try
                {
                    SendTemperature(temperatureRecord);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
        }

        private void SetCurrentTime()
        {
            CurrentTime.Text = DateTime.Now.ToString("h:mm tt");
        }

        private TemperatureRecord ReadTemperature()
        {
            double tempC = 0.0;

            try
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
                tempC = (mv - 500.0) / 10.0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                tempC = 0.0;
            }

            return new TemperatureRecord(tempC);
        }

        private void UpdateTemperatureControls(TemperatureRecord temperatureRecord)
        {
            try
            {
                ThermometerValue.Visibility = Visibility.Visible;

                if (temperatureRecord.Fahrenheit < 0)
                {
                    ErrorTextBox.Text = string.Format("An error was detected with the circuit.");
                    ErrorTextBox.Visibility = Visibility.Visible;
                    ErrorIcon.Visibility = Visibility.Visible;
                }
                else
                {
                    ErrorTextBox.Visibility = Visibility.Collapsed;
                    ErrorIcon.Visibility = Visibility.Collapsed;
                }
                
                CurrentTempFTextBox.Text = string.Format("The current\r\ntemperature\r\nis {0} °F.", Math.Round(temperatureRecord.Fahrenheit, 2));
                

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
            catch
            {
                // ignored
            }
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
            try
            {
                var runtime = GetRuntime();
                var dataPoints = new
                {
                    deviceId = _deviceId,
                    tempC = temperatureRecord.Celsius,
                    tempF = temperatureRecord.Fahrenheit,
                    runtime
                };

                var messageString = JsonConvert.SerializeObject(dataPoints);
                var message = new Message(Encoding.ASCII.GetBytes(messageString));

                Debug.WriteLine($">> Sending current temperature to Iot Hub... {messageString}");

                var deviceClient = InitializeDeviceClient();

                await deviceClient.SendEventAsync(message);

                deviceClient.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("FAILED to send temperature to Iot Hub.");
                Debug.WriteLine(ex);
            }
        }

        private async void ReceiveAverageTemperature()
        {
            try
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
                                Debug.WriteLine($"<< Received Average Temperature from Cloud (in degrees F)... {_avgTemperature}");

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
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void Current_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Debug.WriteLine($"Unhandled Exception: {e.Message}");
            e.Handled = true;
        }

        private double GetRuntime()
        {
            var runtime = _baseRuntimeHours;
            var duration = DateTime.UtcNow - StartTime;

            return runtime + duration.Hours;
        }
    }

    internal class TemperatureRecord
    {
        public DateTime Timestamp { get; private set; }
        public double Celsius { get; private set; }
        public double Fahrenheit { get; private set; }

        public TemperatureRecord(double celsius)
        {
            Timestamp = DateTime.UtcNow;
            Celsius = celsius;
            Fahrenheit = (celsius * 9.0 / 5.0) + 32;
        }
    }


}
