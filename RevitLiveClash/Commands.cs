using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitLiveClash
{
    [Transaction(TransactionMode.Manual)]
    public class CheckClashesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            Document doc = data.Application.ActiveUIDocument.Document;
            View view = doc.ActiveView;
            if (view is ViewSheet || view.IsTemplate) { message = "Откройте 3D-вид или план."; return Result.Failed; }

            using (var t = new Transaction(doc, "Подсветка коллизий"))
            {
                t.Start();
                ClashEngine.Clear(doc, view);
                int n = ClashEngine.CheckAll(doc, view);
                ClashEngine.Apply(doc, view, new System.Collections.Generic.List<ElementId>(ClashEngine.Pairs.Keys));
                t.Commit();
                TaskDialog.Show("Коллизии", n == 0 ? "Коллизий не найдено." : $"Элементов в коллизиях: {n}. Подсвечены красным.");
            }
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class ClearClashesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            Document doc = data.Application.ActiveUIDocument.Document;
            using (var t = new Transaction(doc, "Сброс подсветки коллизий"))
            {
                t.Start();
                ClashEngine.Clear(doc, doc.ActiveView);
                t.Commit();
            }
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class ToggleLiveCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            Document doc = data.Application.ActiveUIDocument.Document;
            if (LiveUpdater.Enabled) { LiveUpdater.Disable(doc); TaskDialog.Show("Live clash", "Live-режим выключен."); }
            else { LiveUpdater.Enable(doc); TaskDialog.Show("Live clash", "Live-режим включён: коллизии пересчитываются при изменении модели (сначала выполните «Проверить»)."); }
            return Result.Succeeded;
        }
    }
}
