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

namespace Valheim_Server_Manager
{
    /// <summary>
    /// Interaction logic for MessageConsole.xaml
    /// </summary>
    public partial class MessageConsole : UserControl
    {
        public string Title { get; set; } = "Message Console";
        public Enums.ConsoleType Type { get; set; } = Enums.ConsoleType.Normal;

        public MessageConsole()
        {
            InitializeComponent();
            DataContext = this;
        }

        public void AddMessage(string text, Brush msgBrush = null, FontWeight? weight = null, double size = 12)
        {
            if (String.IsNullOrWhiteSpace(text))
                return;

            if (msgBrush == null)
                msgBrush = Brushes.White;

            if (weight == null)
                weight = FontWeights.Normal;

            this.Dispatcher.Invoke(() =>
            {
                Run r = new Run
                {
                    Text = text+"\n",
                    Foreground = msgBrush,
                    FontWeight = weight.GetValueOrDefault(),
                    FontSize = size
                };

                MessageContainer.Inlines.Add(r);
                ContainerScroller.ScrollToEnd();
            });
        }

        private void Border_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.C:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                        MessageContainer.CopyToClipboard();
                    break;

                case Key.A:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                        MessageContainer.SelectAll();
                    break;
            }
        }
    }
}
