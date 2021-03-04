using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Valheim_Server_Manager
{
    // TODO: Move all server related actions in here
    public static class ValheimServer
    {
        public struct ServerSettings
        {
            public string ServerPath { get; set; }
            public string ServerName { get; set; }
            public string ServerPassword { get; set; }
            public int ServerPort { get; set; }
            public string SaveDirectory { get; set; }
            public string WorldName { get; set; }
            public Process ServerProcess { get; set; }
        }

        // Custom EventArgs
        public class StateChangedArgs : EventArgs
        {
            public Enums.ServerStateEnum NewState { get; set; }
        }
        public class ServerExitArgs : EventArgs
        {
            public Enums.ServerExitEnum Reason { get; set; }
        }

        // Started Event
        public delegate void ServerStartedHandler(Process process, EventArgs e);
        public static event ServerStartedHandler OnServerStarted;
        // Stopped Event
        public delegate void ServerStoppedHandler(object sender, EventArgs e);
        public static event ServerStoppedHandler OnServerStopped;
        // Output Event
        public delegate void ServerOutputHandler(object sneder, string outputMessage);
        public static event ServerOutputHandler OnOutputReceived;
        // State Changed Event
        public delegate void ServerStateChangedHandler(object sender, StateChangedArgs e);
        public static event ServerStateChangedHandler OnServerStateChanged;
        // Server Event
        public delegate void ServerEventHandler(object sender, EventArgs e);
        public static event ServerEventHandler OnServerEvent;

        static readonly string serverAppID = "892970"; // Use 892970, other to test failure to start
        static ServerSettings currentSettings;
        static Regex rxSteamID = new Regex(@"(\d{17})", RegexOptions.Compiled);

        public static bool ValidatePath(string path)
        {
            if (!String.IsNullOrWhiteSpace(path))
            {
                if(File.Exists(Path.Combine(path, "valheim_server.exe")))
                    return true;
            }

            return false;
        }

        public static string TryGetServerPath()
        {
            // TODO: Try and read the registry to automagically find the installation path of Valheim Dedicated Serveroni
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = hklm.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Steam App 896660", false))
                {
                    if (key == null)
                        return null;

                    Object obj = key.GetValue("InstallLocation", null);

                    if (obj != null)
                        return (obj as string);
                }
            } catch (Exception ex) {
                // May potentially fail due to permissions
                MessageBox.Show($"An error has occurred while trying to read the Registry.\n\nError Message: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return null;
        }

        public static bool IsServerRunning()
        {
            Process[] processList = Process.GetProcessesByName("valheim_server");

            if (processList.Length > 0)
                return true;

            return false;
        }

        public static Process GetServerProcess()
        {
            if (!IsServerRunning())
            {
                return null;
            } else {
                if (currentSettings.ServerProcess != null)
                    return currentSettings.ServerProcess;
                else
                    return Process.GetProcessesByName("valheim_server").First();
            }
        }

        private static bool ValidateServerSettings(ServerSettings serverSettings)
        {
            if (!String.IsNullOrWhiteSpace(serverSettings.ServerPath) && File.Exists(serverSettings.ServerPath))
                if (!String.IsNullOrWhiteSpace(serverSettings.ServerName))
                    if (!String.IsNullOrWhiteSpace(serverSettings.ServerPassword) && serverSettings.ServerPassword.Length >= 5)
                        if (serverSettings.ServerPort >= 1024)
                            if (!String.IsNullOrWhiteSpace(serverSettings.WorldName))
                                return true;

            return false;
        }

        public static async Task StartAsync(ServerSettings serverSettings)
        {
            // Update Server State: Starting
            ServerStateChanged(null, new StateChangedArgs() { NewState = Enums.ServerStateEnum.Starting });

            currentSettings = serverSettings;

            // Set up the StartInfo, such as the server parameters
            ProcessStartInfo startInfo = new ProcessStartInfo(serverSettings.ServerPath)
            {
                Arguments = $"-nographics -batchmode -name \"{currentSettings.ServerName}\" -port {currentSettings.ServerPort} -world \"{currentSettings.WorldName}\" -password \"{currentSettings.ServerPassword}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
            };
            startInfo.EnvironmentVariables.Add("SteamAppId", serverAppID);
            
            // Set up the actual process itself
            Process serverProcess = new Process()
            {
                EnableRaisingEvents = true,
                StartInfo = startInfo
            };

            // Bind the events
            serverProcess.OutputDataReceived += ServerOutputReceived;
            serverProcess.Exited += ServerStopped;

            serverProcess.Start();
            serverProcess.BeginOutputReadLine();

            if (serverProcess.Responding)
            {
                ServerStarted(serverProcess, EventArgs.Empty);
            } else {
                ServerStopped(null, EventArgs.Empty);
            }

            //serverProcess.WaitForExit();
        }

        private static void ServerStopped(object sender, EventArgs e)
        {
            if (OnServerStopped == null)
                return;

            ServerStateChanged(null, new StateChangedArgs() { NewState = Enums.ServerStateEnum.Offline });
    
            OnServerStopped(sender, e);
        }

        private static void ServerStarted(Process process, EventArgs e)
        {
            if (OnServerStarted == null) return;

            //ServerStateChanged(null, new StateChangedArgs() { NewState = Enums.ServerStateEnum.Online });
            OnServerStarted(process, e);
        }

        private static void ServerStateChanged(object sender, StateChangedArgs e)
        {
            OnServerStateChanged?.Invoke(typeof(ValheimServer), e);
        }

        private static void ServerOutputReceived(object sender, DataReceivedEventArgs e)
        {
            if (OnOutputReceived == null || String.IsNullOrWhiteSpace(e.Data)) return;

            if (e.Data.Contains("Game server connected"))
                ServerStateChanged(null, new StateChangedArgs() { NewState = Enums.ServerStateEnum.Online });

            OnOutputReceived(null, e.Data);
        }

        private static void ServerEvent(object sender, EventArgs e)
        {
            OnServerEvent?.Invoke(typeof(ValheimServer), e);
        }

        private static void ServerProcessExited(object sender, EventArgs e)
        {
            //Debug.WriteLine("Process exited");

        }

        public static void Stop()
        {
            if (!IsServerRunning())
                return;

            // Update Server State: Closing
            ServerStateChanged(null, new StateChangedArgs() { NewState = Enums.ServerStateEnum.Closing });

            // We'll want to try and close the server gracefully, this *can* be done in various ways
            // But this was the only reliable way I could find without running the risk of world corruption/not saving
            // Since the server console expects you to close via CTRL+C
            IEnumerable<IntPtr> windows = NativeMethods.FindWindowsWithText("Valheim");

            foreach (IntPtr ptr in windows)
            {
                string wndTxt = NativeMethods.GetWindowText(ptr);
                if (!String.IsNullOrWhiteSpace(wndTxt))
                {
                    // We rule out the server manager itself as it'd part of the result
                    if (!wndTxt.Contains("Valheim Server Manager"))
                    {
                        // If the game client is running, we'd have 2 windows called "Valheim", so we need a way to distinguish the two
                        // We do this by checking the executing assembly, since the server would contain 'valheim_server.exe' while the game 'valheim.exe'
                        string execPath = NativeMethods.GetWindowPath(ptr);
                        if (!String.IsNullOrWhiteSpace(execPath) && execPath.Contains("valheim_server"))
                        {
                            NativeMethods.SendMessage(ptr, 0x10, IntPtr.Zero, IntPtr.Zero);
                            return;
                        }
                    }
                }
            }

            // Weren't able to get the hidden window, kill process? Is it safe? World save? IronGate pls...
            currentSettings.ServerProcess.CloseMainWindow();
            currentSettings.ServerProcess.Kill();
        }
    }
}
