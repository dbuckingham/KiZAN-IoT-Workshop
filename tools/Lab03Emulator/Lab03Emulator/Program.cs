using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;

namespace Lab03Emulator
{
    class Program
    {
        static void Main(string[] args)
        {

            Console.WriteLine("Press CTRL-C to exit emulator.");
            Console.WriteLine();

            while (true)
            {
                var options = new Options();

                var iotHub = options.GetIotHub();
                var deviceId = options.GetDeviceId();
                var key = options.GetKey();

                // Get Temperature Record
                //var temperatureRecord = new TemperatureRecord(23.3);
                var temperatureRecord = new TemperatureRecord(options.GetMinTemperature(), options.GetMaxTemperature());

                // Build object to send
                var dataPoints = new
                {
                    deviceId = deviceId,
                    tempC = temperatureRecord.Celsius,
                    tempF = temperatureRecord.Fahrenheit
                };

                // Create message to send
                var messageString = JsonConvert.SerializeObject(dataPoints);
                var message = new Message(Encoding.ASCII.GetBytes(messageString));

                Console.WriteLine($"{messageString}");

                // Initialize device client and send message
                var deviceClient = DeviceClient.Create(
                    iotHub, 
                    AuthenticationMethodFactory.CreateAuthenticationWithRegistrySymmetricKey(deviceId, key));

                deviceClient.SendEventAsync(message);

                deviceClient.Dispose();

                Thread.Sleep(5000);
            }
        }
    }
}
