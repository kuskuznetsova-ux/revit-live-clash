using System;
using System.Linq;
using Autodesk.Revit.DB;

namespace RevitLiveClash
{
    /// <summary>Пересчитывает коллизии на лету при изменении геометрии или добавлении элементов.</summary>
    public class LiveUpdater : IUpdater
    {
        static readonly UpdaterId Id =
            new UpdaterId(new Autodesk.Revit.DB.AddInId(new Guid("a3c7e1f4-5b2d-4e8a-9c61-7d0f2b4a8e35")),
                          new Guid("5e0b9d2a-61c4-4f7e-8a39-c2d41b7f0e86"));

        public static bool Enabled { get; private set; }

        public UpdaterId GetUpdaterId() { return Id; }
        public string GetUpdaterName() { return "Revit live clash"; }
        public string GetAdditionalInformation() { return "Подсветка коллизий в реальном времени"; }
        public ChangePriority GetChangePriority() { return ChangePriority.FreeStandingComponents; }

        public void Execute(UpdaterData data)
        {
            Document doc = data.GetDocument();
            View view = doc.ActiveView;
            if (view == null || view.IsTemplate) return;

            var changed = data.GetModifiedElementIds().Concat(data.GetAddedElementIds()).ToList();
            if (changed.Count == 0) return;
            try
            {
                var affected = ClashEngine.Recheck(doc, view, changed);
                ClashEngine.Apply(doc, view, affected);
            }
            catch { /* вид может быть недоступен для переопределений (шаблон вида и т.п.) */ }
        }

        public static void Enable(Document doc)
        {
            var updater = new LiveUpdater();
            if (!UpdaterRegistry.IsUpdaterRegistered(Id, doc))
                UpdaterRegistry.RegisterUpdater(updater, doc, false);
            var filter = new ElementIsElementTypeFilter(true); // только не-типы
            UpdaterRegistry.RemoveAllTriggers(Id);
            UpdaterRegistry.AddTrigger(Id, doc, filter, Element.GetChangeTypeGeometry());
            UpdaterRegistry.AddTrigger(Id, doc, filter, Element.GetChangeTypeElementAddition());
            Enabled = true;
        }

        public static void Disable(Document doc)
        {
            if (UpdaterRegistry.IsUpdaterRegistered(Id, doc))
                UpdaterRegistry.UnregisterUpdater(Id, doc);
            Enabled = false;
        }
    }
}
