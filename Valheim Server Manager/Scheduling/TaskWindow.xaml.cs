using ControlzEx.Theming;
using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Valheim_Server_Manager.Scheduling
{
    /// <summary>
    /// Interaction logic for TaskWindow.xaml
    /// </summary>
    public partial class TaskWindow : MetroWindow
    {
        public TaskWindow()
        {
            InitializeComponent();
            ThemeManager.Current.ChangeTheme(this, Utility.UtilityMethods.ThemeManager.CurrentTheme);
        }

        private void CenterOwner()
        {
            if (Owner != null)
            {
                double top = Owner.Top + ((Owner.ActualHeight - this.ActualHeight) / 2);
                double left = Owner.Left + ((Owner.ActualWidth - this.ActualWidth) / 2);

                this.Top = top < 0 ? 0 : top;
                this.Left = left < 0 ? 0 : left;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender == ConfirmButton)
            {
                DialogResult = true;
                this.Close();
            }

            if (sender == CancelButton)
            {
                DialogResult = false;
                this.Close();
            }
        }

        private void TaskManagerWindow_ContentRendered(object sender, EventArgs e)
        {
            CenterOwner();
        }
    }
}
