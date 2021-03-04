using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Valheim_Server_Manager
{
    public class FixedBadged : Badged
    {
        // Badge would randomly be clipping, this override prevents this behaviour
        protected override Geometry GetLayoutClip(Size layoutSlotSize)
        {
            return null;
        }
    }
}
