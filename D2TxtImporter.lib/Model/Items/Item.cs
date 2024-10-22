using System;
using System.Collections.Generic;
using D2TxtImporter.lib.Exceptions;
using D2TxtImporter.lib.Model.Dictionaries;
using D2TxtImporter.lib.Model.Types;
using Newtonsoft.Json;

namespace D2TxtImporter.lib.Model.Items
{
    public class Item
    {
        [JsonIgnore]
        public static Item CurrentItem { get; set; }

        public string Name
        {
            get
            {
                if (!Table.Tables.ContainsKey(Index))
                {
                    ExceptionHandler.LogException(new Exception($"Could not find translation for '{Index}' in any .tbl files"));
                }

                return Table.Tables[Index];
            }
        }
        
        public string Index { get; set; }
        public bool Enabled { get; set; }
        public int ItemLevel { get; set; }
        public int RequiredLevel { get; set; }
        public string Code { get; set; }
        public List<ItemProperty> Properties { get; set; }
        public bool DamageArmorEnhanced { get; set; }
        public Equipment.Equipment Equipment { get; set; }

        public Item()
        {
            CurrentItem = this;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
