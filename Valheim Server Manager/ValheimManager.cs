using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Valheim_Server_Manager
{
    public static class ValheimManager
    {
        public static readonly string ServerAppID = "892970";
        private static readonly Guid localLowGUID = new Guid("A520A1A4-1780-4FF6-BD18-167343C5AF16");
        private static readonly string defaultWorldName = "MyWorld";

        #region Imports
        [DllImport("shell32.dll")]
        static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr pszPath);
        #endregion

        #region Import Methods
        private static string GetKnownFolderPath(Guid knownFolderId)
        {
            IntPtr pszPath = IntPtr.Zero;
            try
            {
                int hr = SHGetKnownFolderPath(knownFolderId, 0, IntPtr.Zero, out pszPath);
                if (hr >= 0)
                    return Marshal.PtrToStringAuto(pszPath);
                throw Marshal.GetExceptionForHR(hr);
            } finally {
                if (pszPath != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pszPath);
            }
        }
        #endregion

        public static bool IsValidServerDirectory(string directory)
        {
            // TODO: Maybe make this a bit more reliable
            return File.Exists(Path.Combine(directory, "valheim_server.exe"));
        }

        public static string GetDefaultSaveDir()
        {
            string localLowPath = GetKnownFolderPath(localLowGUID);
            if(!String.IsNullOrWhiteSpace(localLowPath) && Directory.Exists(localLowPath))
            {
                if(Directory.Exists(Path.Combine(localLowPath, "IronGate", "Valheim")))
                {
                    return Path.GetFullPath(Path.Combine(localLowPath, "IronGate", "Valheim"));
                }
            }

            return null;
        }

        public static List<string> GetWorldList(string path)
        {
            if (!String.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                List<string> worldList = Directory.GetFiles(path, "*.db").Select(x => Path.GetFileNameWithoutExtension(x)).ToList<string>();

                if (worldList.Count > 0)
                {
                    return worldList;
                }
            }

            return null;
        }

        public static void StartServer()
        {

        }
    }
}
