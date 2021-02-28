using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Valheim_Server_Manager
{
    public class ConsoleMessage
    {
        public string OriginalMessage { get; set; }
        public string Message { get; set; }
        public SolidColorBrush Brush { get; set; } = Brushes.White;
        public FontWeight Weight { get; set; } = FontWeights.Normal;
        public double Size { get; set; } = 12;
        public Enums.MessageType Type = Enums.MessageType.None;
        public int Index { get; set; }

        public ConsoleMessage(string message, SolidColorBrush brush = null, FontWeight? weight = null, double size = 12)
        {
            // TODO: In case of Light theme, use other colour...
            if (brush == null)
                brush = Brushes.White;

            if (!weight.HasValue)
                weight = FontWeights.Normal;

            OriginalMessage = message;
            Message = OriginalMessage;

            Type = MessageParser.CheckType(message);
            Message = MessageParser.Parse(message, Type);
        }
    }
}
