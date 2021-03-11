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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Valheim_Server_Manager.Scheduling
{
    public enum SelectionType
    {
        None,
        All,
        Weekdays,
        Weekends
    }

    public partial class CreateTaskPage : Page
    {
        ScheduledTask task = new ScheduledTask();
        CheckBox[] allBoxes;

        public CreateTaskPage()
        {
            InitializeComponent();
            ThemeManager.Current.ChangeTheme(this, Utility.UtilityMethods.ThemeManager.CurrentTheme);
            allBoxes = new CheckBox[]{ MondayCB, TuesdayCB, WednesdayCB, ThursdayCB, FridayCB, SaturdayCB, SundayCB };
        }

        private void SetSelectedDays(SelectionType type)
        {
            switch (type)
            {
                case SelectionType.None:
                    foreach (var checkbox in allBoxes)
                    {
                        checkbox.IsChecked = false;
                    }
                    break;
                case SelectionType.All:
                    foreach (var checkbox in allBoxes)
                    {
                        checkbox.IsChecked = true;
                    }
                    break;
                case SelectionType.Weekdays:
                    foreach (var checkbox in allBoxes)
                    {
                        if (checkbox == MondayCB || checkbox == TuesdayCB || checkbox == WednesdayCB || checkbox == ThursdayCB || checkbox == FridayCB)
                        {
                            checkbox.IsChecked = true;
                        } else {
                            checkbox.IsChecked = false;
                        }
                    }
                    break;
                case SelectionType.Weekends:
                    foreach (var checkbox in allBoxes)
                    {
                        if (checkbox == SaturdayCB || checkbox == SundayCB)
                        {
                            checkbox.IsChecked = true;
                        } else {
                            checkbox.IsChecked = false;
                        }
                    }
                    break;
            }
            UpdateTask();
        }

        private void UpdateTask()
        {
            task.Days = (
                ((MondayCB.IsChecked.Value) ? Days.Monday : Days.None) | 
                ((TuesdayCB.IsChecked.Value) ? Days.Tuesday : Days.None) |
                ((WednesdayCB.IsChecked.Value) ? Days.Wednesday : Days.None) |
                ((ThursdayCB.IsChecked.Value) ? Days.Thursday : Days.None) |
                ((FridayCB.IsChecked.Value) ? Days.Friday : Days.None) |
                ((SaturdayCB.IsChecked.Value) ? Days.Saturday : Days.None) |
                ((SundayCB.IsChecked.Value) ? Days.Sunday : Days.None)
            );

            task.Time = TimePickerControl.SelectedDateTime.GetValueOrDefault();

            if (RestartRB.IsChecked.GetValueOrDefault())
                task.Task = TaskKind.Restart;
            if (UpdateRB.IsChecked.GetValueOrDefault())
                task.Task = TaskKind.UpdateAndRestart;
            if (BothRB.IsChecked.GetValueOrDefault())
                task.Task = TaskKind.UpdateAndRestart;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender == AllButton)
                SetSelectedDays(SelectionType.All);

            if (sender == WeekdaysButton)
                SetSelectedDays(SelectionType.Weekdays);

            if (sender == WeekendsButton)
                SetSelectedDays(SelectionType.Weekends);

            if (sender == ClearButton)
                SetSelectedDays(SelectionType.None);

            if (sender == MondayCB || sender == TuesdayCB || sender == WednesdayCB || sender == ThursdayCB || sender == FridayCB || sender == SaturdayCB || sender == SundayCB)
                UpdateTask();
        }

        private void TimePickerControl_SelectedDateTimeChanged(object sender, RoutedPropertyChangedEventArgs<DateTime?> e)
        {
            UpdateTask();
        }
    }
}
