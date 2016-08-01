using System;
using System.Configuration;
using CommandLine;

namespace Lab03Emulator
{
    internal class Options
    {
        private readonly double _baseRuntimeHours;

        [Option('h', "hostName", HelpText = "Hostname for the IoT Hub.")]
        public string IotHub { get; set; }

        [Option('d', "deviceId", HelpText = "The device id to emulate.")]
        public string DeviceId { get; set; }

        [Option('k', "key", HelpText = "Shared access key for the device.")]
        public string Key { get; set; }

        [Option("minTemp", DefaultValue = 70.0, HelpText = "The minimum temperature in degrees Farenheit.")]
        public double MinTemp { get; set; }

        [Option("maxTemp", DefaultValue = 80.0, HelpText = "The maximum temperature in degrees Farenheit.")]
        public double MaxTemp { get; set; }

        [Option("MinRuntimeHours", DefaultValue = 10000.0, HelpText="The minimum value for the number of runtime hours.")]
        public double MinRuntimeHours { get; set; }

        public Options()
        {
            var r = new Random();
            _baseRuntimeHours = Math.Round(r.NextDouble() * (1000 - 0) + 0, 2);
        }

        public string GetIotHub()
        {
            var iotHub = IotHub ?? ConfigurationManager.AppSettings["iotHub"];

            if (string.IsNullOrWhiteSpace(iotHub)) throw new Exception("IoT Hub is not found.  Please specify an IoT Hub in the configuration or as a command line parameter.");

            return iotHub;
        }

        public string GetDeviceId()
        {
            var deviceId = DeviceId ?? ConfigurationManager.AppSettings["deviceId"];

            if (string.IsNullOrWhiteSpace(deviceId)) throw new Exception("Device Id is not found.  Please specify a Device Id in the configuration or as a command line parameter.");

            return deviceId;
        }

        public string GetKey()
        {
            var key = Key ?? ConfigurationManager.AppSettings["key"];

            if (string.IsNullOrWhiteSpace(key)) throw new Exception("Key is not found.  Please specify a Key in the configuration or as a command line parameter.");

            return key;
        }

        public double GetMinTemperature()
        {
            var minTemp = MinTemp;

            if (minTemp <= 0.0 && !double.TryParse(ConfigurationManager.AppSettings["minTemp"], out minTemp))
            {
                return 70.0;
            }

            return minTemp;
        }

        public double GetMaxTemperature()
        {
            var maxTemp = MaxTemp;

            if (maxTemp <= 0.0 && !double.TryParse(ConfigurationManager.AppSettings["maxTemp"], out maxTemp))
            {
                return 80.0;
            }

            return maxTemp;
        }

        public double GetRuntimeHours()
        {
            var minRunTimeHours = MinRuntimeHours;

            if (minRunTimeHours <= 0.0 && !double.TryParse(ConfigurationManager.AppSettings["minRunTimeHours"], out minRunTimeHours))
            {
                return 10000.0 + _baseRuntimeHours;
            }

            return minRunTimeHours + _baseRuntimeHours;
        }
    }
}