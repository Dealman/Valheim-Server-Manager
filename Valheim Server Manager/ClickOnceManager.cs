using MahApps.Metro.Controls.Dialogs;
using System;
using System.Deployment.Application;
using System.Diagnostics;
using System.Windows;

namespace Valheim_Server_Manager
{
    public static class ClickOnceManager
    {
        public static string CurrentVersion()
        {
            if (ApplicationDeployment.IsNetworkDeployed)
            {
                ApplicationDeployment appDeployment = ApplicationDeployment.CurrentDeployment;

                return appDeployment.CurrentVersion.ToString();
            }

            return "1.0.0.0";
        }

        public static void CheckForUpdate()
        {
            UpdateCheckInfo info = null;

            if (ApplicationDeployment.IsNetworkDeployed)
            {
                ApplicationDeployment appDeployment = ApplicationDeployment.CurrentDeployment;

                try
                {
                    info = appDeployment.CheckForDetailedUpdate();
                } catch (DeploymentDownloadException dde) {
                    MessageBox.Show($"Unable to download the latest version, check your network connection and try again.\n\nError Message:\n{dde.Message}");
                    return;
                } catch (InvalidDeploymentException ide) {
                    MessageBox.Show($"Unable to check for a new version, the ClickOnce deployment may be corrupt. Try again and/or report the issue.\n\nError Message:\n{ide.Message}");
                    return;
                } catch (InvalidOperationException ioe) {
                    MessageBox.Show($"Unable to update.\n\nError Message:\n{ioe.Message}");
                    return;
                }

                if (info.UpdateAvailable)
                {
                    bool shouldUpdate = true;

                    if (!info.IsUpdateRequired)
                    {
                        MessageBoxResult result = MessageBox.Show("An update is available, but not mandatory. Would you still like to update the application?", "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result == MessageBoxResult.No)
                            shouldUpdate = false;
                    } else {
                        MessageBox.Show("An update marked as mandatory was detected, this means it likely contains changes necessary to function as expected. The application will now install the required update and restart.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    if (shouldUpdate)
                    {
                        try
                        {
                            appDeployment.Update();
                            MessageBox.Show("The application has been updated and will now restart. If the server is already running, it should be gracefully closed.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                            RestartApplication();
                        } catch (DeploymentDownloadException dde) {
                            MessageBox.Show($"Unable to download the latest version, check your network connection and try again.\n\nError Message:\n{dde.Message}");
                        }
                    }
                } else {
                    MessageBox.Show("No updates appear to be available at this time, try again later.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            } else {
                MessageBoxResult result = MessageBox.Show("This version of the application is unable to update automatically. Try downloading the latest portable version instead.\n\nWould you like to open the download page?", "Information", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (result == MessageBoxResult.Yes)
                {
                    Process.Start("https://github.com/Dealman/Valheim-Server-Manager/releases");
                }
            }
        }

        private static void RestartApplication()
        {
            if (ValheimServer.IsServerRunning())
            {
                Process process = ValheimServer.GetServerProcess();
                ValheimServer.Stop();
                process.WaitForExit();
            }

            System.Windows.Forms.Application.Restart();
            Application.Current.Shutdown();
        }
    }
}
