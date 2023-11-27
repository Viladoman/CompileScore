using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CompileScore
{
    public class ExternalProcess
    {
        static public ExternalProcess Global { get; } = new ExternalProcess();

        public OutputLog.PaneInstance LogPane { get; set; } = OutputLog.PaneInstance.Default;

        public int ExecuteSync(string toolPath, string arguments)
        {
            return RunProcess(toolPath, arguments);
        }

        public Task<int> ExecuteAsync(string toolPath, string arguments)
        {
            return Task.Run(() => RunProcess(toolPath, arguments));
        }

        private int RunProcess(string toolPath, string arguments)
        {
            Process process = new Process();

            process.StartInfo.FileName = toolPath;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            process.ErrorDataReceived += (sender, errorLine) =>
            {
                if (errorLine.Data != null)
                {
                    OutputLine(errorLine.Data);
                }
            };
            process.OutputDataReceived += (sender, outputLine) =>
            {
                if (outputLine.Data != null)
                {
                    OutputLine(outputLine.Data);
                }
            };

            try
            {
                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
                process.WaitForExit();
            }
            catch (Exception error)
            {
                OutputLine(error.Message);
                return -1;
            }

            int exitCode = process.ExitCode;
            process.Close();

            return exitCode;
        }

        private void OutputLine(string str)
        {
            if (str != null)
            {
                var pane = OutputLog.GetPaneUnsafe(LogPane);
                if ( pane != null )
                {
#pragma warning disable 414, VSTHRD010
                    pane.OutputStringThreadSafe(str + '\n');
#pragma warning restore VSTHRD010
                }
            }
        }
    }
}
 
