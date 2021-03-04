using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Valheim_Server_Manager
{
    public static class Enums
    {
        public enum DebugLevel
        {
            Low,
            Normal,
            Thicc,
            None
        }

        [Flags]
        public enum MessageType
        {
            None = 0,
            Normal,
            Debug,
            Error,
            PlayerConnect,
            PlayerDisconnect,
            ServerOnline,
            ServerExit,
            Network,
            WorldGen
        }

        public enum ConsoleType
        {
            None = 0,
            Normal,
            Network,
            Debug,
            WorldGen
        }

        public enum ListType
        {
            None = 0,
            Admin,
            Banned,
            Permitted
        }

        public enum ServerStateEnum
        {
            None,
            Offline,
            Closing,
            Starting,
            Online
        }

        public enum ServerExitEnum
        {
            Graceful,
            Forced,
            UnknownReason
        }

        public enum ServerState
        {
            Invalid,
            Offline,
            Valid,
            Online
        }
    }
}
