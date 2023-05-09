using System;
using System.Collections.Generic;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;



namespace FivemPlayerlistServer
{
    public class FPLServer : BaseScript
    {

        private Dictionary<int, dynamic[]> list = new Dictionary<int, dynamic[]>();
        public FPLServer()
        {
            EventHandlers.Add("fs:getMaxPlayers", new Action<Player>(ReturnMaxPlayers));
            Exports.Add("setPlayerRowConfig", new Action<string, string, string, string>(SetPlayerConfig2));
            EventHandlers.Add("fs:setPlayerRowConfig", new Action<int, string, int, bool>(SetPlayerConfig));
        }

        private void ReturnMaxPlayers([FromSource] Player source)
        {
            source.TriggerEvent("fs:setMaxPlayers", int.Parse(GetConvar("sv_maxclients", "30").ToString()));
            Debug.WriteLine($"max players requested? {source.Name}");
            //var pl = new PlayerList();
            //foreach (Player p in pl)
            //{
            //    if (list.ContainsKey(int.Parse(p.Handle)))
            //    {
            //        var listItem = list[int.Parse(p.Handle)];
            //        var p1 = listItem[0];
            //        var p2 = listItem[1];
            //        var p3 = listItem[2];
            //        var p4 = listItem[3];
            //        source.TriggerEvent("fs:setPlayerRowConfig", p1, p2, p3, p4);
            //        await Delay(1);
            //    }
            //}
        }

        private void SetPlayerConfig2(string playerServerId, string crewName, string jobPoints, string showJobPointsIcon)
        {
            SetPlayerConfig(int.Parse(playerServerId), crewName, int.Parse(jobPoints ?? "-1"), bool.Parse(showJobPointsIcon ?? "false"));
        }
        private void SetPlayerConfig(int playerServerId, string crewName, int jobPoints, bool showJobPointsIcon)
        {
            if (playerServerId > 0)
            {
                list[playerServerId] = new dynamic[4] { playerServerId, crewName ?? "", jobPoints, showJobPointsIcon};
                TriggerClientEvent("fs:setPlayerConfig", playerServerId, crewName ?? "", jobPoints, showJobPointsIcon);
            }
        }

    }
}
