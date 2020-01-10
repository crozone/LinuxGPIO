using crozone.LinuxGpio;
using System;
using System.Text.RegularExpressions;
namespace LinuxGpioCli
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("LinuxGpio CLI tool");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("init <gpio number> <linux|dummy>");
            Console.WriteLine("*get export");
            Console.WriteLine("*set export <on|off>");
            Console.WriteLine("*get unexport");
            Console.WriteLine("*set unexport <on|off>");
            Console.WriteLine("open");
            Console.WriteLine("get value");
            Console.WriteLine("set value <on|off>");
            Console.WriteLine("get direction");
            Console.WriteLine("set direction <input|output>");
            Console.WriteLine("get activelow");
            Console.WriteLine("set activelow <on|off>");
            Console.WriteLine("get events");
            Console.WriteLine("set events <on|off>");
            Console.WriteLine("deinit");
            Console.WriteLine("exit");
            Console.WriteLine();
            Console.WriteLine("* linux only");

            IGpioPin currentPin = null;

            bool exit = false;
            while (!exit)
            {
                string command = Console.ReadLine();

                var match = Regex.Match(command, "(\\w+)\\s*(\\w*)\\s*(\\w*)\\s*");

                if (match.Success)
                {
                    string first = match.Groups[1]?.Value.ToLower();
                    string second = match.Groups[2]?.Value.ToLower();
                    string third = match.Groups[3]?.Value.ToLower();

                    switch (first)
                    {
                        case "init":
                            if (int.TryParse(second, out int gpioNumber))
                            {
                                switch (third)
                                {
                                    case "linux":
                                        if (currentPin == null)
                                        {
                                            currentPin = new LinuxGpioPin(gpioNumber, $"{third}-{gpioNumber}");
                                            currentPin.ValueChanged += CurrentPin_ValueChanged;
                                            Console.WriteLine($"init gpio {currentPin.Name}");
                                        }
                                        else
                                        {
                                            Console.WriteLine($"init error: pin already initialized");
                                        }
                                        break;
                                    case "dummy":
                                    case null:
                                        if (currentPin == null)
                                        {
                                            currentPin = new DummyGpioPin(gpioNumber, $"{third}-{gpioNumber}");
                                            currentPin.ValueChanged += CurrentPin_ValueChanged;
                                            Console.WriteLine($"init gpio {currentPin.Name}");
                                        }
                                        else
                                        {
                                            Console.WriteLine($"init error: pin already initialized");
                                        }
                                        break;
                                    default:
                                        Console.WriteLine($"init error: unknown gpio type {third}");
                                        break;
                                }
                            }
                            else
                            {
                                Console.WriteLine($"init error: invalid gpio number {second}");
                                break;
                            }
                            break;
                        case "open":
                            if (currentPin != null)
                            {
                                try
                                {
                                    currentPin.Open();
                                    Console.WriteLine($"open gpio {currentPin.Name}");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"open error: {ex.Message}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"open error: pin not initialized");
                            }
                            break;
                        case "deinit":
                            if (currentPin != null)
                            {
                                currentPin.ValueChanged -= CurrentPin_ValueChanged;
                                currentPin.Dispose();
                                Console.WriteLine($"deinit gpio {currentPin.Name}");
                            }
                            else
                            {
                                Console.WriteLine($"open error: pin not initialized");
                            }
                            break;
                        case "exit":
                            exit = true;
                            Console.WriteLine("exit app");
                            break;
                        case "get":
                            if (currentPin != null)
                            {
                                switch (second)
                                {
                                    case "value":
                                        Console.WriteLine($"get value {currentPin.Name}: {(currentPin.Value ? "on" : "off")}");
                                        break;
                                    case "direction":
                                        Console.WriteLine($"get direction {currentPin.Name}: {(currentPin.Direction == GpioDirection.Input ? "input" : "output")}");
                                        break;
                                    case "activelow":
                                        Console.WriteLine($"get activelow {currentPin.Name}: {(currentPin.ActiveLow ? "on" : "off")}");
                                        break;
                                    case "events":
                                        Console.WriteLine($"get events {currentPin.Name}: {(currentPin.EnableRaisingEvents ? "on" : "off")}");
                                        break;
                                }
                            }
                            else
                            {
                                Console.WriteLine($"init error: pin not initialized");
                            }
                            break;
                        case "set":
                            if (currentPin != null)
                            {
                                switch (second)
                                {
                                    case "value":
                                        if (currentPin is IOutputPin outputPin)
                                        {
                                            switch (third)
                                            {
                                                case "off":
                                                    outputPin.Value = false;
                                                    Console.WriteLine($"set value {outputPin.Name} off");

                                                    break;
                                                case "on":
                                                case null:
                                                    outputPin.Value = true;
                                                    Console.WriteLine($"set value {outputPin.Name} on");
                                                    break;
                                                default:
                                                    Console.WriteLine($"set value error: invalid option {third}");
                                                    break;
                                            }
                                            break;
                                        }
                                        else
                                        {
                                            Console.WriteLine($"set value error: pin is not an output");
                                        }
                                        break;
                                    case "direction":
                                        switch (third)
                                        {
                                            case "input":
                                                currentPin.Direction = GpioDirection.Input;
                                                Console.WriteLine($"set direction {currentPin.Name} input");
                                                break;
                                            case "output":
                                                currentPin.Direction = GpioDirection.Input;
                                                Console.WriteLine($"set direction {currentPin.Name} output");
                                                break;
                                            default:
                                                Console.WriteLine($"set direction error: invalid option {third}");
                                                break;
                                        }
                                        break;
                                    case "activelow":
                                        switch (third)
                                        {
                                            case "off":
                                                currentPin.ActiveLow = false;
                                                Console.WriteLine($"set activelow {currentPin.Name} off");
                                                break;
                                            case "on":
                                                currentPin.ActiveLow = true;
                                                Console.WriteLine($"set activelow {currentPin.Name} on");
                                                break;
                                            default:
                                                Console.WriteLine($"set activelow error: invalid option {third}");
                                                break;
                                        }
                                        break;
                                    case "events":
                                        switch (third)
                                        {
                                            case "off":
                                                currentPin.EnableRaisingEvents = false;
                                                Console.WriteLine($"set events {currentPin.Name} off");
                                                break;
                                            case "on":
                                                currentPin.EnableRaisingEvents = true;
                                                Console.WriteLine($"set events {currentPin.Name} on");
                                                break;
                                            default:
                                                Console.WriteLine($"set events error: invalid option {third}");
                                                break;
                                        }
                                        break;
                                    default:
                                        Console.WriteLine($"set error: invalid option {second}");
                                        break;
                                }
                            }
                            else
                            {
                                Console.WriteLine($"init error: pin not initialized");
                            }
                            break;
                        default:
                            Console.WriteLine($"error: unknown command {first}");
                            break;
                    }
                }
            }
        }

        private static void CurrentPin_ValueChanged(object sender, PinValueChangedEventArgs e)
        {
            Console.WriteLine($"Value changed event for {e.Pin.Name}: {e.Value}");
        }
    }
}
