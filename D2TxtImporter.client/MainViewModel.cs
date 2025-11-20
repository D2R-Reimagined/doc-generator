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
                return CubeRecipe.UseDescription;
            }
            set
            {
                CubeRecipe.UseDescription = value;
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

        public bool ExportEnabled => Importer != null && Importer.CubeRecipes != null && Importer.Runewords != null && Importer.Uniques != null;
        public bool ImportEnabled => Directory.Exists(ExcelPath) && Directory.Exists(TablePath) && Directory.Exists(OutputPath);

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
