using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;
using static CitizenFX.Core.UI.Screen;

namespace FivemPlayerlist
{
    public class FivemPlayerlist : BaseScript
    {
        private int maxClients = -1;
        private bool ScaleSetup = false;
        private int currentPage = 0;
        Scaleform scale;
        private int maxPages = (int)Math.Ceiling((double)new PlayerList().Count() / 16.0);
        public struct PlayerRowConfig
        {
            public string crewName;
            public int jobPoints;
            public bool showJobPointsIcon;
        }
        private Dictionary<int, PlayerRowConfig> playerConfigs = new Dictionary<int, PlayerRowConfig>();

        public struct FreefunPlayer
        {
            /*
             * contract --
              netId: any;
              identifiers: string[];
              userId: string;
              userName: string;
              createdAt: number;
              playingTime: number;
              kills: number;
              deaths: number;
              killsOnPed: number;
              inGameJoin: number;
            */
            public int netId;
            public string[] identifiers;
            public string userId;
            public string userName;
        }
        private IList<FreefunPlayer> freefunPlayers = new List<FreefunPlayer>();

        private Dictionary<int, string> textureCache = new Dictionary<int, string>();

        /// <summary>
        /// Constructor
        /// </summary>
        public FivemPlayerlist()
        {
            TriggerServerEvent("fs:getMaxPlayers");
            TriggerServerEvent("freefundb:server:requestInfinityPlayer");
            Tick += ShowScoreboard;
            Tick += DisplayController;
            Tick += BackupTimer;

            // Periodically update the player headshots so, you don't have to wait for them later
            Tick += UpdateHeadshots;

            EventHandlers.Add("fs:setMaxPlayers", new Action<int>(SetMaxPlayers));
            EventHandlers.Add("fs:setPlayerConfig", new Action<int, string, int, bool>(SetPlayerConfig));
            EventHandlers.Add("freefundb:client:onInfinityPlayerUpdate", new Action<List<object>>(onInfinityPlayerUpdate));
            Debug.WriteLine("init?");
        }

