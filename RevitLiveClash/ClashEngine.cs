using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RevitLiveClash
{
    /// <summary>Поиск пересечений элементов и подсветка их в виде.</summary>
    public static class ClashEngine
    {
        /// <summary>Пары коллизий: элемент -> элементы, с которыми он пересекается.</summary>
        public static readonly Dictionary<ElementId, HashSet<ElementId>> Pairs =
            new Dictionary<ElementId, HashSet<ElementId>>();

        /// <summary>Все элементы, которым мы выставили переопределение (чтобы можно было сбросить).</summary>
        public static readonly HashSet<ElementId> Highlighted = new HashSet<ElementId>();

        static readonly BuiltInCategory[] Ignored =
        {
            BuiltInCategory.OST_Lines, BuiltInCategory.OST_RvtLinks,
            BuiltInCategory.OST_Cameras, BuiltInCategory.OST_Levels,
            BuiltInCategory.OST_Grids, BuiltInCategory.OST_Mass,
        };

        public static bool IsCandidate(Element e)
        {
            Category c = e.Category;
            if (c == null || c.CategoryType != CategoryType.Model) return false;
            if (Ignored.Contains((BuiltInCategory)c.Id.IntegerValue)) return false;
            return e.get_BoundingBox(null) != null && Settings.Current.Involves(e);
        }

        /// <summary>Полный пересчёт коллизий для всех элементов вида.</summary>
        public static int CheckAll(Document doc, View view)
        {
            Pairs.Clear();
            var elements = new FilteredElementCollector(doc, view.Id)
                .WhereElementIsNotElementType().ToElements().Where(IsCandidate).ToList();
            foreach (Element e in elements) FindPairs(doc, view, e);
            return Pairs.Count;
        }

        /// <summary>Пересчёт для изменённых элементов; возвращает всех, чья подсветка могла измениться.</summary>
        public static HashSet<ElementId> Recheck(Document doc, View view, ICollection<ElementId> changed)
        {
            var affected = new HashSet<ElementId>(changed);
            foreach (ElementId id in changed)
            {
                if (Pairs.TryGetValue(id, out var old))
                {
                    foreach (ElementId p in old)
                    {
                        affected.Add(p);
                        if (Pairs.TryGetValue(p, out var back)) { back.Remove(id); if (back.Count == 0) Pairs.Remove(p); }
                    }
                    Pairs.Remove(id);
                }
            }
            foreach (ElementId id in changed)
            {
                Element e = doc.GetElement(id);
                if (e == null || !IsCandidate(e)) continue;
                foreach (ElementId p in FindPairs(doc, view, e)) affected.Add(p);
            }
            return affected;
        }

        static List<ElementId> FindPairs(Document doc, View view, Element a)
        {
            var found = new List<ElementId>();
            BoundingBoxXYZ bb = a.get_BoundingBox(null);
            if (bb == null) return found;
            var outline = new Outline(bb.Min, bb.Max);

            var hits = new FilteredElementCollector(doc, view.Id)
                .WhereElementIsNotElementType()
                .WherePasses(new BoundingBoxIntersectsFilter(outline))
                .Excluding(new List<ElementId> { a.Id })
                .WherePasses(new ElementIntersectsElementFilter(a))
                .ToElements();

            foreach (Element b in hits)
            {
                if (!IsCandidate(b) || !Settings.Current.Allows(a, b) || IsExpected(a, b)) continue;
                Add(a.Id, b.Id);
                Add(b.Id, a.Id);
                found.Add(b.Id);
            }
            return found;
        }

        /// <summary>Штатные пересечения: окно в стене, соединённые стены/балки и т.п.</summary>
        static bool IsExpected(Element a, Element b)
        {
            if (Relates(a, b) || Relates(b, a)) return true;
            try { return JoinGeometryUtils.AreElementsJoined(a.Document, a, b); }
            catch { return false; }
        }

        static bool Relates(Element x, Element y)
        {
            var fi = x as FamilyInstance;
            if (fi == null) return false;
            if (fi.Host != null && fi.Host.Id == y.Id) return true;
            if (fi.SuperComponent != null && fi.SuperComponent.Id == y.Id) return true;
            return false;
        }

        static void Add(ElementId a, ElementId b)
        {
            if (!Pairs.TryGetValue(a, out var set)) Pairs[a] = set = new HashSet<ElementId>();
            set.Add(b);
        }

        // ---------- Подсветка ----------

        static OverrideGraphicSettings RedOverride(Document doc)
        {
            var red = new Color(220, 20, 20);
            var ogs = new OverrideGraphicSettings();
            ogs.SetProjectionLineColor(red);
            ogs.SetCutLineColor(red);
            ogs.SetProjectionLineWeight(5);
            ogs.SetSurfaceTransparency(0);
            FillPatternElement solid = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>()
                .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);
            if (solid != null)
            {
                ogs.SetSurfaceForegroundPatternId(solid.Id);
                ogs.SetSurfaceForegroundPatternColor(red);
                ogs.SetCutForegroundPatternId(solid.Id);
                ogs.SetCutForegroundPatternColor(red);
            }
            return ogs;
        }

        /// <summary>Выставить красный цвет элементам с коллизиями, остальным из списка — сбросить. Вызывать внутри транзакции.</summary>
        public static void Apply(Document doc, View view, IEnumerable<ElementId> ids)
        {
            OverrideGraphicSettings red = RedOverride(doc);
            var none = new OverrideGraphicSettings();
            foreach (ElementId id in ids)
            {
                if (doc.GetElement(id) == null) { Highlighted.Remove(id); continue; }
                bool clash = Pairs.ContainsKey(id);
                view.SetElementOverrides(id, clash ? red : none);
                if (clash) Highlighted.Add(id); else Highlighted.Remove(id);
            }
        }

        /// <summary>Сбросить всю подсветку. Вызывать внутри транзакции.</summary>
        public static void Clear(Document doc, View view)
        {
            var none = new OverrideGraphicSettings();
            foreach (ElementId id in Highlighted)
                if (doc.GetElement(id) != null) view.SetElementOverrides(id, none);
            Highlighted.Clear();
            Pairs.Clear();
        }
    }
}
