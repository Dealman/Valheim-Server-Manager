using ControlzEx.Theming;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Valheim_Server_Manager.Utility
{
    public static class UtilityMethods
    {
        public static class ThemeManager
        {
            private static MainWindow mw = (Application.Current.MainWindow as MainWindow);
            public static Theme CurrentTheme { get { return ControlzEx.Theming.ThemeManager.Current.DetectTheme(mw); } }

            public static void PrintThemes()
            {
                var themes = ControlzEx.Theming.ThemeManager.Current.Themes;

                if (themes.Count > 0)
                {
                    foreach(Theme theme in themes)
                    {
                        Debug.WriteLine($"Found Theme: {theme.DisplayName}");
                    }
                }
            }
        }
    }
}
