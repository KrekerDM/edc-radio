using System;
using System.Threading;
using System.Windows.Forms;

namespace EdcRadio
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            using (var wake = new EventWaitHandle(false, EventResetMode.AutoReset, "EdcRadio.Wake", out bool first))
            {
                if (!first)
                {
                    wake.Set();
                    return;
                }
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Settings settings = Settings.Load();
                if (settings.Language == "ru") Text.Russian = true;
                else if (settings.Language == "en") Text.Russian = false;
                using (Art art = Art.Load())
                {
                    GC.Collect();
                    using (var form = new MainForm(settings, art))
                    {
                        RegisteredWaitHandle waiter = ThreadPool.RegisterWaitForSingleObject(wake, (state, timeout) =>
                        {
                            if (form.IsHandleCreated && !form.IsDisposed) form.BeginInvoke(new Action(form.Wake));
                        }, null, -1, false);
                        Application.Run(form);
                        waiter.Unregister(null);
                    }
                }
            }
        }
    }
}
