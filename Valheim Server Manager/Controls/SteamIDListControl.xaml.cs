using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
using MahApps.Metro.Controls.Dialogs;

namespace Valheim_Server_Manager
{
    public partial class CustomListDisplay : UserControl, INotifyPropertyChanged
    {
        public string Title { get; set; } = "My List";
        public Enums.ListType Type { get; set; }
        private int count = 0;
        public int ListCount { get { return count; } set { if (value != count) count = value; OnPropertyChanged(); } }

        private string previousPath;
        // TODO: Should check if these comments are necessary or not, with 99% likeliness they can probably be removed as they're just comments
        // depends on how Valheim reads the files /shrug
        private string[] filePrefixes = new string[]{ "", "// List admin players ID  ONE per line", "// List banned players ID  ONE per line", "// List permitted players ID ONE per line" };

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        #endregion

        public CustomListDisplay()
        {
            InitializeComponent();
        }

        #region List Related Methods
        private bool IsValidSteamID(string id)
        {
            if (!String.IsNullOrWhiteSpace(id) && long.TryParse(id, out long steamID))
                if (steamID > 76560000000000000)
                    return true;

            return false;
        }
        public int GetNumberOfEntries()
        {
            return IDListBox.Items.Count;
        }
        private async void InvalidList()
        {
            MainWindow mw = (Application.Current.MainWindow as MainWindow);

            var result = await DialogManager.ShowMessageAsync(mw, "Warning", $"The {Type.ToString()}list.txt appears to be invalid/corrupted, would you like to attempt and fix it?", MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings { AffirmativeButtonText = "Yes", NegativeButtonText = "No", DefaultButtonFocus = MessageDialogResult.Affirmative});
            if (result == MessageDialogResult.Affirmative)
            {
                FixList();
            }
        }
        private async void FixList()
        {
            if (File.Exists(previousPath))
            {
                var allLines = File.ReadLines(previousPath);
                if (allLines.Count() > 0)
                {
                    List<string> idList = new List<string>();

                    foreach (var line in allLines)
                    {
                        if (String.IsNullOrWhiteSpace(line) || line.Contains("//"))
                            continue;

                        if (IsValidSteamID(line))
                            idList.Add(line);
                    }

                    string prefix = filePrefixes[(int)Type];
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine(prefix);

                    if (idList.Count() > 0)
                    {
                        idList.ForEach(x => sb.AppendLine(x));
                        File.WriteAllText(previousPath, sb.ToString());
                        if (ValidateList())
                        {
                            MainWindow mw = (Application.Current.MainWindow as MainWindow);
                            await DialogManager.ShowMessageAsync(mw, "Information", $"Successfully fixed corrupted {Type.ToString()}list.txt");
                            LoadEntriesFromFile(previousPath);
                        } else {
                            MainWindow mw = (Application.Current.MainWindow as MainWindow);
                            await DialogManager.ShowMessageAsync(mw, "Information", "Ran into an unknown error when trying to fix the file, sorry :(");
                        }
                    } else {
                        File.WriteAllText(previousPath, prefix);
                        if (ValidateList())
                        {
                            MainWindow mw = (Application.Current.MainWindow as MainWindow);
                            await DialogManager.ShowMessageAsync(mw, "Information", $"Successfully fixed corrupted {Type.ToString()}list.txt");
                        } else {
                            MainWindow mw = (Application.Current.MainWindow as MainWindow);
                            await DialogManager.ShowMessageAsync(mw, "Information", "Ran into an unknown error when trying to fix the file, sorry :(");
                        }
                    }
                }
            }
        }
        private bool ValidateList()
        {
            if (File.Exists(previousPath))
            {
                var allLines = File.ReadLines(previousPath);

                if (allLines.Count() > 0)
                {
                    List<string> idList = new List<string>();

                    foreach(var line in allLines)
                    {
                        if (String.IsNullOrWhiteSpace(line) || line.Contains("//"))
                            continue;

                        if (IsValidSteamID(line))
                            idList.Add(line);
                        else
                            return false;
                    }

                    // List can obviously still be valid even if it contains no SteamIDs...
                    if (idList.Count() >= 0)
                        return true;
                }
            }

            return false;
        }
        #endregion

