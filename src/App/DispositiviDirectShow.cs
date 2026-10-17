using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace VisualMusicBlockLooper.App
{
    /// <summary>
    /// Enumera i dispositivi video di Windows tramite DirectShow (interop COM, nessuna dipendenza esterna).
    /// L'ordine coincide con l'indice usato da OpenCV con il backend DirectShow, quindi nome e indice
    /// sono coerenti.
    /// </summary>
    public static class DispositiviDirectShow
    {
        private static readonly Guid ClsidSystemDeviceEnum = new Guid("62BE5D10-60EB-11d0-BD3B-00A0C911CE86");
        private static readonly Guid CategoriaVideoInput = new Guid("860BB310-5D01-11d0-BD3B-00A0C911CE86");
        private static readonly Guid IidPropertyBag = new Guid("55272A00-42CB-11CE-8135-00AA004BB851");

        [ComImport]
        [Guid("29840822-5B84-11D0-BD3B-00A0C911CE86")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ICreateDevEnum
        {
            [PreserveSig]
            int CreateClassEnumerator([In] ref Guid tipo, out IEnumMoniker enumeratore, [In] int flag);
        }

        [ComImport]
        [Guid("55272A00-42CB-11CE-8135-00AA004BB851")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyBag
        {
            [PreserveSig]
            int Read([In, MarshalAs(UnmanagedType.LPWStr)] string nome, [Out, MarshalAs(UnmanagedType.Struct)] out object valore, [In] IntPtr registroErrori);

            [PreserveSig]
            int Write([In, MarshalAs(UnmanagedType.LPWStr)] string nome, [In, MarshalAs(UnmanagedType.Struct)] ref object valore);
        }

        /// <summary>
        /// Elenco dei dispositivi video, nell'ordine di sistema. Non lancia mai: se COM o DirectShow non
        /// sono disponibili restituisce un elenco vuoto.
        /// </summary>
        public static IReadOnlyList<InfoWebcam> Enumera()
        {
            List<InfoWebcam> risultato = new List<InfoWebcam>();
            object creatoreCom = null;
            IEnumMoniker enumeratore = null;
            try
            {
                Type tipo = Type.GetTypeFromCLSID(ClsidSystemDeviceEnum);
                creatoreCom = Activator.CreateInstance(tipo);
                ICreateDevEnum creatore = (ICreateDevEnum)creatoreCom;
                Guid categoria = CategoriaVideoInput;

                // S_FALSE (1) = la categoria è vuota.
                if (creatore.CreateClassEnumerator(ref categoria, out enumeratore, 0) != 0 || enumeratore == null)
                {
                    return risultato;
                }

                IMoniker[] moniker = new IMoniker[1];
                int indice = 0;
                while (enumeratore.Next(1, moniker, IntPtr.Zero) == 0)
                {
                    try
                    {
                        risultato.Add(Leggi(moniker[0], indice));
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(moniker[0]);
                    }

                    indice++;
                }
            }
            catch (Exception)
            {
                // COM/DirectShow non disponibile: l'elenco resta quello letto finora (eventualmente vuoto).
            }
            finally
            {
                if (enumeratore != null)
                {
                    Marshal.ReleaseComObject(enumeratore);
                }

                if (creatoreCom != null)
                {
                    Marshal.ReleaseComObject(creatoreCom);
                }
            }

            return risultato;
        }

        private static InfoWebcam Leggi(IMoniker moniker, int indice)
        {
            string nome = null;
            string percorso = null;
            object sacco = null;
            try
            {
                Guid iid = IidPropertyBag;
                moniker.BindToStorage(null, null, ref iid, out sacco);
                IPropertyBag proprieta = (IPropertyBag)sacco;
                nome = LeggiTesto(proprieta, "FriendlyName");
                percorso = LeggiTesto(proprieta, "DevicePath");
            }
            catch (Exception)
            {
                // Dispositivo senza proprietà leggibili: resta in elenco con il solo indice.
            }
            finally
            {
                if (sacco != null)
                {
                    Marshal.ReleaseComObject(sacco);
                }
            }

            return new InfoWebcam(indice, nome, percorso);
        }

        private static string LeggiTesto(IPropertyBag proprieta, string chiave)
        {
            return proprieta.Read(chiave, out object valore, IntPtr.Zero) == 0 ? valore as string : null;
        }
    }
}
