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

    // Grouped representation used for export in armors.json and weapons.json
    // Groups all properties that share the same Name, Level, and RequiredLevel
    public class AutoMagicExportPropertyGroup
    {
        public string Name { get; set; }
        public int Level { get; set; }
        public int RequiredLevel { get; set; }
        public System.Collections.Generic.List<string> PropertyStrings { get; set; }
    }
}
