using System;
using System.Diagnostics;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Help();
            return;
        }

        // devices komutu özel, cihaz kontrolünden ÖNCE
        if (args[0].ToLower() == "devices")
        {
            ListDevices();
            return;
        }

        string state;
        string serial;

        if (!GetDeviceInfo(out serial, out state))
        {
            Console.WriteLine("andusbc: error: no device connected [Error 310]");
            return;
        }

        if (state == "unauthorized")
        {
            Console.WriteLine("andusbc: error: device is unauthorized [Error 200]");
            return;
        }

        Console.WriteLine($"connected the {serial} device and device is [{state}]");

        if (state != "device")
            return;

        string cmd = args[0].ToLower();
        bool hold = args.Length > 1 && args[1].ToLower() == "hold";

        switch (cmd)
        {
            case "touch":
                if (args.Length != 3)
                {
                    Console.WriteLine("usage: andusbc touch <x> <y>");
                    return;
                }
                Tap(int.Parse(args[1]), int.Parse(args[2]));
                break;

            case "swipe":
                if (args.Length != 6)
                {
                    Console.WriteLine("usage: andusbc swipe <x1> <y1> <x2> <y2> <ms>");
                    return;
                }
                Console.WriteLine("andusbc: info: this command didn't work when tried, so it might not work for you either.");
                Swipe(
                    int.Parse(args[1]),
                    int.Parse(args[2]),
                    int.Parse(args[3]),
                    int.Parse(args[4]),
                    int.Parse(args[5])
                );
                break;

            case "screensize":
                GetScreenSize();
                break;

            case "powerkey":
                KeyEvent("KEYCODE_POWER", hold);
                Console.WriteLine(hold ? "power key long pressed" : "power key pressed");
                break;

            case "volumeup":
                KeyEvent("KEYCODE_VOLUME_UP", hold);
                Console.WriteLine(hold ? "volume up long pressed" : "volume up pressed");
                break;

            case "volumedown":
                KeyEvent("KEYCODE_VOLUME_DOWN", hold);
                Console.WriteLine(hold ? "volume down long pressed" : "volume down pressed");
                break;

            case "version":
                Version();
                break;

            case "help":
                Help();
                break;

            case "screenshot":
                string filename = args.Length > 1 ? args[1] : "screenshot.png";
                Screenshot(filename);
                break;

            default:
                Help();
                break;
        }
    }

    static void ListDevices()
    {
        string output = RunAdb("devices");
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        bool found = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("List")) continue;

            var p = line.Trim().Split('\t');
            if (p.Length >= 2)
            {
                Console.WriteLine(
                    $"connected the {p[0]} device and device is [{p[1]}]"
                );
                found = true;
            }
        }

        if (!found)
        {
            Console.WriteLine("andusbc: error: no device connected [Error 310]");
        }
    }

    static void Help()
    {
        Console.WriteLine(
@"usage: andusbc <command>
  devices                               Shows connected devices.
  touch <x> <y>                         Touch the selected x and y positions of the device.
  swipe <x1> <y1> <x2> <y2> <ms>        The device slides between the given coordinates.
  screensize                            Shows the device screen resolution.
  powerkey [hold]                       Press or hold the device's power key.
  volumeup [hold]                       Press or hold the device's volume up key.
  volumedown [hold]                     Press or hold the device's volume down key.
  version                               The program prints its version to the screen.
  help                                  Shows this help message."
        );
    }

    static void Version()
    {
        Console.WriteLine("Android USB Control v1.0");
    }

    static void Tap(int x, int y)
    {
        RunAdb($"shell input tap {x} {y}");
        Console.WriteLine($"touch sent to the device ({x},{y})");
    }

    static void Swipe(int x1, int y1, int x2, int y2, int ms)
    {
        RunAdb($"shell input swipe {x1} {y1} {x2} {y2} {ms}");
        Console.WriteLine("swipe sent to the device");
    }

    static void KeyEvent(string key, bool hold)
    {
        string flag = hold ? "--longpress " : "";
        RunAdb($"shell input keyevent {flag}{key}");
    }

    static bool GetDeviceInfo(out string serial, out string state)
    {
        serial = "";
        state = "";

        string output = RunAdb("devices");
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.StartsWith("List")) continue;

            var p = line.Trim().Split('\t');
            if (p.Length >= 2)
            {
                serial = p[0];
                state = p[1];
                return true;
            }
        }

        return false;
    }

    static string RunAdb(string args)
    {
        var p = new Process();
        p.StartInfo.FileName = "adb";
        p.StartInfo.Arguments = args;
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.CreateNoWindow = true;
        p.Start();

        return p.StandardOutput.ReadToEnd();
    }
    static void Screenshot(string filename)
    {
        // RunAdb fonksiyonu string döndürdüğü için binary desteği yok
        // Bu yüzden direkt Process kullanıyoruz (RunAdb kullanıyor ama binary base stream ile)
        var p = new Process();
        p.StartInfo.FileName = "adb";
        p.StartInfo.Arguments = "exec-out screencap -p";
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.CreateNoWindow = true;
        p.Start();

        using (var fs = new System.IO.FileStream(filename, System.IO.FileMode.Create))
        {
            p.StandardOutput.BaseStream.CopyTo(fs); // binary olarak kopyala
        }

        p.WaitForExit();
        Console.WriteLine($"screenshot saved to {filename}");
    }
    static void GetScreenSize()
    {
        string output = RunAdb("shell wm size");

        string physical = "";
        string logical = "";

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.Contains("Physical size"))
                physical = line.Split(':')[1].Trim();

            if (line.Contains("Override size"))
                logical = line.Split(':')[1].Trim();
        }

        if (physical != "")
            Console.WriteLine($"screen physical size is [{physical}]");

        if (logical != "")
            Console.WriteLine($"screen override size is [{logical}]");

        if (physical == "" && logical == "")
            Console.WriteLine("andusbc: error: unable to get screen size [Error 604]");
    }
}