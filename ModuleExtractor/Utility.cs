using System;
using System.Diagnostics;

namespace ModuleExtractor
{
    public class Utility
    {
        public static int RunShellCommand(string command)
        {
            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = string.Format("{0} {1}", "/C", command),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            proc.Start();
            proc.WaitForExit();
            return proc.ExitCode;
        }
    }
}
