using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Valheim_Server_Manager.Scheduling
{
    [Flags]
    public enum Days
    {
        Monday = 0,
        Tuesday = 1,
        Wednesday = 2,
        Thursday = 4,
        Friday = 8,
        Saturday = 16,
        Sunday = 32,
        Weekdays = Monday|Tuesday|Wednesday|Thursday|Friday,
        Weekends = Saturday|Sunday,
        All = Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday
    }

    public enum TaskKind
    {
        Restart,
        Update,
        UpdateAndRestart
    }

    public class DaysConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((Days)value).HasFlag(Days.All);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    public class ScheduledTask
    {
        public Days Days { get; set; }
        public DateTime Time { get; set; }
        public TaskKind Task { get; set; }
        public bool Monday { get { return Days.HasFlag(Days.Monday); } }
        public bool Tuesday { get { return Days.HasFlag(Days.Tuesday); } }
        public bool Wednesday { get { return Days.HasFlag(Days.Wednesday); } }
        public bool Thursday { get { return Days.HasFlag(Days.Thursday); } }
        public bool Friday { get { return Days.HasFlag(Days.Friday); } }
        public bool Saturday { get { return Days.HasFlag(Days.Saturday); } }
        public bool Sunday { get { return Days.HasFlag(Days.Sunday); } }
    }
}
