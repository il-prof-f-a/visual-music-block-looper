using System;
using System.Runtime.InteropServices;

namespace VisualMusicBlockLooper.App
{
    /// <summary>MMCSS limitato al thread corrente; nessuna modifica al processo o al registro.</summary>
    internal sealed class PrioritaMultimediale : IDisposable
    {
        [DllImport("avrt.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr AvSetMmThreadCharacteristics(string nome, ref uint indice);
        [DllImport("avrt.dll", SetLastError = true)]
        private static extern bool AvRevertMmThreadCharacteristics(IntPtr handle);
        private readonly IntPtr handle;
        public PrioritaMultimediale()
        {
            try { uint indice = 0; handle = AvSetMmThreadCharacteristics("Pro Audio", ref indice); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
        public bool Attiva => handle != IntPtr.Zero;
        public void Dispose() { if (handle != IntPtr.Zero) AvRevertMmThreadCharacteristics(handle); }
    }
}