        #region File Methods
        public void SaveEntriesToFile(string path = "")
        {
            try
            {
                if (!String.IsNullOrWhiteSpace(path))
                {
                    // A custom path was provided
                } else {
                    if (File.Exists(previousPath))
                    {
                        string prefix = filePrefixes[(int)Type];

                        if (IDListBox.Items.Count > 0)
                        {
                            StringBuilder sb = new StringBuilder();
                            sb.AppendLine(prefix);

                            foreach (string item in IDListBox.Items)
                            {
                                sb.AppendLine(item);
                            }

                            File.WriteAllText(previousPath, sb.ToString());
                        }
                    }
                }
            } catch (Exception ex) {
                MessageBox.Show($"An error has occurred while trying to save a file.\n\nError Message:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        public bool LoadEntriesFromFile(string path)
        {
            if (File.Exists(path))
            {
                var allLines = File.ReadLines(path);

                if (allLines.Count() > 0)
                {
                    List<string> idList = new List<string>();

                    foreach (var line in allLines)
                    {
                        // If there's an empty line, we skip and continue
                        if (String.IsNullOrWhiteSpace(line))
                            continue;

                        if (line.Substring(0, 2) == "//")
                        {
                            // Line is a comment, assume this is ignored by Valheim?
                            // If necessary, can use Regex here
                        } else {
                            if (IsValidSteamID(line))
                            {
                                idList.Add(line);
                            } else {
                                previousPath = path;
                                InvalidList();
                                return false;
                            }
                        }
                    }

                    if (idList.Count > 0)
                    {
                        idList.ForEach(x => IDListBox.Items.Add(x));
                        previousPath = path;
                        ListCount = IDListBox.Items.Count;
                        return true;
                    } else {
                        previousPath = path;
                        return false;
                    }
                }
            }

            return false;
        }
        #endregion

        #region Control Events
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mw = (Application.Current.MainWindow as MainWindow);

            if (sender == AddButton)
            {
                var input = await DialogManager.ShowInputAsync(mw, "Add User", "Enter a valid SteamID (17 digits)");

                if (String.IsNullOrWhiteSpace(input))
                    return;

                if (!IsValidSteamID(input))
                {
                    await DialogManager.ShowMessageAsync(mw, "Error", "The SteamID you entered does not appear to be valid, make sure you're entering a 17-digit SteamID.");
                } else {
                    if (!IDListBox.Items.Contains(input))
                    {
                        IDListBox.Items.Add(input);
                        SaveEntriesToFile();
                    } else {
                        await DialogManager.ShowMessageAsync(mw, "Error", "The provided SteamID already exists!");
                    }
                }
            }

            if (sender == EditButton)
            {
                var selected = IDListBox.SelectedItem as string;

                if (!String.IsNullOrWhiteSpace(selected))
                {
                    var input = await DialogManager.ShowInputAsync(mw, "Add User", "Enter a valid SteamID (17 digits)", new MetroDialogSettings { DefaultText = selected });

                    if (input == null)
                    {
                        // User probably cancelled. TODO: Make proper use of cancellation tokens?
                    } else {
                        if (IsValidSteamID(input))
                        {
                            if (!IDListBox.Items.Contains(input))
                            {
                                IDListBox.Items[IDListBox.SelectedIndex] = input;
                                SaveEntriesToFile();
                            } else {
                                await DialogManager.ShowMessageAsync(mw, "Error", "The provided SteamID already exists!");
                            }
                        } else {
                            await DialogManager.ShowMessageAsync(mw, "Error", "The SteamID you entered does not appear to be valid, make sure you're entering a 17-digit SteamID.");
                        } 
                    }
                }
            }

            if (sender == RemoveButton)
            {
                var selected = IDListBox.SelectedItem as string;

                if (!String.IsNullOrWhiteSpace(selected))
                {
                    var result = await DialogManager.ShowMessageAsync(mw, "Remove User", $"Are you sure you wish to remove {selected} from the Admin list?", MessageDialogStyle.AffirmativeAndNegative);
                    if (result == MessageDialogResult.Affirmative)
                    {
                        IDListBox.Items.RemoveAt(IDListBox.SelectedIndex);
                        SaveEntriesToFile();
                    } else {
                        // User Cancelled.
                    }
                }
            }

            if (sender == RefreshButton)
            {
                if (!String.IsNullOrWhiteSpace(previousPath))
                {
                    IDListBox.Items.Clear();
                    LoadEntriesFromFile(previousPath);
                }
            }

            if (sender == ClearButton)
            {
                if (IDListBox.Items.Count > 0)
                {
                    var result = await DialogManager.ShowMessageAsync(mw, "Clear List", $"Are you sure you wish to clear the Admin list? This will remove all ze admins ja!", MessageDialogStyle.AffirmativeAndNegative);

                    if (result == MessageDialogResult.Affirmative)
                    {
                        IDListBox.Items.Clear();
                    } else {
                        // User Cancelled
                    }
                }
            }

            ListCount = IDListBox.Items.Count;
        }
        private void IDListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IDListBox.SelectedIndex > -1)
            {
                EditButton.IsEnabled = true;
                RemoveButton.IsEnabled = true;
            } else {
                EditButton.IsEnabled = false;
                RemoveButton.IsEnabled = false;
            }
        }
        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            Hyperlink link = (Hyperlink)e.OriginalSource;
            Process.Start($"http://steamcommunity.com/profiles/{link.NavigateUri.ToString()}");
        }
        #endregion
    }
}
