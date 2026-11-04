using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace EdcRadio
{
    internal static class Program
    {
        public static readonly string Version = "v" + typeof(Program).Assembly.GetName().Version.ToString(3);

        public static string CrashLog =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EdcRadio", "crash.log");

        private static MainForm _form;
        private static int _crashed;

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int process);

        [STAThread]
        private static void Main()
        {
            EventWaitHandle wake;
            try
            {
                wake = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\EdcRadio.Wake", out bool first);
                if (!first)
                {
                    AllowSetForegroundWindow(-1);
                    wake.Set();
                    wake.Dispose();
                    return;
                }
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                wake = null;
            }
            using (wake)
                Run(wake);
        }

        private static void Run(EventWaitHandle wake)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Crash(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Crash(e.ExceptionObject as Exception);
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
                    _form = form;
                    RegisteredWaitHandle waiter = wake == null ? null : ThreadPool.RegisterWaitForSingleObject(wake, (state, timeout) =>
                    {
                        try
                        {
                            if (form.IsHandleCreated && !form.IsDisposed) form.BeginInvoke(new Action(form.Wake));
                        }
                        catch (Exception e) when (e is InvalidOperationException || e is ObjectDisposedException)
                        {
                        }
                    }, null, -1, false);
                    Application.Run(form);
                    waiter?.Unregister(null);
                    _form = null;
                }
            }
        }

        private static void Crash(Exception error)
        {
            if (Interlocked.Exchange(ref _crashed, 1) == 1) return;
            string path = CrashLog;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + Version + " " + Environment.OSVersion + Environment.NewLine + error + Environment.NewLine + Environment.NewLine);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
            }
            try
            {
                _form?.SaveState();
            }
            catch (Exception e) when (e is InvalidOperationException || e is ObjectDisposedException || e is IOException)
            {
            }
            try
            {
                _form?.HideTray();
                MessageBox.Show(string.Format(Text.T("crash"), path), "radio.exe", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception e) when (e is InvalidOperationException || e is ObjectDisposedException)
            {
            }
            Environment.Exit(1);
        }
    }
}
