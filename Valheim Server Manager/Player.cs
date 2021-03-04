using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Valheim_Server_Manager
{
    public class Player
    {
        public string SteamID { get; set; }
        public string CharacterID { get; set; }
        public string CharacterName { get; set; }
        public bool RequestReceived { get; set; }
        public bool HandshakeReceived { get; set; }
        public bool IsConnected { get; set; }
        public DateTime JoinTime { get; set; }

        public Player(string steamID)
        {
            if (!String.IsNullOrWhiteSpace(steamID))
            {
                SteamID = steamID;
            }
        }

        public Player(string steamID, string charID, string name)
        {

        }
    }
}
