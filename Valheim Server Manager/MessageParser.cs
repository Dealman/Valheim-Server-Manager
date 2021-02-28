using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Valheim_Server_Manager
{
    public static class MessageParser
    {
        private static Regex rxSteamID = new Regex(@"(\d{17})", RegexOptions.Compiled);
        private static Regex rxCharName = new Regex(@"from\s(.+)\s:", (RegexOptions.Compiled | RegexOptions.IgnoreCase));
        private static Regex rxCharID = new Regex(@"(\S\d{5,16}):1", RegexOptions.Compiled); // Not sure how long the CharID can be, so setting a range of 5 to 16
        // (\S\d{5,16}):(\d{1,10}) new

        public static string[] debugList = new string[] { "Debug", "Line:", "Gfx", "graphics device", "RearBig", "Autodesk", "only_renders", "HDR", "Calc time" };
        public static string[] generationList = new string[] { "Placed locations", "Zonesystem", "DungeonDB", "river", "River", "mountain", "lakes", "lake"};
        public static string[] errorList = new string[] { "Failed to find APPID", "APPID:0", "Invalid APPID", "Steam is not initialized" };
        public static string[] connectionList = new string[] { "Got session request from", "Got handshake from client", "New peer connected", "Got character ZDOID" };
        public static string[] criticalErrors = new string[] { "The password is too short", "Invalid APPID" };

        private static string[] worldGenArray = new string[] { "generator seed", "mountain", "mountains", "lake", "lakes", "Rivers", "River", "Placed", "world", "my id", "locations", "Zonesystem", "DungeonDB", };
        //  Peer 76561197986562417 has wrong password
        //  RPC_Disconnect 
        //  Disposing socket
        //  Closing socket 76561197986562417
        /*
            02/25/2021 01:41:41: Got session request from 76561197986562417
            02/25/2021 01:41:44: Got handshake from client 76561197986562417
            
            02/25/2021 01:41:46: Server: New peer connected,sending global keys
            02/25/2021 01:42:04: Got character ZDOID from Ubbe : 501305756:1
            02/25/2021 01:43:11: Got character ZDOID from Ubbe : 0:0
            02/25/2021 01:43:11: Got character ZDOID from Ubbe : 501305756:75
         * */

        // VERSION check their:0.146.11  mine:0.146.8
        // Peer 76561198103639261 has incompatible version, mine:0.146.8 remote 0.146.11

        private static Dictionary<string, string> networkMessages = new Dictionary<string, string>(){
            // TODO: Add message for version checking, test it - 02/25/2021 01:41:46: VERSION check their:0.145.6  mine:0.145.6
            {"Got session request from", "[NETWORK]: {steamID} - is trying to connect."},
            {"Got handshake from client", "[NETWORK]: {steamID} - received handshake."},
            {"New peer connected", "[NETWORK]: Client successfully connected!"},
            {"Got character ZDOID", ""}, // TODO gotta prevent 0:0 <- detect as death instead
            {"has wrong password", "[NETWORK]: {steamID} entered the wrong password, closing connection." },
            {"Closing socket", "[NETWORK]: {steamID} - disconnected." }
        };

        public static string Parse(string message, Enums.MessageType type)
        {
            string time = DateTime.Now.ToString("hh:mm:ss");
            string date = DateTime.Now.ToString("MM/dd/yy");

            if (type == Enums.MessageType.PlayerConnect)
            {
                string newMessage = message;

                foreach(KeyValuePair<string, string> entry in networkMessages)
                {
                    if (message.Contains(entry.Key))
                    {
                        newMessage = entry.Value;

                        // SteamID
                        if (newMessage.Contains("{steamID}"))
                        {
                            Match match = rxSteamID.Match(message);
                            if (!String.IsNullOrWhiteSpace(match.Value))
                                newMessage = newMessage.Replace("{steamID}", match.Value);
                            else
                                newMessage = "<<IGNORE>>";

                            break;
                        }

                        // CharID
                    }
                }

                return ($"{date} {time}: "+newMessage);
            }

            return message;
        }

        public static Enums.MessageType CheckType(string message)
        {
            // Most messages will be various debug messages, check those first
            if (debugList.Any(x => message.Contains(x)))
                return Enums.MessageType.Debug;

            //if (generationList.Any(x => message.Contains(x)))
            //    return Enums.MessageType.Debug;

            if (errorList.Any(x => message.Contains(x)))
                return Enums.MessageType.Error;

            if (networkMessages.Any(x => message.Contains(x.Key)))
                return Enums.MessageType.Network;

            if (worldGenArray.Any(x => message.Contains(x)))
                return Enums.MessageType.WorldGen;

            // TODO: Check this
            if (message.Contains("Closing socket"))
                return Enums.MessageType.PlayerDisconnect;

            return Enums.MessageType.Debug;
        }

        public static Enums.MessageType GetMessageType(string message)
        {
            if (debugList.Any(x => message.Contains(x)))
                return Enums.MessageType.Debug;

            if (message.Contains("Steam manager on destroy"))
                return Enums.MessageType.ServerExit;

            //if (connectionList.Any(x => message.Contains(x)))
            //    return Enums.MessageType.PlayerConnect;

            if (networkMessages.Any(x => message.Contains(x.Key)))
                return Enums.MessageType.PlayerConnect;

            if (message.Contains("Game server connected"))
                return Enums.MessageType.ServerOnline;

            if (message.Contains("Closing socket"))
                return Enums.MessageType.PlayerDisconnect;

            if (errorList.Any(x => message.Contains(x)))
                return Enums.MessageType.Error;

            return Enums.MessageType.Normal;
        }

        public static Color GetMessageColor(Enums.MessageType messageType)
        {
            switch (messageType)
            {
                case Enums.MessageType.Debug:
                    return Colors.Gray;

                case Enums.MessageType.Error:
                    return Colors.Red;

                case Enums.MessageType.PlayerConnect:
                    return Colors.GreenYellow;

                case Enums.MessageType.PlayerDisconnect:
                    return Colors.GreenYellow;

                case Enums.MessageType.ServerOnline:
                    return Colors.Green;

                case Enums.MessageType.ServerExit:
                    return Colors.Red;

                default:
                    return Colors.White;
            }
        }

        public static SolidColorBrush GetMessageBrush(Enums.MessageType messageType)
        {
            switch (messageType)
            {
                case Enums.MessageType.Debug:
                    return Brushes.Gray;

                case Enums.MessageType.Error:
                    return Brushes.DarkRed;

                case Enums.MessageType.PlayerConnect:
                    return Brushes.Green;

                case Enums.MessageType.PlayerDisconnect:
                    return Brushes.Orange;

                case Enums.MessageType.ServerOnline:
                    return Brushes.GreenYellow;

                case Enums.MessageType.ServerExit:
                    return Brushes.Red;

                default:
                    return Brushes.White;
            }
        }
    }
}

// Player Connect:
// 02/17/2021 02:34:32: Server: New peer connected,sending global keys
// 02/17/2021 02:34:44: Got character ZD0ID from Ubbe : 420493114:1
// Player Exit:
// 02/17/2021 02:44:48: Closing socket 76561197986562417 (SteamID)
// Disposing socket
// Closing socket 0