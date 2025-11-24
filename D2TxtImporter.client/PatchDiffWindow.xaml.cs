using System;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace D2TxtImporter.client
{
    public partial class PatchDiffWindow : Window
    {
        private string _lastMarkdown;

        public PatchDiffWindow()
        {
            InitializeComponent();
            // Load sticky values from user settings
            try
            {
                OldJsonText.Text = Properties.Settings.Default.PatchOldJsonPath;
                NewJsonText.Text = Properties.Settings.Default.PatchNewJsonPath;
                PatchFileText.Text = Properties.Settings.Default.PatchDiffFilePath;
            }
            catch { /* ignore load errors */ }

            // Ensure we persist latest values on close
            this.Closing += PatchDiffWindow_Closing;
        }

        private void PatchDiffWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                Properties.Settings.Default.PatchOldJsonPath = OldJsonText.Text?.Trim();
                Properties.Settings.Default.PatchNewJsonPath = NewJsonText.Text?.Trim();
                Properties.Settings.Default.PatchDiffFilePath = PatchFileText.Text?.Trim();
                Properties.Settings.Default.Save();
            }
            catch { /* ignore save errors during closing */ }
        }

        private void BrowseOldJson(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog { IsFolderPicker = true };
            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                OldJsonText.Text = dlg.FileName;
                try
                {
                    Properties.Settings.Default.PatchOldJsonPath = OldJsonText.Text;
                    Properties.Settings.Default.Save();
                }
                catch { /* ignore */ }
            }
        }

        private void BrowseNewJson(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog { IsFolderPicker = true };
            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                NewJsonText.Text = dlg.FileName;
                try
                {
                    Properties.Settings.Default.PatchNewJsonPath = NewJsonText.Text;
                    Properties.Settings.Default.Save();
                }
                catch { /* ignore */ }
            }
        }

        private void BrowsePatch(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog { IsFolderPicker = false };
            dlg.Filters.Add(new CommonFileDialogFilter("Patch files", "*.patch;*.diff;*.txt"));
            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                PatchFileText.Text = dlg.FileName;
                try
                {
                    Properties.Settings.Default.PatchDiffFilePath = PatchFileText.Text;
                    Properties.Settings.Default.Save();
                }
                catch { /* ignore */ }
            }
        }

        private void RunDiff(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Running...";
                OutputText.Text = string.Empty;
                SaveButton.IsEnabled = false;

                var oldDir = OldJsonText.Text?.Trim();
                var newDir = NewJsonText.Text?.Trim();
                var patch = PatchFileText.Text?.Trim();

                // Persist current values so choices are sticky between runs
                try
                {
                    Properties.Settings.Default.PatchOldJsonPath = oldDir;
                    Properties.Settings.Default.PatchNewJsonPath = newDir;
                    Properties.Settings.Default.PatchDiffFilePath = patch;
                    Properties.Settings.Default.Save();
                }
                catch { /* ignore */ }

                if (string.IsNullOrWhiteSpace(oldDir) || string.IsNullOrWhiteSpace(newDir))
                {
                    MessageBox.Show(this, "Please select both old and new JSON directories.", "Missing input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (!Directory.Exists(oldDir)) { MessageBox.Show(this, "Old JSON directory does not exist.", "Invalid path", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                if (!Directory.Exists(newDir)) { MessageBox.Show(this, "New JSON directory does not exist.", "Invalid path", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

                var models = D2TxtImporter.lib.Diff.JsonModelLoader.Load(oldDir, newDir);

                D2TxtImporter.lib.Diff.DiffFromPatchService.Targets targets = null;
                bool hasPatch = !string.IsNullOrWhiteSpace(patch) && File.Exists(patch);
                if (hasPatch)
                {
                    targets = D2TxtImporter.lib.Diff.DiffFromPatchService.ParseUnifiedDiff(patch);
                }
                else
                {
                    targets = new D2TxtImporter.lib.Diff.DiffFromPatchService.Targets();
                }

                bool includeUnmentioned = IncludeUnmentionedCheck.IsChecked == true;
                bool autoIncludedAll = false;
                // Auto-include all when no .diff is provided or parsed targets are empty across all categories
                if (!hasPatch ||
                    ((targets.UniqueKeys == null || targets.UniqueKeys.Count == 0)
                     && (targets.SetKeys == null || targets.SetKeys.Count == 0)
                     && (targets.RunewordKeys == null || targets.RunewordKeys.Count == 0)
                     && (targets.BaseCodes == null || targets.BaseCodes.Count == 0)
                     && (targets.CubeKeys == null || targets.CubeKeys.Count == 0)))
                {
                    if (!includeUnmentioned)
                    {
                        includeUnmentioned = true;
                        autoIncludedAll = true;
                    }
                }
                var diff = D2TxtImporter.lib.Diff.SemanticDiffService.Compute(models, targets, includeUnmentioned);
                var md = D2TxtImporter.lib.Diff.MarkdownPatchNotesExporter.Render(diff);
                _lastMarkdown = md;
                OutputText.Text = md;
                SaveButton.IsEnabled = true;
                StatusText.Text = autoIncludedAll
                    ? (hasPatch ? "Parsed .diff has no scoped keys; included all changes." : "No .diff provided; included all changes.")
                    : "Done";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error";
                var sb = new StringBuilder();
                sb.AppendLine("An error occurred while computing the diff:");
                sb.AppendLine(ex.Message);
                sb.AppendLine();
                sb.AppendLine(ex.ToString());
                OutputText.Text = sb.ToString();
            }
        }

        private void SaveMarkdown(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_lastMarkdown))
            {
                MessageBox.Show(this, "No markdown to save.", "Nothing to save", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Markdown (*.md)|*.md|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = "PatchNotes.md"
            };
            if (sfd.ShowDialog(this) == true)
            {
                File.WriteAllText(sfd.FileName, _lastMarkdown, new UTF8Encoding(false));
            }
        }
    }
}
