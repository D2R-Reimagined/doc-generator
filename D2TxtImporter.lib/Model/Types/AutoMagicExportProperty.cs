using System;

namespace D2TxtImporter.lib.Model.Types
{
    public class AutoMagicExportProperty
    {
        // The raw name of the Automagic row this property originated from (from Automagic.txt)
        public string Name { get; set; }
        public string PropertyString { get; set; }
        public int Index { get; set; }
        public int Level { get; set; }
        public int RequiredLevel { get; set; }
    }
}
