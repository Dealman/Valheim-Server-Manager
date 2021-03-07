using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Windows;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;
using System.IO.Compression;
using System.Text.RegularExpressions;
using MahApps.Metro.Controls.Dialogs;
using System.Net.Http;

namespace Valheim_Server_Manager
{
    public static class SteamManager
    {
        readonly static string rootFolder = Path.GetDirectoryName(Application.ResourceAssembly.Location);

        internal static class SteamCMD
        {
            public enum SteamCMDState
            {
                None = 0,
                Downloaded,
                Unzipped,
                Installed
            }

            static readonly string steamcmdURL = @"https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";
            static readonly string steamDir = Path.Combine(rootFolder, "SteamCMD");
            static readonly string steamZIP = Path.Combine(steamDir, "steamcmd.zip");
            static readonly string steamEXE = Path.Combine(steamDir, "steamcmd.exe");
            static readonly string uninstalledHash = "2629c77b1149eee9203e045e289e68ef";

            static string CalculateMD5(string file)
            {
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(file))
                    {
                        var hash = md5.ComputeHash(stream);
                        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }
            }

            public static SteamCMDState GetSteamCMDState()
            {
                if (File.Exists(steamEXE))
                    if (CalculateMD5(steamEXE) == uninstalledHash)
                        return SteamCMDState.Unzipped;
                    else
                        return SteamCMDState.Installed;
                else
                    if (File.Exists(steamZIP))
                        return SteamCMDState.Downloaded;

                return SteamCMDState.None;

            }

