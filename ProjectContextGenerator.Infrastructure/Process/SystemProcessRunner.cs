using System.Diagnostics;
using System.Text;
using ProjectContextGenerator.Domain.Abstractions;

namespace ProjectContextGenerator.Infrastructure.Process
{
    public sealed class SystemProcessRunner : IProcessRunner
    {
        public ProcessResult Run(string fileName, string arguments, string workingDirectory, TimeSpan? timeout = null)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var p = new System.Diagnostics.Process { StartInfo = psi };
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            p.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

            if (!p.Start())
                return new ProcessResult { ExitCode = -1, StdOut = "", StdErr = "Failed to start process." };

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            if (!p.WaitForExit((int)(timeout ?? TimeSpan.FromSeconds(15)).TotalMilliseconds))
            {
                try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return new ProcessResult { ExitCode = -1, StdOut = stdout.ToString(), StdErr = "Process timed out." };
            }

            return new ProcessResult
            {
                ExitCode = p.ExitCode,
                StdOut = stdout.ToString(),
                StdErr = stderr.ToString()
            };

        }
    }
}
