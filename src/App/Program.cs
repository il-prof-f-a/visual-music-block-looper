using System;
using System.Windows.Forms;

namespace VisualMusicBlockLooper.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form { Text = "Visual Music Block Looper" });
        }
    }
}