            static void Download()
            {
                using (WebClient client = new WebClient())
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

                    try
                    {
                        if (!Directory.Exists(steamDir))
                            Directory.CreateDirectory(steamDir);

                        // *Could* use async, but the file's so small anyway so I reckon it's rather redundant here
                        client.DownloadFile(steamcmdURL, steamZIP);

                        if (!File.Exists(steamZIP))
                            MessageBox.Show("Some error must've occurred when downloading SteamCMD, were unable to find it.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                    } catch (WebException we) {
                        MessageBox.Show($"An error occurred when trying to download SteamCMD, check your connection or try again.\n\nError Message:\n{we.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    } catch (Exception ex) {
                        MessageBox.Show($"An error occurred when trying to download SteamCMD, unknown error.\n\nError Message:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
            }

            static void Unzip()
            {
                if (File.Exists(steamZIP))
                    ZipFile.ExtractToDirectory(steamZIP, steamDir);
            }

            static void Install()
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(steamEXE)
                {
                    Arguments = $"+quit",
                    UseShellExecute = true,
                    CreateNoWindow = false
                };

                Process steamProcess = new Process()
                {
                    EnableRaisingEvents = false,
                    StartInfo = startInfo
                };

                steamProcess.Start();

                while (steamProcess.MainWindowHandle == IntPtr.Zero)
                    Thread.Sleep(100);

                NativeMethods.SetWindowText(steamProcess.MainWindowHandle, "Installing SteamCMD, please wait...");

                steamProcess.WaitForExit();

                if (steamProcess.ExitCode == 0 || steamProcess.ExitCode == 7)
                {
                    // 0 generally means success, however when isntalling SteamCMD here it returns 7 but still seems to be successfull
                    // am assuming that EC 7 means that it was shut down via the +quit argument?
                    MessageBox.Show("SteamCMD has been downloaded and installed successfully! You can now update and schedule server updates.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                } else {
                    MessageBox.Show($"An unknown error occurred when trying to download/update the server, SteamCMD did not return an exit code of 0!\n\nReceived Exit Code: {steamProcess.ExitCode}");
                }
            }

            public static void DownloadAndInstall()
            {
                var currentState = GetSteamCMDState();

                switch (currentState)
                {
                    case SteamCMDState.None:
                        Download();
                        Unzip();
                        Install();
                        return;

                    case SteamCMDState.Downloaded:
                        Unzip();
                        Install();
                        return;

                    case SteamCMDState.Unzipped:
                        Install();
                        return;

                    case SteamCMDState.Installed:
                        return;

                    default:
                        MessageBox.Show("An unknown error occurred when trying to download or install SteamCMD!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                }
            }

            public static string RunRead(string arguments, bool windowVisible = false, string windowTitle = "")
            {
                if (GetSteamCMDState() == SteamCMDState.Installed && !String.IsNullOrWhiteSpace(arguments))
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo(steamEXE)
                    {
                        Arguments = arguments,
                        UseShellExecute = windowVisible,
                        CreateNoWindow = !windowVisible,
                        RedirectStandardOutput = true
                    };

                    Process steamProcess = new Process()
                    {
                        EnableRaisingEvents = false,
                        StartInfo = startInfo
                    };

                    steamProcess.Start();
                    steamProcess.WaitForExit();

                    return steamProcess.StandardOutput.ReadToEnd();
                }

                return null;
            }

            public static int Run(string arguments, bool windowVisible = false, string windowTitle = "")
            {
                if (GetSteamCMDState() == SteamCMDState.Installed && !String.IsNullOrWhiteSpace(arguments))
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo(steamEXE)
                    {
                        Arguments = arguments,
                        UseShellExecute = windowVisible,
                        CreateNoWindow = !windowVisible
                    };

                    Process steamProcess = new Process()
                    {
                        EnableRaisingEvents = false,
                        StartInfo = startInfo
                    };

                    steamProcess.Start();
                    steamProcess.WaitForExit();

                    return steamProcess.ExitCode;
                }

                return -1;
            }
        }

        internal static class Server
        {
            static readonly Regex rxBuildID = new Regex(@"""buildid""\s+\D(\d+)\D");
            static readonly Regex rxSteamDB = new Regex(@"<td>(\d{7,10})<\/td>");

            public static long? GetVersion(string path)
            {
                string manifestPath = Path.Combine(path, "steamapps", "appmanifest_896660.acf");
                if (File.Exists(manifestPath))
                {
                    var contents = File.ReadAllText(manifestPath);

                    if (!String.IsNullOrWhiteSpace(contents))
                    {
                        Match match = rxBuildID.Match(contents);

                        if (match.Success)
                            if (long.TryParse(match.Groups[1].Value, out long buildid))
                                return buildid;
                    }
                }

                return null;
            }

            // TODO: This isn't very reliable and may break in the future. Also not very fast :(
            public static long? GetLatestVersion()
            {
                var result = SteamCMD.RunRead($"+@ShutdownOnFailedCommand 1 +@NoPromptForPassword 1 +@sSteamCmdForcePlatformType windows +login anonymous +app_info_print 896660 +quit");

                if (!String.IsNullOrWhiteSpace(result))
                {
                    Match match = rxBuildID.Match(result);

                    if (match.Success)
                        if (long.TryParse(match.Groups[1].Value, out long buildid))
                            return buildid;
                }

                return null;
            }

            // TODO: Rework this, it's pretty nasty, make async?
            public static async Task<bool> UpdateValdiate(string path)
            {
                MainWindow mw = (Application.Current.MainWindow as MainWindow);

                if (!String.IsNullOrWhiteSpace(path))
                {
                    if (!File.Exists(Path.Combine(path, "valheim_server.exe")))
                    {
                        var dialogResult = await DialogManager.ShowMessageAsync(mw, "No Server Found!", $"Chosen folder does not appear to have a server installed, would you like to download the server files to:\n\n{path}?", MessageDialogStyle.AffirmativeAndNegative);

                        if (dialogResult == MessageDialogResult.Negative)
                            return false;
                    }

                    var controller = await DialogManager.ShowProgressAsync(mw, "Updating & Validating", "Please wait while SteamCMD updates and validates the files...", false, new MetroDialogSettings { AnimateShow = false, NegativeButtonText = "Ok" });
                    var result = SteamCMD.Run($"+@ShutdownOnFailedCommand 1 +@NoPromptForPassword 1 +@sSteamCmdForcePlatformType windows +login anonymous +force_install_dir \"{path}\" +app_update 896660 validate +quit");

                    controller.Canceled += new EventHandler(async delegate (Object o, EventArgs e) {
                        await controller.CloseAsync();
                    });

                    if (result != 0)
                    {
                        controller.SetMessage($"Failed to update... SteamCMD returned code: {result.ToString()}");
                        controller.SetCancelable(true);
                    } else {
                        controller.SetMessage("SteamCMD successfully updated and validated the files!");
                        controller.SetProgress(1.0);
                        controller.SetCancelable(true);
                        return false;
                    }
                }

                return false;
            }
        }
    }
}