        public void onInfinityPlayerUpdate(List<object> _players)
        {
            freefunPlayers.Clear();
            foreach(var _player in _players) {
                try
                {
                    if(_player is IDictionary<string, object> player)
                    {
                        string netId = player["netId"].ToString();
                        Debug.WriteLine("netId: " + netId);
                        var freefunPlayer = new FreefunPlayer()
                        {
                            netId = Int32.Parse(netId),
                            identifiers = player["identifiers"] as string[],
                            userId = player["userId"] as string,
                            userName = player["userName"] as string,
                        };
                        freefunPlayers.Add(freefunPlayer);
                        Debug.WriteLine("Freefun player" + freefunPlayer.userName);

                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                }
            }
            Debug.WriteLine("Freefun Players count: "+freefunPlayers.Count());
        }

        /// <summary>
        /// Set the config for the specified player.
        /// </summary>
        /// <param name="playerServerId"></param>
        /// <param name="crewname"></param>
        /// <param name="jobpoints"></param>
        /// <param name="showJPicon"></param>
        private async void SetPlayerConfig(int playerServerId, string crewname, int jobpoints, bool showJPicon)
        {
            var cfg = new PlayerRowConfig()
            {
                crewName = crewname ?? "",
                jobPoints = jobpoints,
                showJobPointsIcon = showJPicon
            };
            playerConfigs[playerServerId] = cfg;
            if (currentPage > -1)
                await LoadScale();
        }


        /// <summary>
        /// Used to close the page if the regular timer fails to close it for some odd reason.
        /// </summary>
        /// <returns></returns>
        private async Task BackupTimer()
        {
            var timer = GetGameTimer();
            var oldPage = currentPage;
            while (GetGameTimer() - timer < 8000 && currentPage > 0 && currentPage == oldPage)
            {
                await Delay(0);
            }
            if (oldPage == currentPage)
            {
                currentPage = 0;
            }
        }

        /// <summary>
        /// Updates the max pages to disaplay based on the player count.
        /// </summary>
        private void UpdateMaxPages()
        {
            maxPages = (int)Math.Ceiling((double)new PlayerList().Count() / 16.0);
        }

        /// <summary>
        /// Manages the display and page setup of the playerlist.
        /// </summary>
        /// <returns></returns>
        private async Task DisplayController()
        {
            if (Game.IsControlJustPressed(0, Control.MultiplayerInfo))
            {
                UpdateMaxPages();
                if (ScaleSetup)
                {
                    currentPage++;
                    if (currentPage > maxPages)
                    {
                        currentPage = 0;
                    }
                    await LoadScale();
                    var timer = GetGameTimer();
                    bool nextPage = false;
                    while (GetGameTimer() - timer < 8000)
                    {
                        await Delay(1);
                        if (Game.IsControlJustPressed(0, Control.MultiplayerInfo))
                        {
                            nextPage = true;
                            break;
                        }
                    }
                    if (nextPage)
                    {
                        UpdateMaxPages();
                        if (currentPage < maxPages)
                        {
                            currentPage++;
                            await LoadScale();
                        }
                        else
                        {
                            currentPage = 0;
                        }
                    }
                    else
                    {
                        currentPage = 0;
                    }
                }
            }
        }

        /// <summary>
        /// Updates the max players (triggered from server event)
        /// </summary>
        /// <param name="count"></param>
        private void SetMaxPlayers(int count)
        {
            maxClients = count;
        }

        /// <summary>
        /// Shows the scoreboard.
        /// </summary>
        /// <returns></returns>
        private async Task ShowScoreboard()
        {
            if (maxClients != -1)
            {
                if (!ScaleSetup)
                {
                    await LoadScale();
                    ScaleSetup = true;
                }
                if (currentPage > 0)
                {
                    float safezone = GetSafeZoneSize();
                    float change = (safezone - 0.89f) / 0.11f;
                    float x = 50f;
                    x -= change * 78f;
                    float y = 50f;
                    y -= change * 50f;

                    var width = 400f;
                    var height = 490f;
                    if (scale != null)
                    {
                        if (scale.IsLoaded)
                        {
                            scale.Render2DScreenSpace(new System.Drawing.PointF(x, y), new System.Drawing.PointF(width, height));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Loads the scaleform.
        /// </summary>
        /// <returns></returns>
        private async Task LoadScale()
        {
            if (scale != null)
            {
                for (var i = 0; i < maxClients * 2; i++)
                {
                    scale.CallFunction("SET_DATA_SLOT_EMPTY", i);
                }
                scale.Dispose();
            }
            scale = null;
            while (!HasScaleformMovieLoaded(RequestScaleformMovie("MP_MM_CARD_FREEMODE")))
            {
                await Delay(0);
            }
            scale = new Scaleform("MP_MM_CARD_FREEMODE");
            var titleIcon = "2";
            var titleLeftText = "FiveM";
            var titleRightText = $"Players {NetworkGetNumConnectedPlayers()}/{maxClients}";
            scale.CallFunction("SET_TITLE", titleLeftText, titleRightText, titleIcon);
            await UpdateScale();
            scale.CallFunction("DISPLAY_VIEW");
        }

        /// <summary>
        /// Struct used for the player info row options.
        /// </summary>
        struct PlayerRow
        {
            public int serverId;
            public string name;
            public string rightText;
            public int color;
            public string iconOverlayText;
            public string jobPointsText;
            public string crewLabelText;
            public enum DisplayType
            {
                NUMBER_ONLY = 0,
                ICON = 1,
                NONE = 2
            };
            public DisplayType jobPointsDisplayType;
            public enum RightIconType
            {
                NONE = 0,
                INACTIVE_HEADSET = 48,
                MUTED_HEADSET = 49,
                ACTIVE_HEADSET = 47,
                RANK_FREEMODE = 65,
                KICK = 64,
                LOBBY_DRIVER = 79,
                LOBBY_CODRIVER = 80,
                SPECTATOR = 66,
                BOUNTY = 115,
                DEAD = 116,
                DPAD_GANG_CEO = 121,
                DPAD_GANG_BIKER = 122,
                DPAD_DOWN_TARGET = 123
            };
            public int rightIcon;
            public string textureString;
            public char friendType;
        }

        /// <summary>
        /// Returns the ped headshot string used for the image of the ped for each row.
        /// </summary>
        /// <param name="ped"></param>
        /// <returns></returns>
        private async Task<string> GetHeadshotImage(int ped)
        {
            var headshotHandle = RegisterPedheadshot(ped);
            /*
             * For some reason, the below loop didn't work originally without the Valid check or the re-registering of the headshot
             */
            while (!IsPedheadshotReady(headshotHandle) || !IsPedheadshotValid(headshotHandle))
            {
                // Debug.WriteLine($"{ped} {headshotHandle} {IsPedheadshotReady(headshotHandle)} {IsPedheadshotValid(headshotHandle)}");
                await Delay(100);
            }
            // Debug.WriteLine($"done: {ped} {headshotHandle} {IsPedheadshotReady(headshotHandle)} {IsPedheadshotValid(headshotHandle)}");
            var txd = GetPedheadshotTxdString(headshotHandle) ?? "";
            UnregisterPedheadshot(headshotHandle);
            return txd;
        }

        /// <summary>
        /// Updates the scaleform settings.
        /// </summary>
        /// <returns></returns>
        private async Task UpdateScale()
        {
            List<PlayerRow> rows = new List<PlayerRow>();

            for (var x = 0; x < 150; x++) // cleaning up in case of a reload, this frees up all ped headshot handles :)
            {
                UnregisterPedheadshot(x);
            }

            var amount = 0;
            foreach (FreefunPlayer p in this.freefunPlayers)
            {
                if (IsRowSupposedToShow(amount))
                {
                    PlayerRow row = new PlayerRow(); // Set as a blank PlayerRow obj
                    string cleanName = p.userName.Replace("<", "").Replace(">", "").Replace("^", "").Replace("~", "").Trim();
                    string rightText = "";
                    string name = cleanName;
                    row = new PlayerRow()
                    {
                        color = 111,
                        crewLabelText = "",
                        friendType = ' ',
                        iconOverlayText = "",
                        jobPointsDisplayType = PlayerRow.DisplayType.NUMBER_ONLY,
                        jobPointsText = "ServerId: " + p.netId,
                        name = name,
                        rightIcon = (int)PlayerRow.RightIconType.RANK_FREEMODE,
                        rightText = rightText,
                        serverId = p.netId,
                    };
                    /*
                    if (playerConfigs.ContainsKey(p.netId))
                    {
                        row = new PlayerRow()
                        {
                            color = 111,
                            crewLabelText = playerConfigs[p.netId].crewName,
                            friendType = ' ',
                            iconOverlayText = "",
                            jobPointsDisplayType = playerConfigs[p.netId].showJobPointsIcon ? PlayerRow.DisplayType.ICON :
                                (playerConfigs[p.netId].jobPoints >= 0 ? PlayerRow.DisplayType.NUMBER_ONLY : PlayerRow.DisplayType.NONE),
                            jobPointsText = playerConfigs[p.netId].jobPoints >= 0 ? playerConfigs[p.netId].jobPoints.ToString() : "",
                            name = name,
                            rightIcon = (int)PlayerRow.RightIconType.RANK_FREEMODE,
                            rightText = rank.ToString(),
                            serverId = p.netId,
                        };
                    }
                    else
                    {
                        row = new PlayerRow()
                        {
                            color = 111,
                            crewLabelText = "",
                            friendType = ' ',
                            iconOverlayText = "",
                            jobPointsDisplayType = PlayerRow.DisplayType.NUMBER_ONLY,
                            jobPointsText = "",
                            name = name,
                            rightIcon = (int)PlayerRow.RightIconType.RANK_FREEMODE,
                            rightText = rank.ToString(),
                            serverId = p.netId,
                        };
                    }
                    */

                    //Debug.WriteLine("Checking if {0} is in the Dic. Their SERVER ID {1}.", p.Name, p.ServerId);
                    if (textureCache.ContainsKey(p.netId))
                    {
                        row.textureString = textureCache[p.netId];
                    }
                    else
                    {
                        //Debug.WriteLine("Not in setting image to blank");
                        row.textureString = "";
                    }

                    rows.Add(row);
                }
                amount++;
            }
            rows.Sort((row1, row2) => row1.serverId.CompareTo(row2.serverId));
            for (var i = 0; i < maxClients * 2; i++)
            {
                scale.CallFunction("SET_DATA_SLOT_EMPTY", i);
            }
            var index = 0;
            foreach (PlayerRow row in rows)
            {
                if (row.crewLabelText != "")
                {
                    scale.CallFunction("SET_DATA_SLOT", index, row.rightText, row.name, row.color, row.rightIcon, row.iconOverlayText, row.jobPointsText,
                        $"..+{row.crewLabelText}", (int)row.jobPointsDisplayType, row.textureString, row.textureString, row.friendType);
                }
                else
                {
                    scale.CallFunction("SET_DATA_SLOT", index, row.rightText, row.name, row.color, row.rightIcon, row.iconOverlayText, row.jobPointsText,
                        "", (int)row.jobPointsDisplayType, row.textureString, row.textureString, row.friendType);
                }
                index++;
            }

            await Delay(0);
        }

        /// <summary>
        /// Used to check if the row from the loop is supposed to be displayed based on the current page view.
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private bool IsRowSupposedToShow(int row)
        {
            if (currentPage > 0)
            {
                var max = currentPage * 16;
                var min = (currentPage * 16) - 16;
                if (row >= min && row < max)
                {
                    return true;
                }
                return false;
            }
            return false;
        }

        /// <summary>
        /// Update the "textureCache" Dictionary with headshots of the players online.
        /// </summary>
        /// <returns></returns>
        private async Task UpdateHeadshots()
        {
            PlayerList playersToCheck = new PlayerList();

            foreach (Player p in playersToCheck)
            {
                string headshot = await GetHeadshotImage(GetPlayerPed(p.Handle));

                textureCache[p.ServerId] = headshot;
                // Debug.WriteLine($"Headshot for {p.ServerId}: {headshot}");
            }

            //Maybe make configurable?
            await Delay(3000);
        }

    }
}
