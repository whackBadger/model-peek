using System.IO;
using System.Linq;
using System.Windows;

namespace ModelPeek;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length == 2 &&
            string.Equals(e.Args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                StlModel model = StlLoader.Load(e.Args[1]);
                Shutdown(model.TriangleCount > 0 && model.Radius > 0 ? 0 : 2);
            }
            catch
            {
                Shutdown(2);
            }

            return;
        }

        string? startupFile = e.Args.FirstOrDefault(File.Exists);
        var window = new MainWindow(startupFile);
        MainWindow = window;
        window.Show();
    }
}
