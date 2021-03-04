using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Valheim_Server_Manager
{
    /// <summary>
    /// Interaction logic for PlayerListControl.xaml
    /// </summary>
    public partial class PlayerListControl : UserControl
    {
        ObservableCollection<Player> PlayerCollection { get; set; } = new ObservableCollection<Player>();

        public PlayerListControl()
        {
            InitializeComponent();
            DataContext = PlayerCollection;
        }

        public void AddPlayer(Player player)
        {
            if (!PlayerCollection.Contains(player))
                PlayerCollection.Add(player);

            PlayerDataGrid.Items.Refresh();
        }

        public void RemovePlayer(Player player)
        {
            if (PlayerCollection.Contains(player))
                PlayerCollection.Remove(player);

            PlayerDataGrid.Items.Refresh();
        }

        public void RemovePlayer(string steamID)
        {
            if (PlayerCollection.Count > 0)
            {
                Player playerToRemove = null;

                foreach(Player player in PlayerCollection)
                {
                    if (player.SteamID == steamID)
                    {
                        playerToRemove = player;
                        break;
                    }
                }

                if (playerToRemove != null)
                    PlayerCollection.Remove(playerToRemove);

                PlayerDataGrid.Items.Refresh();
            }
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            Hyperlink link = (Hyperlink)e.OriginalSource;

            if (link != null)
            {
                Process.Start($"http://steamcommunity.com/profiles/{link.NavigateUri.ToString()}");
            }
        }
    }
}
