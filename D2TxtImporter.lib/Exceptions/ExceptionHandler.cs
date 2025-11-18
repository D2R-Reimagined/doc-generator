using System;
using System.Collections.Generic;
using System.IO;
using D2TxtImporter.lib.Model.Items;
using D2TxtImporter.lib.Model.Types;

namespace D2TxtImporter.lib.Exceptions
{
    public class ExceptionHandler
    {
        public static bool ContinueOnException { get; set; }

        private readonly static string ExceptionFile =
            $"{Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location)}/errorlog.txt";

        private readonly static string DebugFile =
            $"{Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location)}/debuglog.txt";

        public static List<string> ExceptionsWritten;

        public static void Initialize()
        {
            ExceptionsWritten = new List<string>();

            // Empty output files
            if (File.Exists(ExceptionFile))
            {
                File.Delete(ExceptionFile);
            }

            if (File.Exists(DebugFile))
            {
                File.Delete(DebugFile);
            }
        }

        public static void WriteException(Exception e)
        {
            var ex = e;

            var errorMessage = "";
            var debugMessage = "";
            do
            {
                errorMessage += $"Message:\n{ex.Message}\n\nStacktrace:\n{ex.StackTrace}\n";
                debugMessage += $"Message:\n{ex.Message}\n";

                if (ex is ItemPropertyException || ex is ItemStatCostException)
                {
                    errorMessage = $"Message:\n{ex.Message}\n\nStacktrace:\n{ex.StackTrace}\n";
                    debugMessage = $"Message:\n{ex.Message}\n";

                    break;
                }

                ex = ex.InnerException;
            } while (ex != null);

            if (!ExceptionsWritten.Contains(debugMessage))
            {
                ExceptionsWritten.Add(debugMessage);

                File.AppendAllText(ExceptionFile, errorMessage + "\n");
                File.AppendAllText(DebugFile, debugMessage + "\n");
            }

            if (!ContinueOnException)
            {
                throw e;
            }
        }

        public static void LogException(Exception e)
        {
            WriteException(e);

            if (!ContinueOnException)
            {
                throw e;
            }
        }
    }

    public class ItemStatCostException : Exception
    {
        private ItemStatCostException(string message) : base(message)
        {
        }

        public static ItemStatCostException Create(string message)
        {
            var resultMessage = "";

            resultMessage += $"Exception loading properties for item: '{Item.CurrentItem.Index}'\n";
            resultMessage +=
                $"\tCould not generate properties for property '{ItemProperty.CurrentItemProperty.Property.Code}' with parameter '{ItemProperty.CurrentItemProperty.Parameter}' min '{ItemProperty.CurrentItemProperty.Min}' max '{ItemProperty.CurrentItemProperty.Max}' index '{ItemProperty.CurrentItemProperty.Index}' itemlvl '{ItemProperty.CurrentItemProperty.ItemLevel}'\n";
            resultMessage += $"\t\t{message}";

            return new ItemStatCostException(resultMessage);
        }
    }

    public class ItemPropertyException : Exception
    {
        private ItemPropertyException(string message, Exception e) : base(message, e)
        {
        }

        public static ItemPropertyException Create(string message, Exception e = null)
        {
            var resultMessage = "";

            resultMessage += $"Exception loading properties for item: '{Item.CurrentItem.Index}'\n";
            resultMessage += $"\t{message}";

            return new ItemPropertyException(resultMessage, e);
        }
    }
}
