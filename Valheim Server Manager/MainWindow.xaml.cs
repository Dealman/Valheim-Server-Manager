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
    internal class DispatcherWinFormsCompatAdapter : ISynchronizeInvoke
    {
        #region IAsyncResult implementation
        private class DispatcherAsyncResultAdapter : IAsyncResult
        {
            private DispatcherOperation m_op;
            private object m_state;

            public DispatcherAsyncResultAdapter(DispatcherOperation operation)
            {
                m_op = operation;
            }

            public DispatcherAsyncResultAdapter(DispatcherOperation operation, object state)
               : this(operation)
            {
                m_state = state;
            }

            public DispatcherOperation Operation
            {
                get { return m_op; }
            }

            #region IAsyncResult Members

            public object AsyncState
            {
                get { return m_state; }
            }

            public WaitHandle AsyncWaitHandle
            {
                get { return null; }
            }

            public bool CompletedSynchronously
            {
                get { return false; }
            }

            public bool IsCompleted
            {
                get { return m_op.Status == DispatcherOperationStatus.Completed; }
            }

            #endregion
        }
        #endregion
        private Dispatcher m_disp;
        public DispatcherWinFormsCompatAdapter(Dispatcher dispatcher)
        {
            m_disp = dispatcher;
        }
        #region ISynchronizeInvoke Members

        public IAsyncResult BeginInvoke(Delegate method, object[] args)
        {
            if (args != null && args.Length > 1)
            {
                object[] argsSansFirst = GetArgsAfterFirst(args);
                DispatcherOperation op = m_disp.BeginInvoke(DispatcherPriority.Normal, method, args[0], argsSansFirst);
                return new DispatcherAsyncResultAdapter(op);
            }
            else
            {
                if (args != null)
                {
                    return new DispatcherAsyncResultAdapter(m_disp.BeginInvoke(DispatcherPriority.Normal, method, args[0]));
                }
                else
                {
                    return new DispatcherAsyncResultAdapter(m_disp.BeginInvoke(DispatcherPriority.Normal, method));
                }
            }
        }

        private static object[] GetArgsAfterFirst(object[] args)
        {
            object[] result = new object[args.Length - 1];
            Array.Copy(args, 1, result, 0, args.Length - 1);
            return result;
        }

        public object EndInvoke(IAsyncResult result)
        {
            DispatcherAsyncResultAdapter res = result as DispatcherAsyncResultAdapter;
            if (res == null)
                throw new InvalidCastException();

            while (res.Operation.Status != DispatcherOperationStatus.Completed || res.Operation.Status == DispatcherOperationStatus.Aborted)
            {
                Thread.Sleep(50);
            }

            return res.Operation.Result;
        }

        public object Invoke(Delegate method, object[] args)
        {
            if (args != null && args.Length > 1)
            {
                object[] argsSansFirst = GetArgsAfterFirst(args);
                return m_disp.Invoke(DispatcherPriority.Normal, method, args[0], argsSansFirst);
            }
            else
            {
                if (args != null)
                {
                    return m_disp.Invoke(DispatcherPriority.Normal, method, args[0]);
                }
                else
                {
                    return m_disp.Invoke(DispatcherPriority.Normal, method);
                }
            }
        }

        public bool InvokeRequired
        {
            get { return m_disp.Thread != Thread.CurrentThread; }
        }

        #endregion
    }

    public partial class MainWindow : MetroWindow
    {
        private string workingDirectory;
        private string saveDirectory;
        private int playerCount;
        private string selectedWorld;
        private SynchronizationContext syncContext;
        private Process serverProcess;
        private Enums.DebugLevel debugLevel = Enums.DebugLevel.Low;
        private Enums.ServerState serverState = Enums.ServerState.Invalid;
        private List<Player> playerList = new List<Player>();
        private Regex rxSteamID = new Regex(@"(\d{17})", RegexOptions.Compiled);
        private Regex rxCharName = new Regex(@"from\s(.+)\s:", (RegexOptions.Compiled | RegexOptions.IgnoreCase));
        private Regex rxCharID = new Regex(@"(\S\d{5,16}):1", RegexOptions.Compiled); // Not sure how long the CharID can be, so setting a range of 5 to 16
        private Enums.ConsoleType currentConsole = Enums.ConsoleType.Normal;
        private Enums.ListType currentList = Enums.ListType.None;

        public MainWindow()
        {
            InitializeComponent();

            // TODO: Move to Loaded/ContentRendered
            ValheimServer.OnServerStarted += ValheimServer_OnServerStarted;
            ValheimServer.OnServerStopped += ValheimServer_OnServerStopped;
            ValheimServer.OnOutputReceived += ValheimServer_OnOutputReceived;
            ValheimServer.OnServerStateChanged += ValheimServer_OnServerStateChanged;
        }

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
            //ParseMessage(outputMessage);
            if (!String.IsNullOrWhiteSpace(outputMessage))
            {
                ConsoleMessage msg = new ConsoleMessage(outputMessage);
                //if (msg.Type == Enums.MessageType.Debug)
                //    return;

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
                            NewPlayerList.AddPlayer(player);
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
                            NewPlayerList.RemovePlayer(leavingPlayer);
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
                case Enums.MessageType.Normal:
                    NormalOutputConsole.AddMessage(msg);
                    break;

                case Enums.MessageType.Network:
                    NetworkOutputConsole.AddMessage(msg);
                    break;

                case Enums.MessageType.Debug:
                    DebugOutputConsole.AddMessage(msg);
                    break;

                case Enums.MessageType.WorldGen:
                    WorldGenOutputConsole.AddMessage(msg);
                    break;
            }

            this.Dispatcher.Invoke(() =>
            {
                Run run = new Run
                {
                    Text = msg + "\n",
                    Foreground = msgColour,
                    FontWeight = msgWeight.GetValueOrDefault(),
                    FontSize = msgSize
                };

                TextThing.Inlines.Add(run);
                Scrolleroni.ScrollToEnd();
                IncrementBadge(type);
            });
        }
        private void SetActiveListView(Enums.ListType lType)
        {
            if (lType == currentList)
                return;

            List<CustomListDisplay> displayList = new List<CustomListDisplay>() { AdminListBox, BanListBox, PermitListBox };

            if (lType == Enums.ListType.None)
            {
                displayList.ForEach(x => x.Visibility = Visibility.Collapsed);
            }

            foreach(CustomListDisplay display in displayList)
            {
                if (display.Type == lType)
                    display.Visibility = Visibility.Visible;
                else
                    display.Visibility = Visibility.Collapsed;
            }

            switch (lType)
            {
                case Enums.ListType.Admin:
                    // Load File
                    //AdminListBox.LoadEntriesFromFile(@"C:\Users\Dealman\AppData\LocalLow\IronGate\Valheim\adminlist.txt");
                    //AdminBadge.Badge = AdminListBox.GetNumberOfEntries();
                    break;

                case Enums.ListType.Banned:
                    //AdminListBox.LoadEntriesFromFile(@"C:\Users\Dealman\AppData\LocalLow\IronGate\Valheim\bannedlist.txt");
                    //AdminBadge.Badge = AdminListBox.GetNumberOfEntries();
                    break;

                case Enums.ListType.Permitted:
                    //AdminListBox.LoadEntriesFromFile(@"C:\Users\Dealman\AppData\LocalLow\IronGate\Valheim\permittedlist.txt");
                    //AdminBadge.Badge = AdminListBox.GetNumberOfEntries();
                    break;
            }
        }
        private void SetActiveConsoleWindow(Enums.ConsoleType cType)
        {
            if (cType == currentConsole)
                return;

            List<MessageConsole> consoleList = new List<MessageConsole>() { NormalOutputConsole, NetworkOutputConsole, DebugOutputConsole, WorldGenOutputConsole };

            if (cType == Enums.ConsoleType.None)
            {
                consoleList.ForEach(x => x.Visibility = Visibility.Collapsed);
                return;
            }

            foreach (MessageConsole console in consoleList)
            {
                if (console.Type == cType)
                    console.Visibility = Visibility.Visible;
                else
                    console.Visibility = Visibility.Collapsed;
            }

            var accentBrush = (Brush)FindResource("MahApps.Brushes.Accent");
            var textBrush = (Brush)FindResource("MahApps.Brushes.Text");
            if (accentBrush == null || textBrush == null)
                return;

            switch (cType)
            {
                case Enums.ConsoleType.Normal:
                    currentConsole = Enums.ConsoleType.Normal;
                    ClearBadgeForButton(NormalConsoleButton);
                    break;
                case Enums.ConsoleType.Network:
                    ClearBadgeForButton(NetworkConsoleButton);
                    break;
                case Enums.ConsoleType.Debug:
                    currentConsole = Enums.ConsoleType.Debug;
                    ClearBadgeForButton(DebugConsoleButton);
                    break;
                case Enums.ConsoleType.WorldGen:
                    currentConsole = Enums.ConsoleType.WorldGen;
                    ClearBadgeForButton(WorldGenConsoleButton);
                    break;
            }
        }
        private void ClearBadgeForButton(Button button)
        {
            if (button == NormalConsoleButton)
                NormalBadge.Badge = null;

            if (button == NetworkConsoleButton)
                NetworkBadge.Badge = null;

            if (button == DebugConsoleButton)
                DebugBadge.Badge = null;

            if (button == WorldGenConsoleButton)
                WorldGenBadge.Badge = null;
        }
        private void IncrementBadge(Enums.MessageType mType)
        {
            switch (mType)
            {
                case Enums.MessageType.Normal:
                    if (NormalOutputConsole.Visibility == Visibility.Collapsed)
                        NormalBadge.Badge = (NormalBadge.Badge == null ? 1 : ((int)NormalBadge.Badge + 1));
                    return;
                case Enums.MessageType.Network:
                    if (NetworkOutputConsole.Visibility == Visibility.Collapsed)
                        NetworkBadge.Badge = (NetworkBadge.Badge == null ? 1 : ((int)NetworkBadge.Badge + 1));
                    return;
                case Enums.MessageType.Debug:
                    if (DebugOutputConsole.Visibility == Visibility.Collapsed)
                        DebugBadge.Badge = (DebugBadge.Badge == null ? 1 : ((int)DebugBadge.Badge + 1));
                    return;
                case Enums.MessageType.WorldGen:
                    if (WorldGenOutputConsole.Visibility == Visibility.Collapsed)
                        WorldGenBadge.Badge = (WorldGenBadge.Badge == null ? 1 : ((int)WorldGenBadge.Badge + 1));
                    return;
            }
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

            // TODO: This needs refactoring, just testing atm
            AdminListBox.LoadEntriesFromFile(ValheimManager.GetListPath(Enums.ListType.Admin));
            PermitListBox.LoadEntriesFromFile(ValheimManager.GetListPath(Enums.ListType.Permitted));
            BanListBox.LoadEntriesFromFile(ValheimManager.GetListPath(Enums.ListType.Banned));
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
                    //this.Close();
                }
            }
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
            List<Button> toggleButtons = new List<Button>() { NormalConsoleButton, NetworkConsoleButton, DebugConsoleButton, WorldGenConsoleButton, PlayerListButton, AdminButton, BannedButton, PermittedButton };
            
            var accentBrush = (Brush)FindResource("MahApps.Brushes.Accent");
            var textBrush = (Brush)FindResource("MahApps.Brushes.Text");

            foreach (Button button in toggleButtons)
            {
                if (sender == button)
                    button.Foreground = (accentBrush != null) ? accentBrush : Brushes.Gray;
                else
                    button.Foreground = (textBrush != null) ? textBrush : Brushes.White;

                if (sender == PlayerListButton)
                {
                    SetActiveConsoleWindow(Enums.ConsoleType.None);
                    SetActiveListView(Enums.ListType.None);
                    NewPlayerList.Visibility = Visibility.Visible;
                } else {
                    if (NewPlayerList.Visibility == Visibility.Visible)
                        NewPlayerList.Visibility = Visibility.Collapsed;
                }
            }

            #region List Buttons
            if (sender == AdminButton)
            {
                SetActiveConsoleWindow(Enums.ConsoleType.None);
                SetActiveListView(Enums.ListType.Admin);
            }

            if (sender == BannedButton)
            {
                SetActiveConsoleWindow(Enums.ConsoleType.None);
                SetActiveListView(Enums.ListType.Banned);
            }

            if (sender == PermittedButton)
            {
                SetActiveConsoleWindow(Enums.ConsoleType.None);
                SetActiveListView(Enums.ListType.Permitted);
            }
            #endregion

            #region Console Buttons
            if (sender == NormalConsoleButton)
            {
                //NormalConsoleButton.Foreground = (Brush)FindResource("MahApps.Brushes.Accent2");
                SetActiveConsoleWindow(Enums.ConsoleType.Normal);
                return;
            }
            if (sender == NetworkConsoleButton)
            {
                SetActiveConsoleWindow(Enums.ConsoleType.Network);
                return;
            }
            if (sender == DebugConsoleButton)
            {
                SetActiveConsoleWindow(Enums.ConsoleType.Debug);
                return;
            }
            if (sender == WorldGenConsoleButton)
            {
                SetActiveConsoleWindow(Enums.ConsoleType.WorldGen);
                return;
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
                        if (MessageBox.Show("Are you sure you wish to stop the server?", "Warning!", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
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

        #region ContextMenu Event Handlers
        private void tContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (!String.IsNullOrWhiteSpace(TextThing.Text))
            {
                tContextSelect.IsEnabled = true;
                tContextCopy.IsEnabled = true;
                tContextClear.IsEnabled = true;
            } else {
                tContextSelect.IsEnabled = false;
                tContextCopy.IsEnabled = false;
                tContextClear.IsEnabled = false;
            }
        }
        private void tContextItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender == tContextSelect)
            {
                if (!String.IsNullOrWhiteSpace(TextThing.Text))
                {
                    TextThing.SelectAll();
                }
            }

            if (sender == tContextCopy)
            {
                //MessageBox.Show(TextThing.SelectedText);
            }

            if (sender == tContextClear)
                TextThing.Clear();
        }
        #endregion

        #region Keybindings
        private void MW_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            //Debug.WriteLine($"Key: {e.Key.ToString()} Modifiers: {e.KeyboardDevice.Modifiers.ToString()}");

            switch (e.Key)
            {
                // Copy Text
                case Key.C:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        TextThing.CopyToClipboard();
                    }
                    break;
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
    }
}
