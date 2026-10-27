using System.Reflection;
using Autodesk.Revit.UI;

namespace RevitLiveClash
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            string path = Assembly.GetExecutingAssembly().Location;
            RibbonPanel panel = app.CreateRibbonPanel("Live Clash");
            panel.AddItem(new PushButtonData("CheckClashes", "Проверить\nколлизии", path, typeof(CheckClashesCommand).FullName));
            panel.AddItem(new PushButtonData("ToggleLive", "Live\nвкл/выкл", path, typeof(ToggleLiveCommand).FullName));
            panel.AddItem(new PushButtonData("ClearClashes", "Сбросить\nподсветку", path, typeof(ClearClashesCommand).FullName));
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app) { return Result.Succeeded; }
    }
}
