using System;
using Windows.ApplicationModel.Background;
using Windows.Devices.Gpio;
using Windows.System.Threading;

namespace Lab03
{
    public sealed class StartupTask : IBackgroundTask
    {
        private BackgroundTaskDeferral _deferral;

        private GpioController _gpio = null;

        private const int RED_LED_PIN = 4; // GPIO pin G4
        private const int YLW_LED_PIN = 5; // GPIO pin G5
        private const int YLW_BTN_PIN = 6; // GPIO pin G6
        
        private GpioPin _redLedPin;
        private GpioPin _yellowLedPin;
        private GpioPin _yellowButtonPin;

        private GpioPinValue _redLedValue = GpioPinValue.High;
        private GpioPinValue _yellowLedValue = GpioPinValue.High;

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

            // Initialize Red Led
            _redLedPin = _gpio.OpenPin(RED_LED_PIN);
            _redLedPin.Write(GpioPinValue.High);
            _redLedPin.SetDriveMode(GpioPinDriveMode.Output);

            // Initialize Yellow Led
            _yellowLedPin = _gpio.OpenPin(YLW_LED_PIN);
            _yellowLedPin.Write(GpioPinValue.High);
            _yellowLedPin.SetDriveMode(GpioPinDriveMode.Output);

            // Initialize Yellow Button
            _yellowButtonPin = _gpio.OpenPin(YLW_BTN_PIN);

            if(_yellowButtonPin.IsDriveModeSupported(GpioPinDriveMode.InputPullUp))
                _yellowButtonPin.SetDriveMode(GpioPinDriveMode.InputPullUp);
            else
                _yellowButtonPin.SetDriveMode(GpioPinDriveMode.Input);

            _yellowButtonPin.DebounceTimeout = TimeSpan.FromMilliseconds(50);
            _yellowButtonPin.ValueChanged += _yellowButtonPin_ValueChanged;
        }

        private void _yellowButtonPin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            if (args.Edge == GpioPinEdge.FallingEdge)
            {
                _yellowLedValue = (_yellowLedValue == GpioPinValue.Low) ?
                    GpioPinValue.High : GpioPinValue.Low;
                _yellowLedPin.Write(_yellowLedValue);
            }
        }

        private void Timer_Tick(ThreadPoolTimer timer)
        {
            _redLedValue = (_redLedValue == GpioPinValue.High) ? GpioPinValue.Low : GpioPinValue.High;
            _redLedPin.Write(_redLedValue);
        }
    }
}