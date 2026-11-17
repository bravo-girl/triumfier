using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Threading;
using System.Windows;

namespace NativeViewer
{
    public partial class App : Application
    {
        public App()
        {
            CultureInfo germanCulture = CultureInfo.GetCultureInfo("de-DE");
            Thread.CurrentThread.CurrentCulture = germanCulture;
            Thread.CurrentThread.CurrentUICulture = germanCulture;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            foreach (string argument in e.Args)
            {
                if (argument == "--help" || argument == "-h" || argument == "/?")
                {
                    MessageBox.Show(
                        "Aufruf: RhaView [Datei ...]\n\n" +
                        "Jede Datei wird in einem eigenen Fenster geöffnet.\n" +
                        "Dateimuster im Dateinamen unterstützen * und ?, zum Beispiel:\n" +
                        "RhaView .\\*.xml",
                        "RhaView – Hilfe", MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown();
                    return;
                }
            }

            List<string> errors = new List<string>();
            List<string> paths = ExpandArguments(e.Args, errors);
            if (errors.Count > 0)
            {
                MessageBox.Show(string.Join(Environment.NewLine, errors),
                                "RhaView – Dateien konnten nicht gefunden werden",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            if (paths.Count == 0)
            {
                OpenFileInNewWindow(null);
                return;
            }

            foreach (string path in paths)
            {
                OpenFileInNewWindow(path);
            }
        }

        public static MainWindow OpenFileInNewWindow(string path)
        {
            MainWindow window = new MainWindow();
            if (!string.IsNullOrEmpty(path))
            {
                window.OpenFile(path);
            }
            if (Current.MainWindow == null)
            {
                Current.MainWindow = window;
            }
            window.Show();
            return window;
        }

        private static List<string> ExpandArguments(string[] arguments, List<string> errors)
        {
            List<string> paths = new List<string>();
            foreach (string argument in arguments)
            {
                if (string.IsNullOrWhiteSpace(argument))
                {
                    continue;
                }
                if (argument.IndexOfAny(new[] { '*', '?' }) < 0)
                {
                    paths.Add(argument);
                    continue;
                }

                string directory = Path.GetDirectoryName(argument);
                string pattern = Path.GetFileName(argument);
                if (string.IsNullOrEmpty(directory))
                {
                    directory = Environment.CurrentDirectory;
                }
                if (directory.IndexOfAny(new[] { '*', '?' }) >= 0)
                {
                    errors.Add("Platzhalter im Ordnerpfad werden nicht unterstützt: " + argument);
                    continue;
                }

                try
                {
                    string fullDirectory = Path.GetFullPath(directory);
                    if (!Directory.Exists(fullDirectory))
                    {
                        errors.Add("Ordner nicht gefunden: " + fullDirectory);
                        continue;
                    }

                    string[] matches = Directory.GetFiles(fullDirectory, pattern, SearchOption.TopDirectoryOnly);
                    Array.Sort(matches, StringComparer.OrdinalIgnoreCase);
                    if (matches.Length == 0)
                    {
                        errors.Add("Keine Dateien für das Muster gefunden: " + argument);
                        continue;
                    }
                    paths.AddRange(matches);
                }
                catch (IOException ex)
                {
                    errors.Add("Dateimuster konnte nicht gelesen werden: " + argument + " (" + ex.Message + ")");
                }
                catch (UnauthorizedAccessException ex)
                {
                    errors.Add("Kein Zugriff auf den Ordner für das Muster " + argument + ": " + ex.Message);
                }
                catch (SecurityException ex)
                {
                    errors.Add("Kein Zugriff auf den Ordner für das Muster " + argument + ": " + ex.Message);
                }
                catch (ArgumentException ex)
                {
                    errors.Add("Ungültiges Dateimuster " + argument + ": " + ex.Message);
                }
            }
            return paths;
        }
    }
}
