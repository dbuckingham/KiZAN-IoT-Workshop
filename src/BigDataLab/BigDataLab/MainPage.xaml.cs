using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Gpio;
using Windows.Devices.Spi;
using Windows.System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Microsoft.Azure.Devices.Client;
using Microsoft.Devices.Tpm;
using Newtonsoft.Json;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace BigDataLab
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const int RED_LED_PIN = 4; // GPIO pin G4
        private const int YLW_LED_PIN = 5; // GPIO pin G5
        private const int IotHubSendInterval = 30;

        private string _deviceId;

        private DispatcherTimer _dispatcherTimer;
        private SpiDevice _mcp3008;
        private DeviceClient _deviceClient;
        private int _iotHubToggle = -1;

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

        //Alarm
        private bool _isInAlarmState = false;
        private string _alarmDescription = "";
        private DateTime _alarmOnDateTime = DateTime.MinValue;

        public MainPage()
        {
            this.InitializeComponent();

            Initialize();

            _dispatcherTimer = new DispatcherTimer();
            _dispatcherTimer.Tick += DispatcherTimerTick;
            _dispatcherTimer.Interval = TimeSpan.FromSeconds(1);
            _dispatcherTimer.Start();
        }

        private void Initialize()
        {
            SetCurrentTime();

            Application.Current.UnhandledException += Current_UnhandledException;

            ThermometerValue.Visibility =
                Visibility.Collapsed;

            InitializeGpio();
            InitializeActivityGpio();
            InitializeMcp3008().Wait(TimeSpan.FromSeconds(5));

            _deviceClient = InitializeDeviceClient();
            ReceiveCloudToDeviceAsync();

            DeviceIdTextBox.Text = _deviceId;

            //Runtime Initialization
            var r = new Random();
            _baseRuntimeHours = 10000.0 + Math.Round(r.NextDouble() * (1000 - 0) + 0, 2);

        }

        private async void ReceiveCloudToDeviceAsync()
        {
            while (true)
            {
                try
                {
                    Message receivedMessage = await _deviceClient.ReceiveAsync();
                    if (receivedMessage == null) continue;

                    var messageBody = Encoding.ASCII.GetString(receivedMessage.GetBytes());

                    Debug.WriteLine("{0}", messageBody);

                    var definition = new { deviceId = "", alert = 0, description = "" };
                    var result = JsonConvert.DeserializeAnonymousType(messageBody, definition);

                    if (result.alert == 1) _alarmOnDateTime = DateTime.UtcNow;

                    _isInAlarmState = (result.alert == 1);
                    _alarmDescription = result.description;

                    await _deviceClient.CompleteAsync(receivedMessage);
                }
                catch (Exception e)
                {
                    Debug.Write(e);

                    _deviceClient = InitializeDeviceClient();
                }
            }
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

            DeviceClient deviceClient = null;

            try
            {
                deviceClient = DeviceClient.Create(
                    hubUri,
                    AuthenticationMethodFactory.CreateAuthenticationWithToken(deviceId, sasToken));

                return deviceClient;
            }
            catch
            {
                Debug.WriteLine("ERROR!  Unable to create device client!");
            }

            return deviceClient;
        }

        private void DispatcherTimerTick(object sender, object e)
        {
            SetCurrentTime();

            var temperatureRecord = ReadTemperature();
            Debug.WriteLine($"The temperature is {temperatureRecord.Celsius} Celsius and {temperatureRecord.Fahrenheit} Farenheit.");

            UpdateCircuit();
            UpdateTemperatureControls(temperatureRecord);

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

        private void UpdateCircuit()
        {
            var elapsedTime = DateTime.UtcNow - _alarmOnDateTime;

            if (elapsedTime < TimeSpan.FromSeconds(60))
            {
                _redLedPin.Write(GpioPinValue.Low);
            }
            else
            {
                _redLedPin.Write(GpioPinValue.High);
            }

            if (_isInAlarmState)
            {
                ErrorTextBox.Text = "An error was detected with the circuit.";
                ErrorTextBox.Visibility = Visibility.Visible;
                ErrorIcon.Visibility = Visibility.Visible;
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
                    tempC = Math.Round(temperatureRecord.Celsius, 2),
                    tempF = Math.Round(temperatureRecord.Fahrenheit, 2),
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
