using System;

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

    public TemperatureRecord(double min, double max)
    {
        Random r = new Random();

        Timestamp = DateTime.UtcNow;
        Fahrenheit = Math.Round(r.NextDouble()*(max - min) + min, 2);
        Celsius = Math.Round((Fahrenheit - 32) * (5.0 / 9.0), 2);
    }
}
