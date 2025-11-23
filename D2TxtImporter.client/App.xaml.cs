using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

using D2TxtImporter.lib.Exceptions;

namespace D2TxtImporter.client
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Global exception routing so anything that slips past the UI flow still shows a pop-up
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try { ExceptionHandler.LogException(e.Exception); } catch { /* ignore */ }
            ShowErrorFromLogsOrException(e.Exception);
            // Mark handled so the app can continue where safe
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception ?? new Exception($"Unhandled exception: {e.ExceptionObject}");
            try { ExceptionHandler.LogException(ex); } catch { /* ignore */ }

            // Marshal to UI thread if possible
            try
            {
                if (Current != null && Current.Dispatcher != null)
                {
                    Current.Dispatcher.BeginInvoke(new Action(() => ShowErrorFromLogsOrException(ex)));
                }
            }
            catch { /* ignore */ }
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            try { ExceptionHandler.LogException(e.Exception); } catch { /* ignore */ }
            e.SetObserved();
            try
            {
                if (Current != null && Current.Dispatcher != null)
                {
                    Current.Dispatcher.BeginInvoke(new Action(() => ShowErrorFromLogsOrException(e.Exception)));
                }
            }
            catch { /* ignore */ }
        }

        private static void ShowErrorFromLogsOrException(Exception ex)
        {
            try
            {
                var debugFile = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) ?? string.Empty, "debuglog.txt");
                string message = null;
                if (System.IO.File.Exists(debugFile))
                {
                    message = System.IO.File.ReadAllText(debugFile);
                }

                if (string.IsNullOrWhiteSpace(message))
                {
                    message = ex?.ToString() ?? "Unknown error";
                }

                var dlg = new ErrorDialog(message);
                dlg.Show();
            }
            catch
            {
                MessageBox.Show(ex?.ToString() ?? "Unknown error", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
