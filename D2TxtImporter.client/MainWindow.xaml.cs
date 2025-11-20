using System;
using System.IO;
using System.Windows;

using Microsoft.WindowsAPICodePack.Dialogs;

namespace D2TxtImporter.client
{
    /// Interaction logic for MainWindow.xaml
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _mainViewModel;

        public MainWindow()
        {
            InitializeComponent();

            _mainViewModel = new MainViewModel();
            DataContext = _mainViewModel;

            _mainViewModel.ExcelPath = Properties.Settings.Default.ExcelPath;
            _mainViewModel.TablePath = Properties.Settings.Default.TablePath;
            _mainViewModel.OutputPath = Properties.Settings.Default.OutputPath;
            _mainViewModel.CubeRecipeUseDescription = Properties.Settings.Default.CubeRecipeUseDescription;
            // Load persisted checkbox settings with requested defaults (defined in Settings.settings)
            _mainViewModel.DuplicateKeyReportEnabled = Properties.Settings.Default.DuplicateKeyReportEnabled;
            _mainViewModel.RequiredLevelReportEnabled = Properties.Settings.Default.RequiredLevelReportEnabled;
            _mainViewModel.ExportJson = Properties.Settings.Default.ExportJson;
            _mainViewModel.ExportWeb = Properties.Settings.Default.ExportWeb;
            _mainViewModel.PrettyPrintJson = Properties.Settings.Default.PrettyPrintJson;
            _mainViewModel.ExportExcel = Properties.Settings.Default.ExportExcel;
        }

        private void BrowseExcel(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            CommonFileDialogResult result = dialog.ShowDialog();


            if (result == CommonFileDialogResult.Ok)
            {
                _mainViewModel.ExcelPath = dialog.FileName;
            }
        }

        private void BrowseTable(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            CommonFileDialogResult result = dialog.ShowDialog();

            if (result == CommonFileDialogResult.Ok)
            {
                _mainViewModel.TablePath = dialog.FileName;
            }
        }

        private void BrowseOutput(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            CommonFileDialogResult result = dialog.ShowDialog();

            if (result == CommonFileDialogResult.Ok)
            {
                _mainViewModel.OutputPath = dialog.FileName;
            }
        }

        private void LoadData(object sender, RoutedEventArgs e)
        {
            // Centered-button confirmation dialog before starting an import/export run
            var dialog = new ConfirmDialog(
                "This will import data and export files.\nExisting output files will be overwritten.\n\nDo you want to continue?",
                "Confirm Import");
            dialog.Owner = this;
            var confirm = dialog.ShowDialog() == true;

            if (!confirm)
            {
                // User chose Cancel — abort operation
                return;
            }

            try
            {
                // Update settings
                Properties.Settings.Default.ExcelPath = _mainViewModel.ExcelPath;
                Properties.Settings.Default.TablePath = _mainViewModel.TablePath;
                Properties.Settings.Default.OutputPath = _mainViewModel.OutputPath;
                Properties.Settings.Default.CubeRecipeUseDescription = _mainViewModel.CubeRecipeUseDescription;
                Properties.Settings.Default.DuplicateKeyReportEnabled = _mainViewModel.DuplicateKeyReportEnabled;
                Properties.Settings.Default.RequiredLevelReportEnabled = _mainViewModel.RequiredLevelReportEnabled;
                Properties.Settings.Default.ExportJson = _mainViewModel.ExportJson;
                Properties.Settings.Default.ExportWeb = _mainViewModel.ExportWeb;
                Properties.Settings.Default.PrettyPrintJson = _mainViewModel.PrettyPrintJson;
                Properties.Settings.Default.ExportExcel = _mainViewModel.ExportExcel;
                Properties.Settings.Default.Save();

                // Import data
                _mainViewModel.Importer = new lib.Importer(_mainViewModel.ExcelPath, _mainViewModel.TablePath, _mainViewModel.OutputPath)
                {
                    ExportJson = _mainViewModel.ExportJson,
                    ExportWeb = _mainViewModel.ExportWeb,
                    PrettyPrintJson = _mainViewModel.PrettyPrintJson,
                    ExportExcel = _mainViewModel.ExportExcel
                };
                _mainViewModel.Importer.LoadData();
                _mainViewModel.Importer.ImportModel();

                _mainViewModel.OnPropertyChange(nameof(_mainViewModel.ExportEnabled));

                // Temporary Export, should be moved to its own button at some point
                _mainViewModel.Importer.Export();
            }
            catch (Exception)
            {
            }

            var debugFile = $"{Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location)}/debuglog.txt";
            if (File.Exists(debugFile))
            {
                var errorLines = File.ReadAllText(debugFile);

                var errorDialog = new ErrorDialog(errorLines);
                errorDialog.Show();
            }
            else
            {
                MessageBox.Show("Export Successful!");
            }
        }

        private void ExportData(object sender, RoutedEventArgs e)
        {
            try
            {
                _mainViewModel.Importer.Export();
            }
            catch (Exception ex)
            {
                var errorDialog = new ErrorDialog(ex.Message + "\n\n" + ex.StackTrace);
                errorDialog.Show();
            }
        }
    }
}
