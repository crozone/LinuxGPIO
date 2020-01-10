using crozone.LinuxGpio;
using System;
using System.Collections.Generic;
using System.Linq;
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
            Console.WriteLine("*get <gpio number> export");
            Console.WriteLine("*set <gpio number> export <on|off>");
            Console.WriteLine("*get <gpio number> unexport");
            Console.WriteLine("*set <gpio number> unexport <on|off>");
            Console.WriteLine("open <gpio number>");
            Console.WriteLine("get <gpio number> value");
            Console.WriteLine("set <gpio number> value <on|off>");
            Console.WriteLine("get <gpio number> direction");
            Console.WriteLine("set <gpio number> direction <input|output>");
            Console.WriteLine("get <gpio number> activelow");
            Console.WriteLine("set <gpio number> activelow <on|off>");
            Console.WriteLine("get <gpio number> events");
            Console.WriteLine("set <gpio number> events <on|off>");
            Console.WriteLine("deinit <gpio number>");
            Console.WriteLine("exit");
            Console.WriteLine();
            Console.WriteLine("* linux only");

            List<IGpioPin> currentPins = new List<IGpioPin>();

            while (true)
            {
                string command = Console.ReadLine();

                var match = Regex.Match(command, "(\\w+)\\s*(\\w*)\\s*(\\w*)\\s*(\\w*)\\s*");

                if (match.Success)
                {
                    string first = match.Groups[1]?.Value.ToLower();
                    string second = match.Groups[2]?.Value.ToLower();
                    string third = match.Groups[3]?.Value.ToLower();
                    string fourth = match.Groups[4]?.Value.ToLower();

                    if (first == "exit")
                    {
                        Console.WriteLine("exit app");
                        break;
                    }

                    IGpioPin gpioPin;
                    if (int.TryParse(second, out int gpioNumber))
                    {
                        gpioPin = currentPins.FirstOrDefault(p => p.Pin == gpioNumber);
                    }
                    else
                    {
                        Console.WriteLine($"command error: {second} is not a valid gpio pin number");
                        continue;
                    }

                    switch (first)
                    {
                        case "init":
                            switch (third)
                            {
                                case "linux":
                                    if (gpioPin == null)
                                    {
                                        gpioPin = new LinuxGpioPin(gpioNumber, $"linux-{gpioNumber}");
                                        gpioPin.ValueChanged += CurrentPin_ValueChanged;
                                        Console.WriteLine($"init gpio {gpioPin.Name}");
                                        currentPins.Add(gpioPin);
                                    }
                                    else
                                    {
                                        Console.WriteLine($"init error: pin {gpioPin.Name} already initialized");
                                    }
                                    break;
                                case "dummy":
                                case "":
                                case null:
                                    if (gpioPin == null)
                                    {
                                        gpioPin = new DummyGpioPin(gpioNumber, $"dummy-{gpioNumber}");
                                        gpioPin.ValueChanged += CurrentPin_ValueChanged;
                                        Console.WriteLine($"init gpio {gpioPin.Name}");
                                        currentPins.Add(gpioPin);
                                    }
                                    else
                                    {
                                        Console.WriteLine($"init error: gpio {gpioPin.Name} already initialized");
                                    }
                                    break;
                                default:
                                    Console.WriteLine($"init error: unknown gpio type {third}");
                                    break;
                            }
                            break;
                        case "open":
                            if (gpioPin != null)
                            {
                                try
                                {
                                    gpioPin.Open();
                                    Console.WriteLine($"open gpio {gpioPin.Name}");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"open error: {ex.Message}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"open error: gpio pin {gpioNumber} not initialized");
                            }
                            break;
                        case "deinit":
                            if (gpioPin != null)
                            {
                                gpioPin.ValueChanged -= CurrentPin_ValueChanged;
                                Console.WriteLine($"deinit gpio {gpioPin.Name}");
                                gpioPin.Dispose();
                                currentPins.Remove(gpioPin);
                            }
                            else
                            {
                                Console.WriteLine($"open error: gpio pin {gpioNumber} not initialized");
                            }
                            break;
                        case "get":
                            if (gpioPin != null)
                            {
                                switch (third)
                                {
                                    case "value":
                                        Console.WriteLine($"get value {gpioPin.Name}: {(gpioPin.Value ? "on" : "off")}");
                                        break;
                                    case "direction":
                                        Console.WriteLine($"get direction {gpioPin.Name}: {(gpioPin.Direction == GpioDirection.Input ? "input" : "output")}");
                                        break;
                                    case "activelow":
                                        Console.WriteLine($"get activelow {gpioPin.Name}: {(gpioPin.ActiveLow ? "on" : "off")}");
                                        break;
                                    case "events":
                                        Console.WriteLine($"get events {gpioPin.Name}: {(gpioPin.EnableRaisingEvents ? "on" : "off")}");
                                        break;
                                    case "export":
                                        if (gpioPin is LinuxGpioPin linuxGpioPin)
                                        {
                                            Console.WriteLine($"get export {linuxGpioPin.Name}: {(linuxGpioPin.Export ? "on" : "off")}");
                                        }
                                        else
                                        {
                                            Console.WriteLine($"get export error: gpio pin is not compatible with export");
                                        }
                                        break;
                                    case "unexport":
                                        if (gpioPin is LinuxGpioPin linuxGpioPin2)
                                        {
                                            Console.WriteLine($"get unexport {linuxGpioPin2.Name}: {(linuxGpioPin2.Export ? "on" : "off")}");
                                        }
                                        else
                                        {
                                            Console.WriteLine($"get export error: gpio pin is not compatible with unexport");
                                        }
                                        break;
                                    default:
                                        Console.WriteLine($"get error: invalid option {third}");
                                        break;
                                }
                            }
                            else
                            {
                                Console.WriteLine($"init error: gpio pin {gpioNumber} not initialized");
                            }
                            break;
                        case "set":
                            if (gpioPin != null)
                            {
                                switch (third)
                                {
                                    case "value":
                                        switch (fourth)
                                        {
                                            case "off":
                                                try
                                                {
                                                    gpioPin.Value = false;
                                                    Console.WriteLine($"set value {gpioPin.Name} off");
                                                }
                                                catch (Exception ex)
                                                {
                                                    Console.WriteLine($"set value error: {ex.Message}");
                                                }
                                                break;
                                            case "on":
                                            case "":
                                            case null:
                                                try
                                                {
                                                    gpioPin.Value = true;
                                                    Console.WriteLine($"set value {gpioPin.Name} on");
                                                }
                                                catch (Exception ex)
                                                {
                                                    Console.WriteLine($"set value error: {ex.Message}");
                                                }
                                                break;
                                            default:
                                                Console.WriteLine($"set value error: invalid option {fourth}");
                                                break;
                                        }
                                        break;
                                    case "direction":
                                        switch (fourth)
                                        {
                                            case "input":
                                                gpioPin.Direction = GpioDirection.Input;
                                                Console.WriteLine($"set direction {gpioPin.Name} input");
                                                break;
                                            case "output":
                                                gpioPin.Direction = GpioDirection.Output;
                                                Console.WriteLine($"set direction {gpioPin.Name} output");
                                                break;
                                            default:
                                                Console.WriteLine($"set direction error: invalid option {fourth}");
                                                break;
                                        }
                                        break;
                                    case "activelow":
                                        switch (fourth)
                                        {
                                            case "off":
                                                gpioPin.ActiveLow = false;
                                                Console.WriteLine($"set activelow {gpioPin.Name} off");
                                                break;
                                            case "on":
                                            case "":
                                            case null:
                                                gpioPin.ActiveLow = true;
                                                Console.WriteLine($"set activelow {gpioPin.Name} on");
                                                break;
                                            default:
                                                Console.WriteLine($"set activelow error: invalid option {fourth}");
                                                break;
                                        }
                                        break;
                                    case "events":
                                        switch (fourth)
                                        {
                                            case "off":
                                                gpioPin.EnableRaisingEvents = false;
                                                Console.WriteLine($"set events {gpioPin.Name} off");
                                                break;
                                            case "on":
                                            case "":
                                            case null:
                                                gpioPin.EnableRaisingEvents = true;
                                                Console.WriteLine($"set events {gpioPin.Name} on");
                                                break;
                                            default:
                                                Console.WriteLine($"set events error: invalid option {fourth}");
                                                break;
                                        }
                                        break;
                                    case "export":
                                        if (gpioPin is LinuxGpioPin linuxGpioPin)
                                        {
                                            switch (fourth)
                                            {
                                                case "off":
                                                    linuxGpioPin.Export = false;
                                                    Console.WriteLine($"set export {gpioPin.Name} off");
                                                    break;
                                                case "on":
                                                case "":
                                                case null:
                                                    linuxGpioPin.Export = true;
                                                    Console.WriteLine($"set export {gpioPin.Name} on");
                                                    break;
                                                default:
                                                    Console.WriteLine($"set export error: invalid option {fourth}");
                                                    break;
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine($"set export error: gpio {gpioPin.Name} is not compatible with export");
                                        }
                                        break;
                                    case "unexport":
                                        if (gpioPin is LinuxGpioPin linuxGpioPin2)
                                        {
                                            switch (fourth)
                                            {
                                                case "off":
                                                    linuxGpioPin2.Unexport = false;
                                                    Console.WriteLine($"set unexport {gpioPin.Name} off");
                                                    break;
                                                case "on":
                                                case "":
                                                case null:
                                                    linuxGpioPin2.Unexport = true;
                                                    Console.WriteLine($"set unexport {gpioPin.Name} on");
                                                    break;
                                                default:
                                                    Console.WriteLine($"set export error: invalid option {fourth}");
                                                    break;
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine($"set unexport error: gpio {gpioPin.Name} is not compatible with export");
                                        }
                                        break;
                                    default:
                                        Console.WriteLine($"set error: invalid option {third}");
                                        break;
                                }
                            }
                            else
                            {
                                Console.WriteLine($"set error: gpio pin {gpioNumber} not initialized");
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
            Console.WriteLine($"event: {e.Pin.Name} value {(e.Value ? "on" : "off")}");
        }
    }
}
