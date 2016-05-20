using System;
using System.Diagnostics;
using Windows.ApplicationModel.Background;
using Windows.Devices.Enumeration;
using Windows.Devices.Spi;
using Windows.System.Threading;

namespace Lab04
{
    public sealed class StartupTask : IBackgroundTask
    {
        private BackgroundTaskDeferral _deferral;
        private ThreadPoolTimer _timer;
        private SpiDevice _mcp3008;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();

            InitializeMcp3008();

            _timer = ThreadPoolTimer.CreatePeriodicTimer(Timer_Tick, TimeSpan.FromSeconds(5));
        }

        private void Timer_Tick(ThreadPoolTimer timer)
        {
            var tempC = ReadTemperature();
            var tempF = (tempC * 9.0 / 5.0) + 32;

            Debug.WriteLine($"The temperature is {tempC} Celsius and {tempF} Farenheit");
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

        private async void InitializeMcp3008()
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
    }
}