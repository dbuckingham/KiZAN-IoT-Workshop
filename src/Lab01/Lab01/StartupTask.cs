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

        //
        private ThreadPoolTimer _timer;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();

            InitializeGpio();

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

        private void Timer_Tick(ThreadPoolTimer timer)
        {
            _redLedValue = (_redLedValue == GpioPinValue.High) ? GpioPinValue.Low : GpioPinValue.High;

            _redLedPin.Write(_redLedValue);
        }
    }
}