using ControlzEx.Theming;
using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq;
using Valheim_Server_Manager.Properties;
using WinForms = System.Windows.Forms;
using System.ComponentModel;
using System.Windows.Threading;
using FontAwesome.WPF;

namespace Valheim_Server_Manager
{
    public partial class MainWindow : MetroWindow
    {
        private string workingDirectory;
        private string saveDirectory;
        private int playerCount;
        private string selectedWorld;
        private SynchronizationContext syncContext;
        private Process serverProcess = null;
        //private Enums.DebugLevel debugLevel = Enums.DebugLevel.Low;
        //private Enums.ServerState serverState = Enums.ServerState.Invalid;
        private List<Player> playerList = new List<Player>();
        private Regex rxSteamID = new Regex(@"(\d{17})", RegexOptions.Compiled);
        private Regex rxCharName = new Regex(@"from\s(.+)\s:", (RegexOptions.Compiled | RegexOptions.IgnoreCase));
        private Regex rxCharID = new Regex(@"(\S\d{5,16}):1", RegexOptions.Compiled); // Not sure how long the CharID can be, so setting a range of 5 to 16
        private Enums.ConsoleType currentConsole = Enums.ConsoleType.Network;
        //private Enums.ListType currentList = Enums.ListType.None;

        #region Resource Definitions
        private MessageConsole networkConsole;
        private MessageConsole debugConsole;
        private MessageConsole worldConsole;
        private PlayerListControl playerView;
        private CustomListDisplay adminView;
        private CustomListDisplay permittedView;
        private CustomListDisplay bannedView;
        #endregion

        public MainWindow()
        {
            InitializeComponent();

            networkConsole = (MessageConsole)FindResource("NetworkConsole");
            debugConsole = (MessageConsole)FindResource("DebugConsole");
            worldConsole = (MessageConsole)FindResource("WorldGenConsole");
            playerView = (PlayerListControl)FindResource("NewPlayerList"); // TODO: Rename
            adminView = (CustomListDisplay)FindResource("AdminList");
            permittedView = (CustomListDisplay)FindResource("PermitList");
            bannedView = (CustomListDisplay)FindResource("BanList");
        }

