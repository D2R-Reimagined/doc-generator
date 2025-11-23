using System;
using System.IO;
using System.Windows;
using System.Threading.Tasks;

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
            _mainViewModel.ItemStatCostExportEnabled = Properties.Settings.Default.ItemStatCostExportEnabled;
            _mainViewModel.CubeRecipesV2ExportEnabled = Properties.Settings.Default.CubeRecipesV2ExportEnabled;
            _mainViewModel.EarlyStopSentinelEnabled = Properties.Settings.Default.EarlyStopSentinelEnabled;
            _mainViewModel.ContinueOnException = Properties.Settings.Default.ContinueOnException;
            _mainViewModel.ExportJson = Properties.Settings.Default.ExportJson;
            _mainViewModel.ExportWeb = Properties.Settings.Default.ExportWeb;
            _mainViewModel.PrettyPrintJson = Properties.Settings.Default.PrettyPrintJson;
            _mainViewModel.ExportExcel = Properties.Settings.Default.ExportExcel;

            // Ensure settings are persisted even if user closes the window without running
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // Persist current view model settings to user settings
                Properties.Settings.Default.ExcelPath = _mainViewModel.ExcelPath;
                Properties.Settings.Default.TablePath = _mainViewModel.TablePath;
                Properties.Settings.Default.OutputPath = _mainViewModel.OutputPath;
                Properties.Settings.Default.CubeRecipeUseDescription = _mainViewModel.CubeRecipeUseDescription;
                Properties.Settings.Default.DuplicateKeyReportEnabled = _mainViewModel.DuplicateKeyReportEnabled;
                Properties.Settings.Default.RequiredLevelReportEnabled = _mainViewModel.RequiredLevelReportEnabled;
                Properties.Settings.Default.ItemStatCostExportEnabled = _mainViewModel.ItemStatCostExportEnabled;
                Properties.Settings.Default.CubeRecipesV2ExportEnabled = _mainViewModel.CubeRecipesV2ExportEnabled;
                Properties.Settings.Default.EarlyStopSentinelEnabled = _mainViewModel.EarlyStopSentinelEnabled;
                Properties.Settings.Default.ContinueOnException = _mainViewModel.ContinueOnException;
                Properties.Settings.Default.ExportJson = _mainViewModel.ExportJson;
                Properties.Settings.Default.ExportWeb = _mainViewModel.ExportWeb;
                Properties.Settings.Default.PrettyPrintJson = _mainViewModel.PrettyPrintJson;
                Properties.Settings.Default.ExportExcel = _mainViewModel.ExportExcel;
                Properties.Settings.Default.Save();
            }
            catch { /* ignore save errors during closing */ }
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

        private async void LoadData(object sender, RoutedEventArgs e)
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
                _mainViewModel.IsBusy = true;
                _mainViewModel.StatusText = "Saving settings...";

                // Update settings on UI thread
                Properties.Settings.Default.ExcelPath = _mainViewModel.ExcelPath;
                Properties.Settings.Default.TablePath = _mainViewModel.TablePath;
                Properties.Settings.Default.OutputPath = _mainViewModel.OutputPath;
                Properties.Settings.Default.CubeRecipeUseDescription = _mainViewModel.CubeRecipeUseDescription;
                Properties.Settings.Default.DuplicateKeyReportEnabled = _mainViewModel.DuplicateKeyReportEnabled;
                Properties.Settings.Default.RequiredLevelReportEnabled = _mainViewModel.RequiredLevelReportEnabled;
                Properties.Settings.Default.ItemStatCostExportEnabled = _mainViewModel.ItemStatCostExportEnabled;
                Properties.Settings.Default.CubeRecipesV2ExportEnabled = _mainViewModel.CubeRecipesV2ExportEnabled;
                Properties.Settings.Default.EarlyStopSentinelEnabled = _mainViewModel.EarlyStopSentinelEnabled;
                Properties.Settings.Default.ContinueOnException = _mainViewModel.ContinueOnException;
                Properties.Settings.Default.ExportJson = _mainViewModel.ExportJson;
                Properties.Settings.Default.ExportWeb = _mainViewModel.ExportWeb;
                Properties.Settings.Default.PrettyPrintJson = _mainViewModel.PrettyPrintJson;
                Properties.Settings.Default.ExportExcel = _mainViewModel.ExportExcel;
                Properties.Settings.Default.Save();

                // Create importer on UI thread (lightweight), then do heavy work in background
                var importer = new lib.Importer(_mainViewModel.ExcelPath, _mainViewModel.TablePath, _mainViewModel.OutputPath)
                {
                    ExportJson = _mainViewModel.ExportJson,
                    ExportWeb = _mainViewModel.ExportWeb,
                    PrettyPrintJson = _mainViewModel.PrettyPrintJson,
                    ExportExcel = _mainViewModel.ExportExcel,
                    ExportItemStatCostReport = _mainViewModel.ItemStatCostExportEnabled,
                    ExportCubeRecipesV2 = _mainViewModel.CubeRecipesV2ExportEnabled
                };

                // Apply Cube V2 early-stop sentinel toggle (static flag) before import
                D2TxtImporter.lib.Model.Items.CubeRecipeV2.EnableEarlyStopSentinel = _mainViewModel.EarlyStopSentinelEnabled;

                // Optionally expose importer to VM for later use
                _mainViewModel.Importer = importer;

                _mainViewModel.StatusText = "Loading tables and data...";
                await Task.Run(() => importer.LoadData());

                _mainViewModel.StatusText = "Importing model...";
                await Task.Run(() => importer.ImportModel());

                _mainViewModel.StatusText = "Exporting...";
                await Task.Run(() => importer.Export());

                _mainViewModel.OnPropertyChange(nameof(_mainViewModel.ExportEnabled));

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
            catch (Exception ex)
            {
                // Route any exception to our central handler and surface a pop-up
                try
                {
                    D2TxtImporter.lib.Exceptions.ExceptionHandler.WriteException(ex);
                }
                catch { /* ignore secondary errors while reporting */ }

                try
                {
                    var debugFile = $"{Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location)}/debuglog.txt";
                    string message = null;
                    if (File.Exists(debugFile))
                    {
                        message = File.ReadAllText(debugFile);
                    }

                    if (string.IsNullOrWhiteSpace(message))
                    {
                        // Fallback to the exception details when no debug log was produced
                        message = ex.ToString();
                    }

                    var errorDialog = new ErrorDialog(message);
                    errorDialog.Show();
                }
                catch
                {
                    // Last resort: message box so the user still sees something
                    MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                _mainViewModel.StatusText = string.Empty;
                _mainViewModel.IsBusy = false;
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
