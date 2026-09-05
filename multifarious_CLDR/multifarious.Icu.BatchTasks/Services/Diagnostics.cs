using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace multifarious.Icu.BatchTasks.Services
{
    /// <summary>
    /// Writes what the tasks and their content processors actually did to a log file, so
    /// behaviour inside Studio's process can be observed rather than inferred. Out-of-process
    /// tests can only show what we hand the framework; this shows what happened when the
    /// framework was really driving.
    ///
    /// Off unless MULTIFARIOUS_ICU_DIAGNOSTICS is set, so a normal run writes nothing. Ported
    /// from the sibling plugins on their own evidence: every Studio-side puzzle reasoned about
    /// there cost several wrong rounds, and every one instrumented was solved in one log line.
    /// </summary>
    public static class Diagnostics
    {
        private static readonly object Gate = new object();

        private static readonly bool Enabled =
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MULTIFARIOUS_ICU_DIAGNOSTICS"));

        public static string LogPath
        {
            get { return Path.Combine(Path.GetTempPath(), "multifarious-icu-diagnostics.log"); }
        }

        /// <summary>Begins the log fresh for one task run.</summary>
        public static void Start(string what)
        {
            if (!Enabled) return;
            lock (Gate)
            {
                try
                {
                    File.WriteAllText(LogPath,
                        "=== " + what + " at " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
                        + " ===" + Environment.NewLine);
                }
                catch (Exception)
                {
                    // Diagnostics must never break the run they are diagnosing.
                }
            }
        }

        public static void Write(string message)
        {
            if (!Enabled) return;
            lock (Gate)
            {
                try
                {
                    File.AppendAllText(LogPath, message + Environment.NewLine, Encoding.UTF8);
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
