using System.ComponentModel;
using System.IO;

using D2TxtImporter.lib.Exceptions;
using D2TxtImporter.lib.Model.Dictionaries;
using D2TxtImporter.lib.Model.Items;

namespace D2TxtImporter.client
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private lib.Importer _importer;
        private string _excelPath;
        private string _tablePath;
        private string _outputPath;
        private bool _exportJson = true;
        private bool _exportWeb = true;
        private bool _prettyPrintJson = true;
        private bool _exportExcel = false;
        private bool _exportSetsByBase = false;
        private bool _itemStatCostExportEnabled = false;
        private bool _cubeRecipesV2ExportEnabled = false;
        private bool _earlyStopSentinelEnabled = false;
        private bool _isBusy = false;
        private string _statusText = string.Empty;

        public lib.Importer Importer
        {
            get => _importer;
            set
            {
                _importer = value;
                OnPropertyChange(nameof(ExportEnabled));
            }
        }

        public string ExcelPath
        {
            get => _excelPath; set
            {
                _excelPath = value;
                OnPropertyChange(nameof(ExcelPath));
                OnPropertyChange(nameof(ImportEnabled));
            }
        }

        public string TablePath
        {
            get => _tablePath; set
            {
                _tablePath = value;
                OnPropertyChange(nameof(TablePath));
                OnPropertyChange(nameof(ImportEnabled));
            }
        }

        public string OutputPath
        {
            get => _outputPath; set
            {
                _outputPath = value;
                OnPropertyChange(nameof(OutputPath));
                OnPropertyChange(nameof(ImportEnabled));
            }
        }

        public bool CubeRecipeUseDescription
        {
            get
            {
                return CubeRecipeV2.UseDescription;
            }
            set
            {
                CubeRecipeV2.UseDescription = value;
            }
        }

        public bool ContinueOnException
        {
            get
            {
                return ExceptionHandler.ContinueOnException;
            }
            set
            {
                ExceptionHandler.ContinueOnException = value;
            }
        }

        public bool DuplicateKeyReportEnabled
        {
            get => Table.EnableDuplicateReport;
            set
            {
                Table.EnableDuplicateReport = value;
                OnPropertyChange(nameof(DuplicateKeyReportEnabled));
            }
        }

        public bool RequiredLevelReportEnabled
        {
            get => RequiredLevelReport.Enabled;
            set
            {
                RequiredLevelReport.Enabled = value;
                OnPropertyChange(nameof(RequiredLevelReportEnabled));
            }
        }

        public bool ItemStatCostExportEnabled
        {
            get => _itemStatCostExportEnabled;
            set
            {
                _itemStatCostExportEnabled = value;
                OnPropertyChange(nameof(ItemStatCostExportEnabled));
            }
        }

        public bool CubeRecipesV2ExportEnabled
        {
            get => _cubeRecipesV2ExportEnabled;
            set
            {
                _cubeRecipesV2ExportEnabled = value;
                OnPropertyChange(nameof(CubeRecipesV2ExportEnabled));
            }
        }

        public bool EarlyStopSentinelEnabled
        {
            get => _earlyStopSentinelEnabled;
            set
            {
                _earlyStopSentinelEnabled = value;
                OnPropertyChange(nameof(EarlyStopSentinelEnabled));
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChange(nameof(IsBusy));
                // Also notify ImportEnabled since it's computed and depends on IsBusy
                OnPropertyChange(nameof(ImportEnabled));
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText == value) return;
                _statusText = value;
                OnPropertyChange(nameof(StatusText));
            }
        }

        public bool ExportJson
        {
            get => _exportJson;
            set
            {
                _exportJson = value;
                OnPropertyChange(nameof(ExportJson));
            }
        }

        public bool ExportWeb
        {
            get => _exportWeb;
            set
            {
                _exportWeb = value;
                OnPropertyChange(nameof(ExportWeb));
            }
        }

        public bool ExportExcel
        {
            get => _exportExcel;
            set
            {
                _exportExcel = value;
                OnPropertyChange(nameof(ExportExcel));
            }
        }

        public bool PrettyPrintJson
        {
            get => _prettyPrintJson;
            set
            {
                _prettyPrintJson = value;
                OnPropertyChange(nameof(PrettyPrintJson));
            }
        }

        public bool ExportSetsByBase
        {
            get => _exportSetsByBase;
            set
            {
                _exportSetsByBase = value;
                OnPropertyChange(nameof(ExportSetsByBase));
            }
        }

        public bool ExportEnabled => Importer != null && Importer.Runewords != null && Importer.Uniques != null && Importer.Sets != null;
        public bool ImportEnabled => !IsBusy && Directory.Exists(ExcelPath) && Directory.Exists(TablePath) && Directory.Exists(OutputPath);

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChange(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
