using System;
using System.Text;
using System.Threading;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;

namespace Lab03Emulator
{
    class Program
    {
        private static Options Options { get; set; }
        private static readonly DateTime StartTime = DateTime.UtcNow;

        static void Main(string[] args)
        {
            Options = new Options();
            CommandLine.Parser.Default.ParseArguments(args, Options);

            Console.WriteLine("Press CTRL-C to exit emulator.");
            Console.WriteLine();

            while (true)
            {
                var iotHub = Options.GetIotHub();
                var deviceId = Options.GetDeviceId();
                var key = Options.GetKey();

                // Get Temperature Record
                var temperatureRecord = new TemperatureRecord(Options.GetMinTemperature(), Options.GetMaxTemperature());
                var runtime = GetRuntime();
                
                // Build object to send
                var dataPoints = new
                {
                    deviceId,
                    tempC = temperatureRecord.Celsius,
                    tempF = temperatureRecord.Fahrenheit,
                    runtime
                };

                // Create message to send
                var messageString = JsonConvert.SerializeObject(dataPoints);
                var message = new Message(Encoding.ASCII.GetBytes(messageString));

                Console.WriteLine($"{DateTime.Now.ToString("T")} > {messageString}");

                // Initialize device client and send message
                var deviceClient = DeviceClient.Create(
                    iotHub, 
                    AuthenticationMethodFactory.CreateAuthenticationWithRegistrySymmetricKey(deviceId, key));

                deviceClient.SendEventAsync(message);

                deviceClient.Dispose();

                Thread.Sleep(5000);
            }
        }

        private static double GetRuntime()
        {
            var runtime = Options.GetRuntimeHours();
            var duration = DateTime.UtcNow - StartTime;

            return runtime + duration.Hours;
        }
    }
}
