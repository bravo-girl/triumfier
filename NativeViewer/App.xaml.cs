using System.Globalization;
using System.Threading;
using System.Windows;

namespace NativeViewer
{
    public partial class App : Application
    {
        public App()
        {
            CultureInfo germanCulture = CultureInfo.GetCultureInfo("de-DE");
            Thread.CurrentThread.CurrentCulture = germanCulture;
            Thread.CurrentThread.CurrentUICulture = germanCulture;
        }
    }
}
