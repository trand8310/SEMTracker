
using SEM;
using System.Diagnostics;

namespace MainClient.Common
{
    public class AppHelper
    {
        /// <summary>
        /// 系统重启
        /// </summary>
        public static void ProcessRestart()
        {
            Process.Start(Application.ExecutablePath, "restart");
            try
            {
                Process.GetCurrentProcess().Kill();
            }
            catch (Exception)
            {
                CommonHelper.KillProcExec(Process.GetCurrentProcess().Id);
            }
        }
        public static void CreateShortcut(string shortcutName)
        {
            IWshRuntimeLibrary.WshShell wsh = new IWshRuntimeLibrary.WshShell();
            var shortcutPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), $"{shortcutName}_{string.Join("", AppConsts.AppVersion.Split('.').Take(1))}.lnk");
            if (System.IO.File.Exists(shortcutPath))
            {
                System.IO.File.Delete(shortcutPath);
            }
            IWshRuntimeLibrary.IWshShortcut shortcut = wsh.CreateShortcut(shortcutPath) as IWshRuntimeLibrary.IWshShortcut;
            shortcut.Arguments = "restart";
            shortcut.TargetPath = System.Windows.Forms.Application.ExecutablePath;
            shortcut.WindowStyle = 1;
            shortcut.Description = shortcutName;
            shortcut.WorkingDirectory = System.AppDomain.CurrentDomain.BaseDirectory;
            shortcut.IconLocation = System.Windows.Forms.Application.ExecutablePath;
            shortcut.Save();
        }
    }
}
