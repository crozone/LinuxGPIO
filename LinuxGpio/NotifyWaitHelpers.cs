using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace crozone.LinuxGpio
{
    public static class NotifyWaitHelpers
    {
        // Set the inotifywait binary path.
        //
        // /usr/bin/inotifywait is the usual installation location for most Linux distros.
        //
        public static readonly string INotifyWaitPath = Path.DirectorySeparatorChar + Path.Combine("usr", "bin", "inotifywait");

        /// <summary>
        /// Starts an inotifywait operation on the given path.
        /// When cancelled, this task transitions to a successful completion, rather than
        /// throwing a task cancellation exception.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="events"></param>
        /// <param name="notifyCallback"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static async Task NotifyWait(
            string path,
            string[] events,
            Action<NotifyWaitResponse> notifyCallback,
            CancellationToken token)
        {
            using (Process process = new Process())
            {
                process.StartInfo.FileName = INotifyWaitPath;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.Arguments = $"-m {(events == null ? "" : $"-e { string.Join(" ", events)}")} -q -c {path}";
                process.Start();

                try
                {
                    using (token.Register(() => process.Kill()))
                    {
                        while (!process.HasExited)
                        {
                            // Read a line from the inotifywait process.
                            // This should throw an exception if it is blocking while
                            // the process is killed.
                            //
                            string line = await process.StandardOutput.ReadLineAsync();

                            token.ThrowIfCancellationRequested();

                            // Every time we read a line, announce the inotify with the notify callback
                            //
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                string[] parts = CSVParser.ParseLine(line).ToArray();

                                if (parts.Length == 3)
                                {
                                    string[] eventNames = parts[1].Split(',');
                                    NotifyWaitResponse response = new NotifyWaitResponse()
                                    {
                                        WatchedFilename = parts[0],
                                        EventNames = eventNames,
                                        EventFilename = parts[2]
                                    };

                                    notifyCallback(response);
                                }
                            }
                        }
                    }
                }
                finally
                {
                    try
                    {
                        // Ensure the process is killed.
                        //
                        process.Kill();
                    }
                    catch
                    {

                    }
                }
            }
        }
    }
}
