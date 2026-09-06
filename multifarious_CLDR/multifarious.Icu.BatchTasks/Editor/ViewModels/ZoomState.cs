using System;
using System.Globalization;
using System.IO;

namespace multifarious.Icu.BatchTasks.Editor.ViewModels
{
    /// <summary>
    /// The ICU Forms window's zoom, in steps between 60% and 250%, remembered in a small file
    /// under the user's roaming profile so it survives a restart as Studio's own zoom does.
    /// The store path is a parameter so the tests keep out of the profile.
    /// </summary>
    public sealed class ZoomState
    {
        public const double Minimum = 0.6;
        public const double Maximum = 2.5;
        public const double Step = 0.1;

        private readonly string _storePath;

        public ZoomState(string storePath = null)
        {
            _storePath = storePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "multifarious", "IcuSupport", "forms-zoom.txt");
            Factor = Load();
        }

        public double Factor { get; private set; }

        public bool CanZoomIn { get { return Factor < Maximum - 0.0001; } }

        public bool CanZoomOut { get { return Factor > Minimum + 0.0001; } }

        public double ZoomIn()
        {
            return Set(Factor + Step);
        }

        public double ZoomOut()
        {
            return Set(Factor - Step);
        }

        public double Reset()
        {
            return Set(1.0);
        }

        /// <summary>Clamps to the range, rounds to the step, stores and returns the factor.</summary>
        public double Set(double factor)
        {
            var rounded = Math.Round(factor / Step) * Step;
            Factor = Math.Max(Minimum, Math.Min(Maximum, rounded));
            Save();
            return Factor;
        }

        private double Load()
        {
            try
            {
                if (File.Exists(_storePath))
                {
                    double stored;
                    if (double.TryParse(File.ReadAllText(_storePath).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out stored)
                        && stored >= Minimum && stored <= Maximum)
                    {
                        return Math.Round(stored / Step) * Step;
                    }
                }
            }
            catch (Exception)
            {
                // A zoom that cannot be read is a zoom of 100%.
            }

            return 1.0;
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_storePath));
                File.WriteAllText(_storePath, Factor.ToString("0.##", CultureInfo.InvariantCulture));
            }
            catch (Exception)
            {
                // Remembering the zoom must never break the window.
            }
        }
    }
}
