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
        private const string MitLicenseText =
            "MIT License\n\n" +
            "Copyright (c) 2026 RhaView 1.0\n\n" +
            "Permission is hereby granted, free of charge, to any person obtaining a copy " +
            "of this software and associated documentation files (the \"Software\"), to deal " +
            "in the Software without restriction, including without limitation the rights " +
            "to use, copy, modify, merge, publish, distribute, sublicense, and/or sell " +
            "copies of the Software, and to permit persons to whom the Software is " +
            "furnished to do so, subject to the following conditions:\n\n" +
            "The above copyright notice and this permission notice shall be included in all " +
            "copies or substantial portions of the Software.\n\n" +
            "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR " +
            "IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, " +
            "FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE " +
            "AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER " +
            "LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, " +
            "OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.";
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
        private long currentFileSize = -1;
        private string savedEditorContent;
        private bool isDirty;
        private bool isRefreshingView;
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
            MessageBox.Show(this, "RhaView 1.0\nDateibetrachter für JSON, XML, Markdown und Text.\n\nCopyright (c) 2026 RhaView 1.0\nMIT-Lizenz",
                            "Über RhaView", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LicenseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Window licenseWindow = new Window
            {
                Title = "RhaView – MIT-Lizenz",
                Owner = this,
                Width = 760,
                Height = 560,
                MinWidth = 500,
                MinHeight = 350,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new TextBox
                {
                    Text = MitLicenseText,
                    IsReadOnly = true,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    Padding = new Thickness(12)
                }
            };
            licenseWindow.ShowDialog();
        }

        private void KeyboardHelpMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this,
                "Datei öffnen: Strg+O\nSpeichern: Strg+S\nSpeichern unter: Strg+Umschalt+S\n" +
                "Rückgängig: Strg+Z\nWiederholen: Strg+Y\nSuche ein-/ausblenden: Strg+F\n" +
                "Nächster Treffer: F3\nVorheriger Treffer: Umschalt+F3\n" +
                "Ausschneiden/Kopieren/Einfügen/Alles auswählen: Strg+X / Strg+C / Strg+V / Strg+A",
                "RhaView – Tastenkürzel", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UndoMenuItem_Click(object sender, RoutedEventArgs e)
        {
            PrepareTextEditor();
            if (TextView.CanUndo)
            {
                TextView.Undo();
            }
        }

        private void RedoMenuItem_Click(object sender, RoutedEventArgs e)
        {
            PrepareTextEditor();
            if (TextView.CanRedo)
            {
                TextView.Redo();
            }
        }

        private void CutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            PrepareTextEditor();
            TextView.Cut();
        }

        private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            PrepareTextEditor();
            TextView.Copy();
        }

        private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            PrepareTextEditor();
            TextView.Paste();
        }

        private void SelectAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            PrepareTextEditor();
            TextView.SelectAll();
        }

        private void PrepareTextEditor()
        {
            TextView.Focus();
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
            FindNext(false);
        }

        private void FindPreviousButton_Click(object sender, RoutedEventArgs e)
        {
            FindNext(true);
        }

        private void SearchTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                FindNext(System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift));
                e.Handled = true;
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            lastSearchQuery = null;
            searchStartIndex = 0;
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
            else if (KeyboardShortcut(e, System.Windows.Input.Key.Z, false))
            {
                UndoMenuItem_Click(sender, e);
            }
            else if (KeyboardShortcut(e, System.Windows.Input.Key.Y, false))
            {
                RedoMenuItem_Click(sender, e);
            }
            else if (e.Key == System.Windows.Input.Key.F3 &&
                     (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == 0)
            {
                FindNext(System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift));
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

        private void TextView_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isRefreshingView || currentPath == null)
            {
                return;
            }

            rawContent = TextView.Text;
            isDirty = !string.Equals(TextView.Text, savedEditorContent, StringComparison.Ordinal);
            if ((currentExtension == ".md" || currentExtension == ".markdown") &&
                MarkdownRenderedMenuItem.IsChecked)
            {
                MarkdownView.Document = RenderMarkdown(rawContent, fontSize);
            }
            UpdateCurrentFileSize();
            UpdateEditCommandState();
            UpdateStatusBar();
        }

        private void TextView_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (!isRefreshingView)
            {
                UpdateStatusBar();
                UpdateEditCommandState();
            }
        }

        private void UpdateCurrentFileSize()
        {
            try
            {
                currentFileSize = fileEncoding.GetByteCount(TextView.Text) +
                                  (fileHasPreamble ? fileEncoding.GetPreamble().LongLength : 0);
            }
            catch (EncoderFallbackException)
            {
                currentFileSize = -1;
            }
        }

        private void UpdateStatusBar()
        {
            if (FileNameStatusText == null)
            {
                return;
            }
            if (currentPath == null)
            {
                FileNameStatusText.Text = string.Empty;
                FileSizeText.Text = string.Empty;
                PositionText.Text = string.Empty;
                ModifiedText.Text = string.Empty;
                return;
            }

            FileNameStatusText.Text = Path.GetFileName(currentPath);
            FileSizeText.Text = FormatFileSize(currentFileSize);
            if (TextView.Visibility == Visibility.Visible)
            {
                int line = TextView.GetLineIndexFromCharacterIndex(TextView.CaretIndex);
                int lineStart = TextView.GetCharacterIndexFromLineIndex(Math.Max(0, line));
                PositionText.Text = "Zeile " + (line + 1).ToString(CultureInfo.InvariantCulture) +
                                    ", Spalte " + (TextView.CaretIndex - lineStart + 1).ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                PositionText.Text = "Vorschau";
            }
            ModifiedText.Text = isDirty ? "Geändert" : "Unverändert";
            Title = (isDirty ? "* " : string.Empty) + Path.GetFileName(currentPath) + " – RhaView";
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 0)
            {
                return "Größe unbekannt";
            }
            if (bytes < 1024)
            {
                return "Größe " + bytes.ToString("N0", CultureInfo.CurrentCulture) + " B";
            }

            string[] units = { "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unitIndex = -1;
            do
            {
                size /= 1024;
                unitIndex++;
            }
            while (size >= 1024 && unitIndex < units.Length - 1);
            return "Größe " + size.ToString("N1", CultureInfo.CurrentCulture) + " " + units[unitIndex];
        }

        private void UpdateEditCommandState()
        {
            if (TextView == null || UndoMenuItem == null)
            {
                return;
            }
            bool hasFile = currentPath != null;
            UndoMenuItem.IsEnabled = hasFile && TextView.CanUndo;
            RedoMenuItem.IsEnabled = hasFile && TextView.CanRedo;
            UndoButton.IsEnabled = hasFile && TextView.CanUndo;
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
            string content = fileEncoding.GetString(bytes, preambleLength, bytes.Length - preambleLength);
            currentFileSize = bytes.LongLength;
            return content;
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
            savedEditorContent = null;
            isDirty = false;
            currentExtension = Path.GetExtension(path).ToLowerInvariant();
            MarkdownRenderedMenuItem.IsEnabled = currentExtension == ".md" || currentExtension == ".markdown";
            MarkdownRenderedMenuItem.IsChecked = MarkdownRenderedMenuItem.IsEnabled;
            lastSearchQuery = null;
            searchStartIndex = 0;
            FileNameText.Text = Path.GetFileName(path);
            FilePathText.Text = path;
            Title = Path.GetFileName(path) + " – RhaView";
            RefreshView();
            savedEditorContent = TextView.Text;
            UpdateStatusBar();
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

            bool wasDirty = isDirty;
            isRefreshingView = true;
            try
            {
                TextView.FontSize = fontSize;
                if (currentExtension == ".md" || currentExtension == ".markdown")
                {
                    if (!string.Equals(TextView.Text, rawContent, StringComparison.Ordinal))
                    {
                        TextView.Text = rawContent;
                    }
                    if (MarkdownRenderedMenuItem.IsChecked)
                    {
                        Grid.SetColumnSpan(TextView, 1);
                        TextView.Visibility = Visibility.Visible;
                        MarkdownView.Visibility = Visibility.Visible;
                        MarkdownView.Document = RenderMarkdown(rawContent, fontSize);
                        StatusText.Text = "Markdown-Vorschau";
                    }
                    else
                    {
                        Grid.SetColumnSpan(TextView, 2);
                        MarkdownView.Visibility = Visibility.Collapsed;
                        TextView.Visibility = Visibility.Visible;
                        StatusText.Text = "Markdown-Quelltext";
                    }
                    return;
                }

                MarkdownView.Visibility = Visibility.Collapsed;
                TextView.Visibility = Visibility.Visible;
                Grid.SetColumnSpan(TextView, 2);
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
            finally
            {
                isRefreshingView = false;
                if (!wasDirty)
                {
                    savedEditorContent = TextView.Text;
                }
                UpdateStatusBar();
                UpdateEditCommandState();
            }
        }

        private string GetSaveContent()
        {
            return TextView.Text;
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
                    FileName = Path.GetFileName(currentPath),
                    InitialDirectory = currentPath == null ? AppContext.BaseDirectory : Path.GetDirectoryName(currentPath)
                };
                if (dialog.ShowDialog(this) != true)
                {
                    return;
                }
                path = dialog.FileName;
            }

            try
            {
                string content = GetSaveContent();
                WriteTextFile(path, content);
                rawContent = content;
                savedEditorContent = content;
                isDirty = false;
                currentPath = path;
                currentExtension = Path.GetExtension(path).ToLowerInvariant();
                MarkdownRenderedMenuItem.IsEnabled = currentExtension == ".md" || currentExtension == ".markdown";
                FilePathText.Text = path;
                FileNameText.Text = Path.GetFileName(path);
                currentFileSize = new FileInfo(path).Length;
                Title = Path.GetFileName(path) + " – RhaView";
                if (!MarkdownRenderedMenuItem.IsEnabled)
                {
                    MarkdownView.Visibility = Visibility.Collapsed;
                    TextView.Visibility = Visibility.Visible;
                    Grid.SetColumnSpan(TextView, 2);
                }
                else if (MarkdownRenderedMenuItem.IsChecked)
                {
                    TextView.Visibility = Visibility.Visible;
                    Grid.SetColumnSpan(TextView, 1);
                    MarkdownView.Visibility = Visibility.Visible;
                    MarkdownView.Document = RenderMarkdown(content, fontSize);
                }
                else
                {
                    MarkdownView.Visibility = Visibility.Collapsed;
                    TextView.Visibility = Visibility.Visible;
                    Grid.SetColumnSpan(TextView, 2);
                }
                AddRecentFile(path);
                StatusText.Text = "Gespeichert";
                UpdateStatusBar();
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

        private void FindNext(bool reverse)
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
                searchStartIndex = reverse ? searchTextLength() : 0;
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
                Match match = Match.Empty;
                if (reverse)
                {
                    MatchCollection matches = searchRegex.Matches(searchText);
                    for (int i = 0; i < matches.Count; i++)
                    {
                        if (matches[i].Index < searchStartIndex)
                        {
                            match = matches[i];
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (!match.Success && matches.Count > 0)
                    {
                        match = matches[matches.Count - 1];
                    }
                }
                else
                {
                    match = searchRegex.Match(searchText, Math.Min(searchStartIndex, searchText.Length));
                    if (!match.Success && searchStartIndex > 0)
                    {
                        match = searchRegex.Match(searchText);
                    }
                }
                if (!match.Success)
                {
                    StatusText.Text = "Kein Treffer.";
                    return;
                }

                TextView.Focus();
                TextView.Select(match.Index, match.Length);
                TextView.ScrollToLine(TextView.GetLineIndexFromCharacterIndex(match.Index));
                searchStartIndex = reverse
                    ? match.Index - 1
                    : match.Index + Math.Max(1, match.Length);
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

        private int searchTextLength()
        {
            return (currentExtension == ".md" || currentExtension == ".markdown")
                ? (rawContent == null ? 0 : rawContent.Length)
                : TextView.Text.Length;
        }

        private void ShowError(string path, string message)
        {
            currentPath = null;
            rawContent = null;
            savedEditorContent = null;
            isDirty = false;
            currentFileSize = -1;
            fileEncoding = new UTF8Encoding(false, true);
            fileHasPreamble = false;
            MarkdownRenderedMenuItem.IsEnabled = false;
            MarkdownView.Visibility = Visibility.Collapsed;
            TextView.Visibility = Visibility.Visible;
            Grid.SetColumnSpan(TextView, 2);
            TextView.Text = message;
            FileNameText.Text = Path.GetFileName(path);
            FilePathText.Text = path;
            StatusText.Text = message;
            UpdateStatusBar();
            UpdateEditCommandState();
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
