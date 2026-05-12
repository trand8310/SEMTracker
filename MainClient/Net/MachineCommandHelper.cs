
using System.Diagnostics;

namespace MainClient.Net;

public static class MachineCommandHelper
{
    public static void Restart()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "shutdown",
            Arguments = "/r /t 0",
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }

    public static void Logoff()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "shutdown",
            Arguments = "/l",
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }
}