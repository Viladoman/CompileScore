using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace CompileScore
{
    public static class ExternalProcess
    {
        public static int ExecuteSync(string toolPath, string arguments)
        {
            var process = StartProcess(toolPath, arguments);

            if (process == null)
            {
                return -1;
            }

            WaitForExit(process);

            return process.ExitCode;
        }

        public static async Task<int> ExecuteAsync(string toolPath, string arguments)
        {
            var process = StartProcess(toolPath, arguments);

            if (process == null)
            {
                return -1;
            }

            await Task.Run(() => WaitForExit(process));

            return process.ExitCode;
        }

        private static Process StartProcess(string toolPath, string arguments)
        {
            var process = new Process();

            process.StartInfo.FileName = toolPath;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            try
            {
                process.Start();
            }
            catch (Exception error)
            {
                OutputLine(error.Message);
                return null;
            }

            return process;
        }

        private static void WaitForExit(Process process)
        {
            //Handle output
            while (!process.StandardOutput.EndOfStream || !process.StandardError.EndOfStream)
            {
                if (!process.StandardOutput.EndOfStream)
                {
                    OutputLine(process.StandardOutput.ReadLine());
                }

                if (!process.StandardError.EndOfStream)
                {
                    OutputLine(process.StandardError.ReadLine());
                }
            }

            process.WaitForExit();
        }

        private static void OutputLine(string str)
        {
            if (str != null)
            {
#pragma warning disable 414, VSTHRD010
                OutputLog.GetPane().OutputStringThreadSafe(str + '\n');
#pragma warning restore VSTHRD010
            }
        }

    }
}
 
