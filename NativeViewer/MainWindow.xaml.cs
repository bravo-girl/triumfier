using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace NativeViewer
{
    public partial class MainWindow : Window
    {
        private const int DefaultIndentSize = 2;
        private const int MaximumRecentFiles = 50;
        private static readonly Regex MarkdownInlinePattern =
            new Regex(@"(\*\*.+?\*\*|__.+?__|`[^`]+`|\*[^*]+\*|_[^_]+_)", RegexOptions.Compiled);
        private static readonly List<string> recentFiles = new List<string>();
        private static readonly string settingsPath = Path.Combine(AppContext.BaseDirectory, "RhaView.settings.xml");
        private static bool settingsLoaded;
        private string currentPath;
        private string rawContent;
        private string currentExtension;
        private string lastSearchQuery;
        private int searchStartIndex;
        private double fontSize = 14;
        private static int indentSize = DefaultIndentSize;
        private Encoding fileEncoding = new UTF8Encoding(false, true);
        private bool fileHasPreamble;

        public MainWindow()
        {
            InitializeComponent();
            if (!settingsLoaded)
            {
                settingsLoaded = true;
                LoadSettings();
            }
            UpdateIndentMenuChecks();
            RefreshRecentFilesMenu();
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Unterstützte Dateien|*.json;*.xml;*.md;*.markdown;*.txt|Alle Dateien|*.*",
                CheckFileExists = true,
                Multiselect = true
            };

            if (dialog.ShowDialog(this) == true)
            {
                OpenFiles(dialog.FileNames);
            }
        }

        private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SaveFile(false);
        }

        private void SaveAsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SaveFile(true);
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CommandLineHelpMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this,
                "Aufruf: RhaView [Datei ...]\n\n" +
                "Mehrere Dateien werden jeweils in einem eigenen Fenster geöffnet.\n" +
                "Dateimuster mit * oder ? werden im angegebenen Ordner aufgelöst, zum Beispiel:\n" +
                "RhaView .\\*.xml",
                "RhaView – Hilfe", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this, "RhaView\n\nDateibetrachter für JSON, XML, Markdown und Text.",
                            "Über RhaView", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RecentFilesMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            RemoveMissingRecentFiles();
        }

        private void PrettyPrintMenuItem_Click(object sender, RoutedEventArgs e)
        {
            RefreshView();
        }

        private void MarkdownRenderedMenuItem_Click(object sender, RoutedEventArgs e)
        {
            RefreshView();
        }

        private void WordWrapMenuItem_Click(object sender, RoutedEventArgs e)
        {
            TextView.TextWrapping = WordWrapMenuItem.IsChecked ? TextWrapping.Wrap : TextWrapping.NoWrap;
            TextView.HorizontalScrollBarVisibility = WordWrapMenuItem.IsChecked
                ? System.Windows.Controls.ScrollBarVisibility.Disabled
                : System.Windows.Controls.ScrollBarVisibility.Auto;
        }

        private void SearchMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (SearchPanel.Visibility == Visibility.Visible)
            {
                SearchPanel.Visibility = Visibility.Collapsed;
                TextView.Focus();
                return;
            }

            SearchPanel.Visibility = Visibility.Visible;
            SearchTextBox.Focus();
            SearchTextBox.SelectAll();
        }

        private void CloseSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchPanel.Visibility = Visibility.Collapsed;
            TextView.Focus();
        }

        private void FindNextButton_Click(object sender, RoutedEventArgs e)
        {
            FindNext();
        }

        private void SearchTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                FindNext();
                e.Handled = true;
            }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (KeyboardShortcut(e, System.Windows.Input.Key.O, false))
            {
                OpenButton_Click(sender, e);
            }
            else if (e.Key == System.Windows.Input.Key.S &&
                     (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
            {
                SaveFile(System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift));
            }
            else if (KeyboardShortcut(e, System.Windows.Input.Key.F, false))
            {
                SearchMenuItem_Click(sender, e);
            }
            else
            {
                return;
            }
            e.Handled = true;
        }

        private static bool KeyboardShortcut(System.Windows.Input.KeyEventArgs e, System.Windows.Input.Key key, bool shift)
        {
            System.Windows.Input.ModifierKeys modifiers = System.Windows.Input.Keyboard.Modifiers;
            return e.Key == key &&
                   (modifiers & System.Windows.Input.ModifierKeys.Control) != 0 &&
                   ((modifiers & System.Windows.Input.ModifierKeys.Shift) != 0) == shift;
        }

        private void IncreaseFontMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetFontSize(fontSize + 1);
        }

        private void DecreaseFontMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetFontSize(fontSize - 1);
        }

        private void ResetFontMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetFontSize(14);
        }

        private void IndentSizeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            int selectedSize;
            if (menuItem != null &&
                int.TryParse(Convert.ToString(menuItem.Tag, CultureInfo.InvariantCulture), NumberStyles.None,
                             CultureInfo.InvariantCulture, out selectedSize))
            {
                SetIndentSize(selectedSize);
            }
        }

        private void CustomIndentSizeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Window dialog = new Window
            {
                Title = "Einrückung festlegen",
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                SizeToContent = SizeToContent.WidthAndHeight,
                ShowInTaskbar = false
            };
            StackPanel content = new StackPanel { Margin = new Thickness(16) };
            content.Children.Add(new TextBlock
            {
                Text = "Leerzeichen pro Einrückungsebene (1–8):",
                Margin = new Thickness(0, 0, 0, 8)
            });

            TextBox sizeInput = new TextBox
            {
                Text = indentSize.ToString(CultureInfo.InvariantCulture),
                MaxLength = 1,
                MinWidth = 180
            };
            content.Children.Add(sizeInput);

            StackPanel buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            Button applyButton = new Button
            {
                Content = "Übernehmen",
                MinWidth = 90,
                Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true
            };
            Button cancelButton = new Button
            {
                Content = "Abbrechen",
                MinWidth = 90,
                IsCancel = true
            };
            int selectedSize = indentSize;
            applyButton.Click += delegate
            {
                if (!int.TryParse(sizeInput.Text, NumberStyles.None, CultureInfo.InvariantCulture, out selectedSize) ||
                    selectedSize < 1 || selectedSize > 8)
                {
                    MessageBox.Show(dialog, "Bitte eine ganze Zahl zwischen 1 und 8 eingeben.",
                                    "Ungültige Einrückung", MessageBoxButton.OK, MessageBoxImage.Warning);
                    sizeInput.Focus();
                    sizeInput.SelectAll();
                    return;
                }
                dialog.DialogResult = true;
            };
            buttons.Children.Add(applyButton);
            buttons.Children.Add(cancelButton);
            content.Children.Add(buttons);
            dialog.Content = content;
            dialog.Loaded += delegate
            {
                sizeInput.Focus();
                sizeInput.SelectAll();
            };

            if (dialog.ShowDialog() == true)
            {
                SetIndentSize(selectedSize);
            }
        }

        private void SetFontSize(double size)
        {
            fontSize = Math.Max(8, Math.Min(32, size));
            TextView.FontSize = fontSize;
            RefreshView();
        }

        private void SetIndentSize(int size)
        {
            if (size < 1 || size > 8)
            {
                return;
            }

            indentSize = size;
            UpdateIndentMenuChecks();
            RefreshView();
            foreach (Window openWindow in Application.Current.Windows)
            {
                MainWindow viewer = openWindow as MainWindow;
                if (viewer != null && viewer != this)
                {
                    viewer.UpdateIndentMenuChecks();
                    viewer.RefreshView();
                }
            }
            StatusText.Text = "Einrückung: " + size.ToString(CultureInfo.InvariantCulture) + " Leerzeichen";
            SaveSettings();
        }

        private void UpdateIndentMenuChecks()
        {
            Indent2MenuItem.IsChecked = indentSize == 2;
            Indent4MenuItem.IsChecked = indentSize == 4;
            Indent8MenuItem.IsChecked = indentSize == 8;
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths != null && paths.Length > 0)
            {
                OpenFiles(paths);
            }
        }

        private void OpenFiles(string[] paths)
        {
            if (paths == null || paths.Length == 0)
            {
                return;
            }

            LoadFile(paths[0]);
            for (int i = 1; i < paths.Length; i++)
            {
                App.OpenFileInNewWindow(paths[i]);
            }
        }

        public void OpenFile(string path)
        {
            LoadFile(path);
        }

        private void LoadFile(string path)
        {
            try
            {
                string content = ReadTextFile(path);
                DisplayFile(path, content);
            }
            catch (FileNotFoundException ex)
            {
                RemoveRecentFile(path);
                ShowError(path, "Datei wurde nicht gefunden: " + ex.Message);
            }
            catch (DirectoryNotFoundException ex)
            {
                RemoveRecentFile(path);
                ShowError(path, "Der Dateiordner wurde nicht gefunden: " + ex.Message);
            }
            catch (IOException ex)
            {
                ShowError(path, "Datei konnte nicht gelesen werden: " + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                ShowError(path, "Zugriff auf die Datei verweigert: " + ex.Message);
            }
            catch (SecurityException ex)
            {
                ShowError(path, "Zugriff auf die Datei verweigert: " + ex.Message);
            }
            catch (DecoderFallbackException ex)
            {
                ShowError(path, "Dateikodierung konnte nicht gelesen werden: " + ex.Message);
            }
            catch (ArgumentException ex)
            {
                ShowError(path, "Ungültiger Dateipfad: " + ex.Message);
            }
        }

        private string ReadTextFile(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            int preambleLength;
            fileEncoding = DetectEncoding(bytes, out preambleLength, out fileHasPreamble);
            return fileEncoding.GetString(bytes, preambleLength, bytes.Length - preambleLength);
        }

        private static Encoding DetectEncoding(byte[] bytes, out int preambleLength, out bool hasPreamble)
        {
            if (StartsWith(bytes, 0xFF, 0xFE, 0x00, 0x00))
            {
                preambleLength = 4;
                hasPreamble = true;
                return new UTF32Encoding(false, true, true);
            }
            if (StartsWith(bytes, 0x00, 0x00, 0xFE, 0xFF))
            {
                preambleLength = 4;
                hasPreamble = true;
                return new UTF32Encoding(true, true, true);
            }
            if (StartsWith(bytes, 0xEF, 0xBB, 0xBF))
            {
                preambleLength = 3;
                hasPreamble = true;
                return new UTF8Encoding(true, true);
            }
            if (StartsWith(bytes, 0xFE, 0xFF))
            {
                preambleLength = 2;
                hasPreamble = true;
                return new UnicodeEncoding(true, true, true);
            }
            if (StartsWith(bytes, 0xFF, 0xFE))
            {
                preambleLength = 2;
                hasPreamble = true;
                return new UnicodeEncoding(false, true, true);
            }

            preambleLength = 0;
            hasPreamble = false;
            return new UTF8Encoding(false, true);
        }

        private static bool StartsWith(byte[] bytes, params byte[] signature)
        {
            if (bytes.Length < signature.Length)
            {
                return false;
            }
            for (int i = 0; i < signature.Length; i++)
            {
                if (bytes[i] != signature[i])
                {
                    return false;
                }
            }
            return true;
        }

        private void DisplayFile(string path, string content)
        {
            currentPath = path;
            rawContent = content;
            currentExtension = Path.GetExtension(path).ToLowerInvariant();
            MarkdownRenderedMenuItem.IsEnabled = currentExtension == ".md" || currentExtension == ".markdown";
            MarkdownRenderedMenuItem.IsChecked = MarkdownRenderedMenuItem.IsEnabled;
            lastSearchQuery = null;
            searchStartIndex = 0;
            FileNameText.Text = Path.GetFileName(path);
            FilePathText.Text = path;
            Title = Path.GetFileName(path) + " – RhaView";
            RefreshView();
            AddRecentFile(path);
        }

        private void AddRecentFile(string path)
        {
            recentFiles.RemoveAll(existing => string.Equals(existing, path, StringComparison.OrdinalIgnoreCase));
            recentFiles.Insert(0, path);
            if (recentFiles.Count > MaximumRecentFiles)
            {
                recentFiles.RemoveRange(MaximumRecentFiles, recentFiles.Count - MaximumRecentFiles);
            }
            RefreshRecentFileMenus();
            SaveSettings();
        }

        private void RemoveMissingRecentFiles()
        {
            int removed = recentFiles.RemoveAll(path => !File.Exists(path));
            if (removed == 0)
            {
                return;
            }

            RefreshRecentFileMenus();
            SaveSettings();
            StatusText.Text = removed == 1
                ? "Eine nicht mehr vorhandene Datei wurde aus dem Verlauf entfernt."
                : removed.ToString(CultureInfo.InvariantCulture) + " nicht mehr vorhandene Dateien wurden aus dem Verlauf entfernt.";
        }

        private void RemoveRecentFile(string path)
        {
            if (recentFiles.RemoveAll(existing => string.Equals(existing, path, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                RefreshRecentFileMenus();
                SaveSettings();
            }
        }

        private void RefreshRecentFileMenus()
        {
            RefreshRecentFilesMenu();
            foreach (Window openWindow in Application.Current.Windows)
            {
                MainWindow viewer = openWindow as MainWindow;
                if (viewer != null && viewer != this)
                {
                    viewer.RefreshRecentFilesMenu();
                }
            }
        }

        private void RefreshRecentFilesMenu()
        {
            RecentFilesMenuItem.Items.Clear();
            if (recentFiles.Count == 0)
            {
                RecentFilesMenuItem.Items.Add(new MenuItem
                {
                    Header = "Keine zuletzt geöffneten Dateien",
                    IsEnabled = false
                });
                return;
            }

            foreach (string path in recentFiles)
            {
                MenuItem item = new MenuItem
                {
                    Header = Path.GetFileName(path),
                    ToolTip = path,
                    Tag = path
                };
                item.Click += RecentFileMenuItem_Click;
                RecentFilesMenuItem.Items.Add(item);
            }
        }

        private void RecentFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = sender as MenuItem;
            string path = item == null ? null : item.Tag as string;
            if (!string.IsNullOrEmpty(path))
            {
                LoadFile(path);
            }
        }

        private void LoadSettings()
        {
            if (!File.Exists(settingsPath))
            {
                return;
            }

            bool removedMissingFiles = false;
            try
            {
                XDocument document = XDocument.Load(settingsPath);
                XElement root = document.Root;
                if (root == null || root.Name != "RhaViewSettings")
                {
                    StatusText.Text = "Die lokale Einstellungsdatei hat ein ungültiges Format.";
                    return;
                }

                XElement indentElement = root.Element("IndentSize");
                if (indentElement != null)
                {
                    int savedIndentSize;
                    if (int.TryParse(indentElement.Value, NumberStyles.None, CultureInfo.InvariantCulture, out savedIndentSize) &&
                        savedIndentSize >= 1 && savedIndentSize <= 8)
                    {
                        indentSize = savedIndentSize;
                    }
                    else
                    {
                        StatusText.Text = "Die gespeicherte Einrückung ist ungültig; der Standardwert wird verwendet.";
                    }
                }

                XElement recentElement = root.Element("RecentFiles");
                if (recentElement != null)
                {
                    HashSet<string> uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (XElement fileElement in recentElement.Elements("File"))
                    {
                        string path = fileElement.Value;
                        if (!string.IsNullOrWhiteSpace(path) && uniquePaths.Add(path))
                        {
                            if (File.Exists(path))
                            {
                                recentFiles.Add(path);
                                if (recentFiles.Count == MaximumRecentFiles)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                removedMissingFiles = true;
                            }
                        }
                    }
                }
            }
            catch (IOException ex)
            {
                StatusText.Text = "Lokale Einstellungen konnten nicht gelesen werden: " + ex.Message;
            }
            catch (UnauthorizedAccessException ex)
            {
                StatusText.Text = "Kein Zugriff auf die lokalen Einstellungen: " + ex.Message;
            }
            catch (SecurityException ex)
            {
                StatusText.Text = "Kein Zugriff auf die lokalen Einstellungen: " + ex.Message;
            }
            catch (XmlException ex)
            {
                StatusText.Text = "Die lokale Einstellungsdatei ist ungültig: " + ex.Message;
            }

            if (removedMissingFiles)
            {
                SaveSettings();
            }
        }

        private void SaveSettings()
        {
            XElement root = new XElement("RhaViewSettings");
            if (indentSize != DefaultIndentSize)
            {
                root.Add(new XElement("IndentSize", indentSize.ToString(CultureInfo.InvariantCulture)));
            }
            root.Add(new XElement("RecentFiles", recentFiles.Select(path => new XElement("File", path))));

            try
            {
                new XDocument(root).Save(settingsPath);
            }
            catch (IOException ex)
            {
                StatusText.Text = "Lokale Einstellungen konnten nicht gespeichert werden: " + ex.Message;
            }
            catch (UnauthorizedAccessException ex)
            {
                StatusText.Text = "Kein Schreibzugriff auf den EXE-Ordner für lokale Einstellungen: " + ex.Message;
            }
            catch (SecurityException ex)
            {
                StatusText.Text = "Kein Schreibzugriff auf den EXE-Ordner für lokale Einstellungen: " + ex.Message;
            }
            catch (ArgumentException ex)
            {
                StatusText.Text = "Lokale Einstellungen konnten nicht gespeichert werden: " + ex.Message;
            }
        }

        private void RefreshView()
        {
            if (rawContent == null)
            {
                return;
            }

            if (currentExtension == ".md" || currentExtension == ".markdown")
            {
                if (MarkdownRenderedMenuItem.IsChecked)
                {
                    TextView.Visibility = Visibility.Collapsed;
                    MarkdownView.Visibility = Visibility.Visible;
                    MarkdownView.Document = RenderMarkdown(rawContent, fontSize);
                    StatusText.Text = "Markdown dargestellt";
                }
                else
                {
                    MarkdownView.Visibility = Visibility.Collapsed;
                    TextView.Visibility = Visibility.Visible;
                    TextView.Text = rawContent;
                    TextView.FontSize = fontSize;
                    StatusText.Text = "Markdown-Quelltext";
                }
                return;
            }

            MarkdownView.Visibility = Visibility.Collapsed;
            TextView.Visibility = Visibility.Visible;
            TextView.FontSize = fontSize;
            string displayText = rawContent;
            string status = currentExtension == ".json" ? "JSON" :
                            currentExtension == ".xml" ? "XML" : "Textdatei";

            if (PrettyPrintMenuItem.IsChecked && currentExtension == ".json")
            {
                try
                {
                    displayText = FormatJson(rawContent, indentSize);
                    status += " – formatiert";
                }
                catch (FormatException ex)
                {
                    status = "Ungültiges JSON – Originaltext wird angezeigt: " + ex.Message;
                }
            }
            else if (PrettyPrintMenuItem.IsChecked && currentExtension == ".xml")
            {
                try
                {
                    displayText = FormatXml(rawContent, indentSize);
                    status += " – formatiert";
                }
                catch (XmlException ex)
                {
                    status = "Ungültiges XML – Originaltext wird angezeigt: " + ex.Message;
                }
            }

            TextView.Text = displayText;
            StatusText.Text = status;
        }

        private string GetSaveContent()
        {
            if (currentExtension != ".json" && currentExtension != ".xml")
            {
                return rawContent;
            }
            return PrettyPrintMenuItem.IsChecked ? TextView.Text : rawContent;
        }

        private void SaveFile(bool saveAs)
        {
            if (rawContent == null)
            {
                StatusText.Text = "Keine Datei zum Speichern geöffnet.";
                return;
            }

            string path = currentPath;
            if (saveAs || string.IsNullOrEmpty(path))
            {
                SaveFileDialog dialog = new SaveFileDialog
                {
                    Filter = "Alle Dateien|*.*",
                    FileName = Path.GetFileName(currentPath)
                };
                if (dialog.ShowDialog(this) != true)
                {
                    return;
                }
                path = dialog.FileName;
            }
            else if (MessageBox.Show(this, "Die Datei mit der aktuell angezeigten Formatierung überschreiben?",
                                     "Speichern bestätigen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                WriteTextFile(path, GetSaveContent());
                currentPath = path;
                FilePathText.Text = path;
                FileNameText.Text = Path.GetFileName(path);
                StatusText.Text = "Gespeichert";
            }
            catch (IOException ex)
            {
                StatusText.Text = "Speichern fehlgeschlagen: " + ex.Message;
            }
            catch (UnauthorizedAccessException ex)
            {
                StatusText.Text = "Speichern fehlgeschlagen: " + ex.Message;
            }
            catch (SecurityException ex)
            {
                StatusText.Text = "Speichern fehlgeschlagen: " + ex.Message;
            }
            catch (EncoderFallbackException ex)
            {
                StatusText.Text = "Zeichen können mit der ursprünglichen Dateikodierung nicht gespeichert werden: " + ex.Message;
            }
            catch (ArgumentException ex)
            {
                StatusText.Text = "Ungültiger Dateipfad: " + ex.Message;
            }
        }

        private void WriteTextFile(string path, string content)
        {
            byte[] contentBytes = fileEncoding.GetBytes(content);
            byte[] preamble = fileHasPreamble ? fileEncoding.GetPreamble() : new byte[0];
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                if (preamble.Length > 0)
                {
                    stream.Write(preamble, 0, preamble.Length);
                }
                stream.Write(contentBytes, 0, contentBytes.Length);
            }
        }

        private void FindNext()
        {
            if (rawContent == null)
            {
                StatusText.Text = "Keine Datei zum Durchsuchen geöffnet.";
                return;
            }

            string query = SearchTextBox.Text;
            if (string.IsNullOrEmpty(query))
            {
                StatusText.Text = "Bitte einen Suchbegriff eingeben.";
                return;
            }

            if (!string.Equals(query, lastSearchQuery, StringComparison.Ordinal))
            {
                lastSearchQuery = query;
                searchStartIndex = 0;
            }

            string searchText = currentExtension == ".md" || currentExtension == ".markdown"
                ? rawContent
                : TextView.Text;
            try
            {
                RegexOptions options = MatchCaseCheckBox.IsChecked == true
                    ? RegexOptions.None
                    : RegexOptions.IgnoreCase;
                string pattern = RegexCheckBox.IsChecked == true ? query : Regex.Escape(query);
                Regex searchRegex = new Regex(pattern, options, TimeSpan.FromSeconds(2));
                Match match = searchRegex.Match(searchText, Math.Min(searchStartIndex, searchText.Length));
                if (!match.Success && searchStartIndex > 0)
                {
                    match = searchRegex.Match(searchText);
                }
                if (!match.Success)
                {
                    StatusText.Text = "Kein Treffer.";
                    return;
                }

                if (currentExtension == ".md" || currentExtension == ".markdown")
                {
                    MarkdownRenderedMenuItem.IsChecked = false;
                    RefreshView();
                }
                TextView.Focus();
                TextView.Select(match.Index, match.Length);
                TextView.ScrollToLine(TextView.GetLineIndexFromCharacterIndex(match.Index));
                searchStartIndex = match.Index + Math.Max(1, match.Length);
                StatusText.Text = "Treffer an Position " + (match.Index + 1).ToString(CultureInfo.InvariantCulture);
            }
            catch (ArgumentException ex)
            {
                StatusText.Text = "Ungültiger regulärer Ausdruck: " + ex.Message;
            }
            catch (RegexMatchTimeoutException)
            {
                StatusText.Text = "Die Suche mit regulärem Ausdruck hat das Zeitlimit überschritten.";
            }
        }

        private void ShowError(string path, string message)
        {
            currentPath = null;
            rawContent = null;
            fileEncoding = new UTF8Encoding(false, true);
            fileHasPreamble = false;
            MarkdownRenderedMenuItem.IsEnabled = false;
            MarkdownView.Visibility = Visibility.Collapsed;
            TextView.Visibility = Visibility.Visible;
            TextView.Text = message;
            FileNameText.Text = Path.GetFileName(path);
            FilePathText.Text = path;
            StatusText.Text = message;
        }

        private static string FormatJson(string json, int indentSize)
        {
            new JsonParser(json).Parse();

            StringBuilder output = new StringBuilder(json.Length + 32);
            bool inString = false;
            bool escaped = false;
            int indent = 0;

            for (int i = 0; i < json.Length; i++)
            {
                char current = json[i];
                if (inString)
                {
                    output.Append(current);
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (current == '\\')
                    {
                        escaped = true;
                    }
                    else if (current == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                    output.Append(current);
                }
                else if (current == '{' || current == '[')
                {
                    output.Append(current);
                    char closing = current == '{' ? '}' : ']';
                    int next = NextNonWhitespace(json, i + 1);
                    if (next < json.Length && json[next] != closing)
                    {
                        indent++;
                        AppendNewLine(output, indent, indentSize);
                    }
                }
                else if (current == '}' || current == ']')
                {
                    char opening = current == '}' ? '{' : '[';
                    int previous = PreviousNonWhitespace(json, i - 1);
                    if (previous >= 0 && json[previous] != opening)
                    {
                        indent--;
                        AppendNewLine(output, indent, indentSize);
                    }
                    output.Append(current);
                }
                else if (current == ',')
                {
                    output.Append(current);
                    AppendNewLine(output, indent, indentSize);
                }
                else if (current == ':')
                {
                    output.Append(": ");
                }
                else if (!char.IsWhiteSpace(current))
                {
                    output.Append(current);
                }
            }

            return output.ToString();
        }

        private static int NextNonWhitespace(string text, int start)
        {
            while (start < text.Length && char.IsWhiteSpace(text[start]))
            {
                start++;
            }
            return start;
        }

        private static int PreviousNonWhitespace(string text, int start)
        {
            while (start >= 0 && char.IsWhiteSpace(text[start]))
            {
                start--;
            }
            return start;
        }

        private static void AppendNewLine(StringBuilder output, int indent, int indentSize)
        {
            output.AppendLine();
            output.Append(' ', Math.Max(0, indent) * indentSize);
        }

        private static string FormatXml(string xml, int indentSize)
        {
            XDocument document = XDocument.Parse(xml);
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = new string(' ', indentSize),
                OmitXmlDeclaration = document.Declaration == null
            };

            using (StringWriter writer = new StringWriter(CultureInfo.InvariantCulture))
            using (XmlWriter xmlWriter = XmlWriter.Create(writer, settings))
            {
                document.Save(xmlWriter);
                xmlWriter.Flush();
                return writer.ToString();
            }
        }

        private static FlowDocument RenderMarkdown(string markdown, double fontSize)
        {
            FlowDocument document = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = fontSize,
                PagePadding = new Thickness(12)
            };
            string[] lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            bool inCodeBlock = false;
            StringBuilder code = new StringBuilder();
            List currentList = null;

            foreach (string line in lines)
            {
                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    if (inCodeBlock)
                    {
                        document.Blocks.Add(CreateCodeBlock(code.ToString()));
                        code.Clear();
                        inCodeBlock = false;
                    }
                    else
                    {
                        inCodeBlock = true;
                    }
                    currentList = null;
                    continue;
                }

                if (inCodeBlock)
                {
                    code.AppendLine(line);
                    continue;
                }

                string trimmed = line.Trim();
                if (trimmed.Length == 0)
                {
                    currentList = null;
                    continue;
                }

                int headingLevel = 0;
                while (headingLevel < trimmed.Length && headingLevel < 6 && trimmed[headingLevel] == '#')
                {
                    headingLevel++;
                }
                if (headingLevel > 0 && headingLevel < trimmed.Length && trimmed[headingLevel] == ' ')
                {
                    currentList = null;
                    Paragraph heading = new Paragraph
                    {
                        Margin = new Thickness(0, headingLevel == 1 ? 12 : 8, 0, 6),
                        FontSize = Math.Max(16, 28 - (headingLevel * 2)),
                        FontWeight = FontWeights.SemiBold
                    };
                    AddInlineMarkdown(heading.Inlines, trimmed.Substring(headingLevel + 1));
                    document.Blocks.Add(heading);
                    continue;
                }

                Match listMatch = Regex.Match(trimmed, @"^(?:[-*+]|\d+\.)\s+(.+)$");
                if (listMatch.Success)
                {
                    if (currentList == null)
                    {
                        currentList = new List
                        {
                            MarkerStyle = Regex.IsMatch(trimmed, @"^\d+\.") ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                            Margin = new Thickness(12, 0, 0, 6)
                        };
                        document.Blocks.Add(currentList);
                    }
                    Paragraph item = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
                    AddInlineMarkdown(item.Inlines, listMatch.Groups[1].Value);
                    currentList.ListItems.Add(new ListItem(item));
                    continue;
                }

                currentList = null;
                Paragraph paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
                AddInlineMarkdown(paragraph.Inlines, trimmed);
                document.Blocks.Add(paragraph);
            }

            if (inCodeBlock)
            {
                document.Blocks.Add(CreateCodeBlock(code.ToString()));
            }

            return document;
        }

        private static Paragraph CreateCodeBlock(string code)
        {
            Paragraph paragraph = new Paragraph
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                Background = new SolidColorBrush(Color.FromRgb(241, 243, 246)),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 4, 0, 10)
            };
            paragraph.Inlines.Add(new Run(code));
            return paragraph;
        }

        private static void AddInlineMarkdown(InlineCollection inlines, string text)
        {
            int position = 0;
            foreach (Match match in MarkdownInlinePattern.Matches(text))
            {
                if (match.Index > position)
                {
                    inlines.Add(new Run(text.Substring(position, match.Index - position)));
                }

                string token = match.Value;
                if (token.StartsWith("**", StringComparison.Ordinal) || token.StartsWith("__", StringComparison.Ordinal))
                {
                    inlines.Add(new Bold(new Run(token.Substring(2, token.Length - 4))));
                }
                else if (token[0] == '`')
                {
                    inlines.Add(new Run(token.Substring(1, token.Length - 2))
                    {
                        FontFamily = new FontFamily("Consolas"),
                        Background = new SolidColorBrush(Color.FromRgb(241, 243, 246))
                    });
                }
                else
                {
                    inlines.Add(new Italic(new Run(token.Substring(1, token.Length - 2))));
                }
                position = match.Index + match.Length;
            }

            if (position < text.Length)
            {
                inlines.Add(new Run(text.Substring(position)));
            }
        }

        private sealed class JsonParser
        {
            private readonly string text;
            private int position;

            public JsonParser(string text)
            {
                this.text = text;
            }

            public void Parse()
            {
                SkipWhitespace();
                ParseValue(0);
                SkipWhitespace();
                if (position != text.Length)
                {
                    Fail("Unerwarteter Inhalt nach dem JSON-Wert.");
                }
            }

            private void ParseValue(int depth)
            {
                if (depth > 256)
                {
                    Fail("Die JSON-Verschachtelung ist zu tief.");
                }
                if (position >= text.Length)
                {
                    Fail("Unerwartetes Dateiende.");
                }

                switch (text[position])
                {
                    case '{':
                        ParseObject(depth + 1);
                        break;
                    case '[':
                        ParseArray(depth + 1);
                        break;
                    case '"':
                        ParseString();
                        break;
                    case 't':
                        ParseLiteral("true");
                        break;
                    case 'f':
                        ParseLiteral("false");
                        break;
                    case 'n':
                        ParseLiteral("null");
                        break;
                    default:
                        ParseNumber();
                        break;
                }
            }

            private void ParseObject(int depth)
            {
                position++;
                SkipWhitespace();
                if (Consume('}'))
                {
                    return;
                }

                while (true)
                {
                    if (position >= text.Length || text[position] != '"')
                    {
                        Fail("Ein Objekt-Schlüssel muss eine Zeichenkette sein.");
                    }
                    ParseString();
                    SkipWhitespace();
                    Expect(':');
                    SkipWhitespace();
                    ParseValue(depth);
                    SkipWhitespace();
                    if (Consume('}'))
                    {
                        return;
                    }
                    Expect(',');
                    SkipWhitespace();
                }
            }

            private void ParseArray(int depth)
            {
                position++;
                SkipWhitespace();
                if (Consume(']'))
                {
                    return;
                }

                while (true)
                {
                    ParseValue(depth);
                    SkipWhitespace();
                    if (Consume(']'))
                    {
                        return;
                    }
                    Expect(',');
                    SkipWhitespace();
                }
            }

            private void ParseString()
            {
                position++;
                while (position < text.Length)
                {
                    char current = text[position++];
                    if (current == '"')
                    {
                        return;
                    }
                    if (current < 0x20)
                    {
                        Fail("Eine Zeichenkette enthält ein ungültiges Steuerzeichen.");
                    }
                    if (current != '\\')
                    {
                        continue;
                    }
                    if (position >= text.Length)
                    {
                        Fail("Unvollständige Escape-Sequenz.");
                    }

                    char escape = text[position++];
                    if (escape == 'u')
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (position >= text.Length || !Uri.IsHexDigit(text[position++]))
                            {
                                Fail("Ungültige Unicode-Escape-Sequenz.");
                            }
                        }
                    }
                    else if (escape != '"' && escape != '\\' && escape != '/' &&
                             escape != 'b' && escape != 'f' && escape != 'n' &&
                             escape != 'r' && escape != 't')
                    {
                        Fail("Ungültige Escape-Sequenz.");
                    }
                }
                Fail("Nicht abgeschlossene Zeichenkette.");
            }

            private void ParseNumber()
            {
                int start = position;
                Consume('-');
                if (Consume('0'))
                {
                    if (position < text.Length && IsDigit(text[position]))
                    {
                        Fail("Eine Zahl darf nicht mit zusätzlichen Ziffern auf null beginnen.");
                    }
                }
                else
                {
                    RequireDigit();
                    while (position < text.Length && IsDigit(text[position]))
                    {
                        position++;
                    }
                }

                if (Consume('.'))
                {
                    RequireDigit();
                    while (position < text.Length && IsDigit(text[position]))
                    {
                        position++;
                    }
                }

                if (Consume('e') || Consume('E'))
                {
                    if (!Consume('+'))
                    {
                        Consume('-');
                    }
                    RequireDigit();
                    while (position < text.Length && IsDigit(text[position]))
                    {
                        position++;
                    }
                }

                if (position == start)
                {
                    Fail("Ungültiger JSON-Wert.");
                }
            }

            private void ParseLiteral(string literal)
            {
                for (int i = 0; i < literal.Length; i++)
                {
                    if (position >= text.Length || text[position++] != literal[i])
                    {
                        Fail("Ungültiger JSON-Wert.");
                    }
                }
            }

            private void RequireDigit()
            {
                if (position >= text.Length || !IsDigit(text[position]))
                {
                    Fail("Eine Ziffer wurde erwartet.");
                }
                position++;
            }

            private bool IsDigit(char value)
            {
                return value >= '0' && value <= '9';
            }

            private void SkipWhitespace()
            {
                while (position < text.Length &&
                       (text[position] == ' ' || text[position] == '\t' ||
                        text[position] == '\r' || text[position] == '\n'))
                {
                    position++;
                }
            }

            private bool Consume(char expected)
            {
                if (position < text.Length && text[position] == expected)
                {
                    position++;
                    return true;
                }
                return false;
            }

            private void Expect(char expected)
            {
                if (!Consume(expected))
                {
                    Fail("'" + expected + "' wurde erwartet.");
                }
            }

            private void Fail(string message)
            {
                throw new FormatException(message + " Position: " + position.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }
    }
}