        #region Server Events
        private void ValheimServer_OnServerStateChanged(object sender, ValheimServer.StateChangedArgs e)
        {
            switch (e.NewState)
            {
                case Enums.ServerStateEnum.Starting:
                    SetStatusBarIcon(FontAwesomeIcon.Cog, true);
                    SetButtonText(StartButton, "Stop Server");
                    SetStatusBarText("Server Status: Starting...");
                    ToggleSettingsState(false);
                    SetThemeCombination("Dark", "Blue");
                    break;

                case Enums.ServerStateEnum.Online:
                    SetStatusBarIcon(FontAwesomeIcon.Cog, true);
                    SetButtonText(StartButton, "Stop Server");
                    SetStatusBarText("Server Status: Online");
                    SetThemeCombination("Dark", "Blue");
                    break;

                case Enums.ServerStateEnum.Closing:
                    break;

                case Enums.ServerStateEnum.Offline:
                    SetStatusBarIcon(FontAwesomeIcon.Cog, false);
                    SetButtonText(StartButton, "Start Server");
                    ToggleSettingsState(true);
                    break;

                default:
                    break;
            }
        }
        private void ValheimServer_OnServerStarted(Process process, EventArgs e)
        {
            // Server has started
        }
        private void ValheimServer_OnServerStopped(object sender, EventArgs e)
        {
            // Server was stopped
            this.Dispatcher.Invoke(() =>
            {
                IsServerSetupValid();
            });
        }
        private void ValheimServer_OnOutputReceived(object sender, string outputMessage)
        {
            if (!String.IsNullOrWhiteSpace(outputMessage))
            {
                ConsoleMessage msg = new ConsoleMessage(outputMessage);

                if (String.IsNullOrWhiteSpace(msg.Message) || msg.Message.Contains("<<IGNORE>>"))
                    return;

                AddConsoleMessage(msg.Message, msg.Type, msg.Brush, msg.Weight, msg.Size);

                if (msg.Type == Enums.MessageType.Network)
                    ParseMessageForConnection(msg.OriginalMessage);
            }
        }
        private void ParseMessageForConnection(string msg)
        {
            // Someone's trying to connect, try to fetch the SteamID
            if (msg.Contains("Got connection SteamID"))
            {
                Match match = rxSteamID.Match(msg);
                if (!String.IsNullOrWhiteSpace(match.Value))
                {
                    // Someone is connecting, we got their SteamID, no handshake yet
                    Player newPlayer = new Player(match.Value);
                    newPlayer.RequestReceived = true;
                    playerList.Add(newPlayer);
                }
            }
            // Got a handshake from a connecting client, fetch the SteamID and find the matching Player class
            if (msg.Contains("Got handshake from client"))
            {
                Match match = rxSteamID.Match(msg);
                if (!String.IsNullOrWhiteSpace(match.Value))
                {
                    // We now got a handshake from someone
                    foreach (Player player in playerList)
                    {
                        if (player.SteamID == match.Value)
                        {
                            player.HandshakeReceived = true;
                            return;
                        }
                    }
                }
            }
            // Client is now successfully connected to the server and will be assigned a char soon
            if (msg.Contains("New peer connected"))
            {
                foreach (Player player in playerList)
                {
                    if (player.RequestReceived && player.HandshakeReceived && !player.IsConnected)
                    {
                        player.IsConnected = true;
                        playerCount++;
                        UpdatePlayerCount(playerCount);
                        return;
                    }
                }
            }
            // TODO: If ZDO ends with :0, someone died. If it ends with :1, they just connected. Anything other than 0 or 1, is a new ID when they respawn
            if (msg.Contains("Got character ZDOID"))
            {
                foreach (Player player in playerList)
                {
                    if (player.RequestReceived && player.HandshakeReceived && player.IsConnected && (String.IsNullOrEmpty(player.CharacterName) || String.IsNullOrEmpty(player.CharacterID)))
                    {
                        player.IsConnected = true;
                        player.CharacterName = rxCharName.Match(msg).Groups[1].Value;
                        player.CharacterID = rxCharID.Match(msg).Groups[1].Value;
                        player.JoinTime = DateTime.Now;

                        this.Dispatcher.Invoke(() =>
                        {
                            playerView.AddPlayer(player);
                            //NewPlayerList.AddPlayer(player);
                        });
                    }
                }
            }
            // Player Leaving
            if (msg.Contains("Closing socket"))
            {
                Match match = rxSteamID.Match(msg);
                if (!String.IsNullOrWhiteSpace(match.Value))
                {
                    Player leavingPlayer = playerList.Find(x => (x.SteamID == match.Value));
                    if (leavingPlayer != null)
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            playerView.RemovePlayer(leavingPlayer);
                            //NewPlayerList.RemovePlayer(leavingPlayer);
                        });

                        if (playerCount > 0)
                        {
                            playerCount--;
                            UpdatePlayerCount(playerCount);
                        }
                    }
                }
            }
        }
        #endregion

        #region UI Update Methods
        // These are methods used to update the UI, which will be on another thread - thus we use the dispatcher
        private void SetThemeCombination(string baseColour, string accentColour)
        {
            this.Dispatcher.Invoke(() =>
            {
                ThemeManager.Current.ChangeThemeBaseColor(this, baseColour);
                ThemeManager.Current.ChangeThemeColorScheme(this, accentColour);
            });
        }
        private void SetStatusBarIcon(FontAwesomeIcon newIcon, bool shouldSpin)
        {
            this.Dispatcher.Invoke(() =>
            {
                ServerStateIcon.Icon = newIcon;
                ServerStateIcon.Spin = shouldSpin;
            });
        }
        private void SetButtonText(Button button, string text)
        {
            if (button == null || String.IsNullOrWhiteSpace(text))
                return;

            this.Dispatcher.Invoke(() =>
            {
                button.Content = text;
            });
        }
        private void SetStatusBarText(string message)
        {
            if (String.IsNullOrWhiteSpace(message))
                return;

            this.Dispatcher.Invoke(() =>
            {
                CurrentStatusLabel.Content = message;
            });
        }
        private void UpdatePlayerCount(int newCount)
        {
            // TODO: Apparently you can remove the player cap of 10 via modding, support this somehow?
            this.Dispatcher.Invoke(() =>
            {
                PlayerCountLabel.Content = $"{newCount}/10";
                PlayerCountBadge.Badge = newCount;
            });
        }
        private void AddConsoleMessage(string msg, Enums.MessageType type, Brush msgColour = null, FontWeight? msgWeight = null, double msgSize = 12)
        {
            if (msgColour == null)
                msgColour = Brushes.White;

            if (msgWeight == null)
                msgWeight = FontWeights.Normal;

            switch (type)
            {
                case Enums.MessageType.Network:
                    networkConsole.AddMessage(msg);
                    IncrementBadge(Enums.MessageType.Network);
                    break;

                case Enums.MessageType.Debug:
                    debugConsole.AddMessage(msg);
                    IncrementBadge(Enums.MessageType.Debug);
                    break;

                case Enums.MessageType.WorldGen:
                    worldConsole.AddMessage(msg);
                    IncrementBadge(Enums.MessageType.WorldGen);
                    break;
            }
        }
        private void ClearBadgeForButton(Button button)
        {
            if (button == NetworkConsoleButton)
                NetworkBadge.Badge = null;

            if (button == DebugConsoleButton)
                DebugBadge.Badge = null;

            if (button == WorldGenConsoleButton)
                WorldGenBadge.Badge = null;
        }
        private void IncrementBadge(Enums.MessageType mType)
        {
            this.Dispatcher.Invoke(() =>
            {
                switch (mType)
                {
                    case Enums.MessageType.Network:
                        if (LeTransit.Content != networkConsole)
                            NetworkBadge.Badge = (NetworkBadge.Badge == null ? 1 : ((int)NetworkBadge.Badge + 1));
                        return;
                    case Enums.MessageType.Debug:
                        if (LeTransit.Content != debugConsole)
                            DebugBadge.Badge = (DebugBadge.Badge == null ? 1 : ((int)DebugBadge.Badge + 1));
                        return;
                    case Enums.MessageType.WorldGen:
                        if (LeTransit.Content != worldConsole)
                            WorldGenBadge.Badge = (WorldGenBadge.Badge == null ? 1 : ((int)WorldGenBadge.Badge + 1));
                        return;
                }
            });
        }
        #endregion

        #region Utility Methods
        private void UpdateWorlds()
        {
            if (WorldsDropDown.Items.Count > 0)
                WorldsDropDown.Items.Clear();

            if (String.IsNullOrWhiteSpace(saveDirectory))
            {
                List<string> worldList = ValheimManager.GetWorldList(Path.Combine(ValheimManager.GetDefaultSaveDir(), "worlds"));
                if (worldList != null && worldList.Count > 0)
                {
                    foreach (string world in worldList)
                    {
                        WorldsDropDown.Items.Add(world);
                    }
                }
            } else {
                List<string> worldList = ValheimManager.GetWorldList(saveDirectory);

                if (worldList != null && worldList.Count > 0)
                {
                    foreach (string world in worldList)
                    {
                        WorldsDropDown.Items.Add(world);
                    }
                } else {
                    WorldsDropDown.Items.Clear();
                    WorldsDropDown.SelectedIndex = -1;
                    TextBoxHelper.SetWatermark(WorldsDropDown, "No worlds found...");
                }
            }
        }

        public void ToggleSettingsState(bool newState)
        {
            this.Dispatcher.Invoke(() =>
            {
                var children = SettingsGrid.Children;

                if (children.Count > 0)
                {
                    foreach (var child in children)
                    {
                        UIElement element = child as UIElement;
                        element.IsEnabled = newState;
                    }
                }
            });
        }
        #endregion

        #region MainWindow Event Handlers
        private void MetroWindow_ContentRendered(object sender, EventArgs e)
        {
            #region Load Settings
            // Location
            if (Settings.Default.StartX != 0 && Settings.Default.StartY != 0)
            {
                this.Left = Settings.Default.StartX;
                this.Top = Settings.Default.StartY;
            } else {
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            // Size
            if (Settings.Default.SizeX > 0 && Settings.Default.SizeY > 0)
            {
                this.SizeToContent = SizeToContent.Manual;
                this.Width = Settings.Default.SizeX;
                this.Height = Settings.Default.SizeY;
            } else {
                this.SizeToContent = SizeToContent.WidthAndHeight;
                this.Width = this.MinWidth;
                this.Height = this.MinHeight;
            }

            // Path to server
            string serverPath = Settings.Default.ServerPath;
            if (!String.IsNullOrWhiteSpace(serverPath) && Directory.Exists(serverPath) && ValheimManager.IsValidServerDirectory(serverPath))
            {
                ServerDirTextBox.Text = serverPath;
                workingDirectory = serverPath;
            }

            // Save dir
            string savePath = Settings.Default.SavePath;
            if (!String.IsNullOrWhiteSpace(savePath) && Directory.Exists(savePath))
            {
                SaveDirTextBox.Text = savePath;
                saveDirectory = savePath;
            }

            // Server name
            string serverName = Settings.Default.ServerName;
            if (!String.IsNullOrWhiteSpace(serverName))
            {
                ServerNameTextBox.Text = serverName;
            }

            // Password
            string password = Settings.Default.ServerPassword;
            if (!String.IsNullOrWhiteSpace(password))
            {
                ServerPasswordTextBox.Password = password;
            }

            // Port
            int serverPort = Settings.Default.ServerPort;
            if (serverPort != 0)
            {
                ServerPortNUD.Value = serverPort;
            } else {
                ServerPortNUD.Value = 2456;
            }
            #endregion

            #region Check Worlds
            UpdateWorlds();

            if(WorldsDropDown.Items.Count > 0)
            {
                foreach(string world in WorldsDropDown.Items)
                {
                    int i = 0;
                    if (world == Settings.Default.PreviousWorld)
                    {
                        WorldsDropDown.SelectedIndex = i;
                        break;
                    }
                    i++;
                }
            }
            #endregion

            syncContext = SynchronizationContext.Current;

            IsServerSetupValid();

            // TODO: Verify path before doing this
            adminView.LoadEntriesFromFile(ValheimManager.GetListPath(Enums.ListType.Admin));
            permittedView.LoadEntriesFromFile(ValheimManager.GetListPath(Enums.ListType.Permitted));
            bannedView.LoadEntriesFromFile(ValheimManager.GetListPath(Enums.ListType.Banned));

            ValheimServer.OnServerStarted += ValheimServer_OnServerStarted;
            ValheimServer.OnServerStopped += ValheimServer_OnServerStopped;
            ValheimServer.OnOutputReceived += ValheimServer_OnOutputReceived;
            ValheimServer.OnServerStateChanged += ValheimServer_OnServerStateChanged;

            Scheduling.ScheduledTask task1 = new Scheduling.ScheduledTask { Days = Scheduling.Days.All, Task = Scheduling.TaskKind.Restart, Time = DateTime.Now };
            Scheduling.ScheduledTask task2 = new Scheduling.ScheduledTask { Days = Scheduling.Days.Weekdays, Task = Scheduling.TaskKind.Update, Time = DateTime.Now };
            Scheduling.ScheduledTask task3 = new Scheduling.ScheduledTask { Days = Scheduling.Days.Weekends, Task = Scheduling.TaskKind.Restart, Time = DateTime.Now };
            Scheduling.ScheduledTask task4 = new Scheduling.ScheduledTask { Days = Scheduling.Days.Wednesday, Task = Scheduling.TaskKind.UpdateAndRestart, Time = DateTime.Now };
            foreach (var task in new Scheduling.ScheduledTask[] { task1, task2, task3, task4 })
            {
                ScheduleGrid.Items.Add(task);
            }
            ScheduleGrid.Items.Refresh();
        }
        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save Location
            if (this.Left != Settings.Default.StartX || this.Top != Settings.Default.StartY)
            {
                Settings.Default.StartX = this.Left;
                Settings.Default.StartY = this.Top;
                Settings.Default.Save();
            }

            // Save Size
            if (this.ActualWidth != Settings.Default.SizeX || this.ActualHeight > Settings.Default.SizeY)
            {
                Settings.Default.SizeX = this.ActualWidth;
                Settings.Default.SizeY = this.ActualHeight;
                Settings.Default.Save();
            }

            // World Name
            if (!String.IsNullOrWhiteSpace(selectedWorld))
            {
                if (selectedWorld != Settings.Default.PreviousWorld)
                {
                    Settings.Default.PreviousWorld = selectedWorld;
                    Settings.Default.Save();
                }
            }

            // Password
            if (!String.IsNullOrWhiteSpace(ServerPasswordTextBox.Password))
            {
                if (ServerPasswordTextBox.Password != Settings.Default.ServerPassword)
                {
                    Settings.Default.ServerPassword = ServerPasswordTextBox.Password;
                    Settings.Default.Save();
                }
            }

            if (serverProcess != null)
            {
                try
                {
                    Process process = Process.GetProcessById(serverProcess.Id);
                    if (process != null)
                    {
                        MessageBoxResult result = MessageBox.Show("WARNING! You're still hosting a Valheim Dedicated Server, closing this program will also close the server.\n\nAre you sure you wish to continue?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        switch (result)
                        {
                            case MessageBoxResult.Yes:
                                // TODO: Send input (CTRL+C) instead of outright killing it
                                process.Kill();
                                process.WaitForExit();
                                this.Close();
                                return;
                            case MessageBoxResult.No:
                                e.Cancel = true;
                                return;
                        }
                    }
                } catch (Exception ex) {
                    MessageBox.Show($"An error occurred when trying to stop the server process!\n\nMessage:{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    //this.Close();
                }
            }
        }
        private void MetroWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DummyTab.Width = this.ActualWidth - 580 - 5; // Sum of tabs, minus an extra 5 for margin, nasty way of doing this but eh, it works
        }
        #endregion

        #region Control Event Handlers
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender == WorldNameTextBox || sender == WorldSeedTextBox)
            {
                if (WorldNameTextBox.Text.Length > 0 || WorldSeedTextBox.Text.Length > 0)
                    WorldsDropDown.IsEnabled = false;
                else
                    WorldsDropDown.IsEnabled = true;
            }

            if (sender == ServerNameTextBox)
            {
                string name = ServerNameTextBox.Text;
                if (!String.IsNullOrWhiteSpace(name) && name != Settings.Default.ServerName)
                {
                    Settings.Default.ServerName = name;
                    Settings.Default.Save();
                }
            }

            IsServerSetupValid();
        }
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            #region Console/List Button Styling
            List<Button> toggleButtons = new List<Button>() { NetworkConsoleButton, DebugConsoleButton, WorldGenConsoleButton, PlayerListButton, AdminButton, PermittedButton, BannedButton };

            if (toggleButtons.Contains(sender))
            {
                var accentBrush = (Brush)FindResource("MahApps.Brushes.Accent");
                var textBrush = (Brush)FindResource("MahApps.Brushes.Text");

                foreach(Button button in toggleButtons)
                {
                    if (sender == button)
                    {
                        button.Foreground = accentBrush;
                        button.BorderBrush = accentBrush;
                        button.BorderThickness = new Thickness(1);
                    } else {
                        button.Foreground = textBrush;
                        button.BorderBrush = null;
                        button.BorderThickness = new Thickness(0);
                    }
                }
            }
            #endregion

            #region List Buttons
            if (sender == AdminButton)
            {
                LeTransit.Content = adminView;
            }
            if (sender == PermittedButton)
            {
                LeTransit.Content = permittedView;
            }
            if (sender == BannedButton)
            {
                LeTransit.Content = bannedView;
            }
            if (sender == PlayerListButton)
            {
                LeTransit.Content = playerView;
            }
            #endregion

            #region Console Buttons
            if (sender == NetworkConsoleButton)
            {
                LeTransit.Content = networkConsole;
                currentConsole = Enums.ConsoleType.Network;
                ClearBadgeForButton(NetworkConsoleButton);
                return;
            }
            if (sender == DebugConsoleButton)
            {
                LeTransit.Content = debugConsole;
                currentConsole = Enums.ConsoleType.Debug;
                ClearBadgeForButton(DebugConsoleButton);
                return;
            }
            if (sender == WorldGenConsoleButton)
            {
                LeTransit.Content = worldConsole;
                currentConsole = Enums.ConsoleType.WorldGen;
                ClearBadgeForButton(WorldGenConsoleButton);
                return;
            }
            #endregion

            #region Setting Tab Buttons
            if (sender == SteamButton)
            {
                if (SteamManager.SteamCMD.GetSteamCMDState() != SteamManager.SteamCMD.SteamCMDState.Installed)
                    SteamManager.SteamCMD.DownloadAndInstall();
                //else
                    // TODO: Uninstall SteamCMD?
            }

            if (sender == SteamUpdate)
            {
                if (ValheimServer.ValidatePath(workingDirectory))
                {
                    var version = SteamManager.Server.GetVersion(workingDirectory);
                    var latest = SteamManager.Server.GetLatestVersion();

                    if (version == latest)
                    {
                        MessageBox.Show("Latest version is already installed, not update necessary.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    } else {
                        MessageBoxResult mbr = MessageBox.Show("Update Available!", "Update?", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (mbr == MessageBoxResult.Yes)
                            await SteamManager.Server.UpdateValdiate(workingDirectory);
                        else
                            return;
                    }
                }
            }
            #endregion

            if (sender == ServerDirButton)
            {
                using (WinForms.FolderBrowserDialog fbd = new WinForms.FolderBrowserDialog())
                {
                    fbd.RootFolder = Environment.SpecialFolder.MyComputer;
                    fbd.ShowNewFolderButton = true;
                    fbd.Description = "Browse to the Valheim Dedicated Server directory";

                    if (fbd.ShowDialog() == WinForms.DialogResult.OK)
                    {
                        string path = fbd.SelectedPath;
                        if (!String.IsNullOrWhiteSpace(path))
                        {
                            // Make sure the directory exists
                            if (Directory.Exists(path) && ValheimManager.IsValidServerDirectory(path))
                            {
                                // Save the location
                                ServerDirTextBox.Text = path;
                                workingDirectory = path;
                                Settings.Default.ServerPath = path;
                                Settings.Default.Save();

                                UpdateWorlds();
                            } else {
                                if (!ValheimManager.IsValidServerDirectory(path))
                                    MessageBox.Show("Specified directory does not seem to be a valid directory! Make sure you're choosing the folder containing the Valheim server files.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                                else
                                    MessageBox.Show("Specified directory does not seem to Exist!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                    } else {
                        // User cancelled
                    }
                }
            }
            if (sender == SaveDirButton)
            {
                using (WinForms.FolderBrowserDialog fbd = new WinForms.FolderBrowserDialog())
                {
                    fbd.RootFolder = Environment.SpecialFolder.MyComputer;
                    fbd.ShowNewFolderButton = true;
                    fbd.Description = "Choose a Directory";

                    if (fbd.ShowDialog() == WinForms.DialogResult.OK)
                    {
                        string path = fbd.SelectedPath;
                        if (Directory.Exists(path))
                        {
                            SaveDirTextBox.Text = path;
                            saveDirectory = path;
                            Settings.Default.SavePath = path;
                            Settings.Default.Save();

                            UpdateWorlds();
                        }
                    } else {
                        // User cancelled
                    }
                }
            }
            if (sender == StartButton)
            {
                if (!String.IsNullOrWhiteSpace(workingDirectory) && File.Exists(Path.Combine(workingDirectory, "valheim_server.exe")))
                {
                    if (!ValheimServer.IsServerRunning())
                    {
                        // Check all parameters before we start the serveroni
                        await ValheimServer.StartAsync(new ValheimServer.ServerSettings {
                            ServerPath = Path.Combine(workingDirectory, "valheim_server.exe"),
                            ServerName = ServerNameTextBox.Text,
                            ServerPassword = ServerPasswordTextBox.Password,
                            ServerPort = (int)ServerPortNUD.Value,
                            SaveDirectory = null,
                            WorldName = WorldNameTextBox.Text
                        });
                    } else {
                        if (MessageBox.Show("Are you sure you wish to stop the server?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                        {
                                ValheimServer.Stop();
                        } else {
                            return;
                        }
                    }
                } else {
                    MessageBox.Show("Error, unable to locate valheim_server.exe!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void WorldsDropDown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender == WorldsDropDown)
            {
                if (WorldsDropDown.SelectedIndex >= 0 && WorldsDropDown.SelectedItem != null)
                {
                    selectedWorld = (string)WorldsDropDown.SelectedItem;
                }

                IsServerSetupValid();
            }
        }
        private void ServerPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            IsServerSetupValid();
        }
        private void ServerPortNUD_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            // Redundant, but hey, maybe will add more NUDs later
            if (sender == ServerPortNUD)
            {
                if (ServerPortNUD.Value != Settings.Default.ServerPort)
                {
                    Settings.Default.ServerPort = (int)ServerPortNUD.Value;
                    Settings.Default.Save();
                }
            }
        }
        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            Hyperlink link = (Hyperlink)e.OriginalSource;
            Process.Start($"http://steamcommunity.com/profiles/{link.NavigateUri.ToString()}");
        }
        #endregion

        #region Keybindings
        private void MW_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                // Reset window location
                case Key.R:
                    if (e.KeyboardDevice.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
                    {
                        this.Width = GridLength.Auto.Value;
                        this.Height = GridLength.Auto.Value;
                        double screenWidth = SystemParameters.PrimaryScreenWidth;
                        double screenHeight = SystemParameters.PrimaryScreenHeight;
                        double windowWidth = this.Width;
                        double windowHeight = this.Height;
                        this.Left = (screenWidth / 2) - (windowWidth / 2);
                        this.Top = (screenHeight / 2) - (windowHeight / 2);
                    }
                    break;
            }
        }
        #endregion

        #region StatusBar Management
        private bool IsServerSetupValid()
        {
            // Working Directory (path to server)
            if (String.IsNullOrWhiteSpace(workingDirectory))
            {
                SetStatusBarText("Server Status: Invalid Setup - working directory not set! Browse to valheim_server.exe");
                SetThemeCombination("Dark", "Red");
                return false;
            }

            // Server Name
            if (String.IsNullOrWhiteSpace(ServerNameTextBox.Text))
            {
                SetStatusBarText("Server Status: Invalid Setup - server name not set!");
                SetThemeCombination("Dark", "Red");
                return false;
            }

            // Server Password
            if (String.IsNullOrWhiteSpace(ServerPasswordTextBox.Password) || ServerPasswordTextBox.Password.Length < 5)
            {
                SetStatusBarText("Server Status: Invalid Setup - server password not valid! (must be minimum 5 character)");
                SetThemeCombination("Dark", "Red");
                return false;
            }

            // World Selection
            if (WorldsDropDown.Items.Count > 0)
            {
                if (WorldsDropDown.SelectedItem == null)
                {
                    SetStatusBarText("Server Status: Invalid Setup - no world selected!");
                    SetThemeCombination("Dark", "Red");
                    return false;
                }
            } else {
                if (String.IsNullOrWhiteSpace(WorldNameTextBox.Text))
                {
                    SetStatusBarText("Server Status: Invalid Setup - world name invalid or not set!");
                    SetThemeCombination("Dark", "Red");
                    return false;
                }

                SetStatusBarText("Server Status: Invalid Setup - no worlds found! Choose another folder or enter a name to create a new one");
                SetThemeCombination("Dark", "Red");
                return false;
            }

            SetStatusBarText("Server Status: Setup Valid - Ready!");
            SetThemeCombination("Dark", "Green");
            return true;
        }
        #endregion

        private void UpdateCheck_Click(object sender, RoutedEventArgs e)
        {
            ClickOnceManager.CheckForUpdate();
        }

        // TODO: Version checking, it's slow af
        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl)
            {
                var tab = (TabItem)MainTabControl.SelectedItem;

                if (tab == null)
                {
                    e.Handled = true;
                    return;
                }

                if (tab == SettingsTab)
                {
                    if (SteamManager.SteamCMD.GetSteamCMDState() == SteamManager.SteamCMD.SteamCMDState.Installed)
                    {
                        SteamButton.Content = "Uninstall SteamCMD";
                    } else {
                        SteamButton.Content = "Install SteamCMD";
                    }

                    e.Handled = true;
                }

                if (tab == AboutTab)
                {
                    VersionLabel.Text = $"Version: {ClickOnceManager.CurrentVersion}";
                    ThemeManager.Current.ChangeTheme(MyPage, ThemeManager.Current.DetectTheme(this));
                }
            }
        }
    }
}
