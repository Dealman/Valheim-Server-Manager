using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace Valheim_Server_Manager
{
    public class RunCustom : Run
    {
        public Enums.MessageType MessageType { get; set; } = Enums.MessageType.None;
    }
}
