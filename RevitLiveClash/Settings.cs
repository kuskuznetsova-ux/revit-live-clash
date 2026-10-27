using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Autodesk.Revit.DB;

namespace RevitLiveClash
{
    [DataContract]
    public class CategoryPair
    {
        [DataMember(Name = "a")] public List<string> A { get; set; } = new List<string>();
        [DataMember(Name = "b")] public List<string> B { get; set; } = new List<string>();
    }

    /// <summary>
    /// Правила: какие категории с какими пересекать. Файл %AppData%\RevitLiveClash\settings.json.
    /// Имена категорий — BuiltInCategory (например "OST_Walls"). Пустой список пар = проверять всё со всем.
    /// </summary>
    [DataContract]
    public class Settings
    {
        [DataMember(Name = "pairs")] public List<CategoryPair> Pairs { get; set; } = new List<CategoryPair>();

        public static string FilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RevitLiveClash", "settings.json");

        public static Settings Current { get; private set; } = new Settings();

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) { Save(Example()); }
                using (var s = File.OpenRead(FilePath))
                    Current = (Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(s) ?? new Settings();
            }
            catch { Current = new Settings(); }
        }

        static void Save(Settings st)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            using (var s = File.Create(FilePath))
                new DataContractJsonSerializer(typeof(Settings)).WriteObject(s, st);
        }

        // Пустой набор пар: по умолчанию проверяем всё со всем. Пример лежит в README.
        static Settings Example() { return new Settings(); }

        static string Name(Element e)
        {
            return e.Category == null ? "" : ((BuiltInCategory)e.Category.Id.IntegerValue).ToString();
        }

        public bool Involves(Element e)
        {
            if (Pairs.Count == 0) return true;
            string n = Name(e);
            return Pairs.Any(p => p.A.Contains(n) || p.B.Contains(n));
        }

        public bool Allows(Element x, Element y)
        {
            if (Pairs.Count == 0) return true;
            string nx = Name(x), ny = Name(y);
            return Pairs.Any(p => (p.A.Contains(nx) && p.B.Contains(ny)) || (p.A.Contains(ny) && p.B.Contains(nx)));
        }
    }
}
