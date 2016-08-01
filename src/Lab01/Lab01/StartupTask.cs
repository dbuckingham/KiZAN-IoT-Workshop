using System;
using Windows.ApplicationModel.Background;
using Windows.Devices.Gpio;
using Windows.System.Threading;

namespace Lab01
{
    public sealed class StartupTask : IBackgroundTask
    {
        private BackgroundTaskDeferral _deferral;

        private GpioController _gpio = null;

        private const int RED_LED_PIN = 4; // GPIO pin G4
        private GpioPin _redLedPin;
        private GpioPinValue _redLedValue = GpioPinValue.Low;

        private ThreadPoolTimer _timer;

        //Activity Pin
        private const int ACT_LED_PIN = 47;
        private GpioPin _actLedPin;
        private GpioPinValue _actLedValue = GpioPinValue.High;
        private ThreadPoolTimer _activityTimer;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();

            InitializeGpio();
            InitializeActivityGpio();

            _timer = ThreadPoolTimer.CreatePeriodicTimer(Timer_Tick, TimeSpan.FromMilliseconds(500));
        }

        private void InitializeGpio()
        {
            _gpio = GpioController.GetDefault();
            if (_gpio == null) return;

            _redLedPin = _gpio.OpenPin(RED_LED_PIN);
            _redLedPin.Write(GpioPinValue.Low);
            _redLedPin.SetDriveMode(GpioPinDriveMode.Output);
        }

        private void InitializeActivityGpio()
        {
            _actLedPin = _gpio.OpenPin(ACT_LED_PIN);
            _actLedPin.Write(GpioPinValue.High);
            _actLedPin.SetDriveMode(GpioPinDriveMode.Output);

            _activityTimer = ThreadPoolTimer.CreatePeriodicTimer(ActivityTimer_Tick, TimeSpan.FromMilliseconds(1000));
        }

        private void Timer_Tick(ThreadPoolTimer timer)
        {
            _redLedValue = (_redLedValue == GpioPinValue.High) ? GpioPinValue.Low : GpioPinValue.High;

            _redLedPin.Write(_redLedValue);
        }
        private void ActivityTimer_Tick(ThreadPoolTimer timer)
        {
            _actLedValue = (_actLedValue == GpioPinValue.High) ? GpioPinValue.Low : GpioPinValue.High;

            _actLedPin.Write(_actLedValue);
        }
    }
}