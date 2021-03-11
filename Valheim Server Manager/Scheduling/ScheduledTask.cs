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
        None = 0,
        Monday = 1,
        Tuesday = 2,
        Wednesday = 4,
        Thursday = 8,
        Friday = 16,
        Saturday = 32,
        Sunday = 64,
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
