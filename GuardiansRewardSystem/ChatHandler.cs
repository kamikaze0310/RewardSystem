using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace RewardSystem
{
    public class ChatHandler
    {
        public static void ChatCommands(string messageText, ref bool sendToOthers)
        {
            if (messageText.StartsWith("/rewardhelp") || 
                messageText.StartsWith("/leaderboard") || 
                messageText.StartsWith("/leaderboardpve") ||
                messageText.StartsWith("/rewards") || 
                messageText.StartsWith("/claimreward") || 
                messageText.StartsWith("/hideleaderboard") || 
                messageText.StartsWith("/bounty") ||
                messageText.StartsWith("/rewardlog") ||
                messageText.StartsWith("/rewardrank") ||
                messageText.StartsWith("/disablerewards") ||
                messageText.StartsWith("/enablerewards") ||
                messageText.StartsWith("/resetAllRewards") ||
                messageText.StartsWith("/resetLog") ||
                messageText.StartsWith("/rewardyield ") ||
                messageText.StartsWith("/rewardnpcyield ") ||
                messageText.StartsWith("/rewardmultiple ") ||
                messageText.StartsWith("/resetRewardFaction"))
            {
                long clientId = MyAPIGateway.Session.LocalHumanPlayer.IdentityId;
                messageText += "\n" + clientId.ToString();
                sendToOthers = false;
                Comms.SendChatToServer(messageText);
            }
        }

        public static void HelpList(long playerId)
        {
            string message = $"Use the following chat commands for the reward system: /leaderboard, /leaderboardpve, /rewards, /claimreward, /hideleaderboard, /bounty, /rewardlog, /rewardrank, /resetLog";
            MyVisualScriptLogicProvider.SendChatMessageColored(message, Color.Green, "Server", playerId, "Green");
        }

        public static void ShowRank(long playerId)
        {
            IMyFaction faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(playerId);
            if (faction == null) return;
            
            var _instance = Core.Instance;

            Dictionary<IMyFaction, long> points = new Dictionary<IMyFaction, long>();

            foreach (var item in _instance.Database)
            {
                points.Add(item.faction, item.points);
            }

            var items = from pair in points
                        orderby pair.Value descending
                        select pair;

            int pos = 1;
            int factionPos = 0;
            long factionPoints = 0;
            foreach(var pair in items)
            {
                if(pair.Key == faction)
                {
                    factionPoints = pair.Value;
                    factionPos = pos;
                }

                pos++;
            }

            string message = $"Your faction is currently {factionPos} out of {pos -1} total factions with {factionPoints} points";
            MyVisualScriptLogicProvider.SendChatMessageColored(message, Color.Green, "Server", playerId, "Green");
        }

        public static void GetCurrentRewards(long playerId)
        {
            IMyFaction faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(playerId);
            if (faction == null) return;
            IMyPlayer toPlayer = GetPlayerFromId(playerId);
            if (toPlayer == null) return;

            foreach (var item in Core.Instance.Database)
            {
                // Match faction data
                if (item.factionId == faction.FactionId)
                {
                    Comms.SendCurrentRewardsToClient(item, toPlayer);
                    break;
                }
            }
        }

        public static void DisplayLog(long playerId, string message)
        {
            IMyFaction faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(playerId);
            if (faction == null) return;
            IMyPlayer toPlayer = GetPlayerFromId(playerId);
            if (toPlayer == null) return;

            if(message == "/rewardlog")
            {
                foreach (var item in Core.Instance.Database)
                {
                    // Match faction data
                    if (item.factionId == faction.FactionId)
                    {
                        Comms.SendLogToClient(item, toPlayer);
                        break;
                    }
                }

                return;
            }

            var split = message.Split('.');
            if (split.Length != 2) return;

            if (toPlayer.PromoteLevel != MyPromoteLevel.Admin && toPlayer.PromoteLevel != MyPromoteLevel.Owner)
            {
                MyVisualScriptLogicProvider.SendChatMessageColored("Your Unauthorized Access Attempt Has Been Logged.", Color.Red, "Server", playerId, "Red");
                return;
            }

            IMyFaction logfaction = MyAPIGateway.Session.Factions.TryGetFactionByTag(split[1]);
            if (logfaction == null) return;

            foreach (var item in Core.Instance.Database)
            {
                // Match faction data
                if (item.factionTag == logfaction.Tag)
                {
                    Comms.SendLogToClient(item, toPlayer);
                    break;
                }
            }
        }

        public static void DisableRewards(long playerId)
        {
            IMyPlayer player = GetPlayerFromId(playerId);
            if (player == null) return;
            if(player.PromoteLevel != MyPromoteLevel.Admin && player.PromoteLevel != MyPromoteLevel.Owner)
            {
                MyVisualScriptLogicProvider.SendChatMessageColored("Your Unauthorized Access Attempt Has Been Logged.", Color.Red, "Server", playerId, "Red");
                return;
            }

            Core.config.enabled = false;
            BlockSettings.SaveSettings(Core.config);
            MyVisualScriptLogicProvider.SendChatMessageColored("Reward System is Disabled", Color.Red, "Server", playerId, "Red");
        }

        public static void EnableRewards(long playerId)
        {
            IMyPlayer player = GetPlayerFromId(playerId);
            if (player == null) return;
            if (player.PromoteLevel != MyPromoteLevel.Admin && player.PromoteLevel != MyPromoteLevel.Owner)
            {
                MyVisualScriptLogicProvider.SendChatMessageColored("Your Unauthorized Access Attempt Has Been Logged.", Color.Red, "Server", playerId, "Red");
                return;
            }

            Core.config.enabled = true;
            BlockSettings.SaveSettings(Core.config);
            MyVisualScriptLogicProvider.SendChatMessageColored("Reward System is Enabled", Color.Green, "Server", playerId, "Green");
        }

        public static void ResetAllRewards(long playerId)
        {
            IMyPlayer player = GetPlayerFromId(playerId);
            if (player == null) return;
            if (player.PromoteLevel != MyPromoteLevel.Admin && player.PromoteLevel != MyPromoteLevel.Owner)
            {
                MyVisualScriptLogicProvider.SendChatMessageColored("Your Unauthorized Access Attempt Has Been Logged.", Color.Red, "Server", playerId, "Red");
                return;
            }

            Core.Instance.Database.Clear();
            Core.Instance.Database = RewardConfig.GetNewFactionsDatabase();

            UpdateLeaderboards();
            RewardConfig.SaveDatabase(Core.Instance.Database);
            MyVisualScriptLogicProvider.SendChatMessageColored("All reward data has been reset.", Color.Green, "Server", playerId, "Green");

            if (Core.config.crossServer) Core.Instance.UpdateCrossServer(-1, -1, true);
        }

        public static void ResetFactionRewards(long playerId, string message)
        {
            IMyPlayer player = GetPlayerFromId(playerId);
            if (player == null) return;
            if (player.PromoteLevel != MyPromoteLevel.Admin && player.PromoteLevel != MyPromoteLevel.Owner)
            {
                MyVisualScriptLogicProvider.SendChatMessageColored("Your Unauthorized Access Attempt Has Been Logged.", Color.Red, "Server", playerId, "Red");
                return;
            }

            var split = message.Split('.');
            IMyFaction faction = MyAPIGateway.Session.Factions.TryGetFactionByTag(split[1]);
            if (faction == null) return;
            int index = 0;

            for(int i = 0; i < Core.Instance.Database.Count; i++)
            {
                RewardDatabase db = Core.Instance.Database[i];
                
                if(db.factionTag == split[1])
                {
                    index = i;
                    Core.Instance.Database[i].bountyPointsAgainst = 0;
                    Core.Instance.Database[i].npcPoints = 0;
                    Core.Instance.Database[i].hunter = new List<BountyHunter>().ToArray();
                    Core.Instance.Database[i].isWanted = false;
                    Core.Instance.Database[i].points = 0;
                    Core.Instance.Database[i].rewards = new List<RewardIngots>().ToArray();
                    Core.Instance.Database[i].logger = new Log();

                    break;
                }
            }

            UpdateLeaderboards();
            RewardConfig.SaveDatabase(Core.Instance.Database);
            MyVisualScriptLogicProvider.SendChatMessageColored($"Reward data for {faction.Tag} has been reset.", Color.Green, "Server", playerId, "Green");

            if (Core.config.crossServer) Core.Instance.UpdateCrossServer(index, -1);
        }

        public static void ResetLogs(long playerId)
        {
            IMyFaction faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(playerId);
            if (faction == null) return;

            if (!faction.IsFounder(playerId) && !faction.IsLeader(playerId))
            {
                MyVisualScriptLogicProvider.SendChatMessageColored("Only the faction owner or leaders can claim rewards.", Color.Red, "Server", playerId, "Red");
                return;
            }

            for (int i = 0; i < Core.Instance.Database.Count; i++)
            {
                if(Core.Instance.Database[i].factionTag == faction.Tag)
                {
                    Core.Instance.Database[i].logger = new Log();
                    MyVisualScriptLogicProvider.SendChatMessageColored("Your faction logs have been cleared.", Color.Green, "Server", playerId, "Green");
                    break;
                }
            }
        }

        public static void ClaimReward(long playerId)
        {
            IMyFaction faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(playerId);
            if (faction == null) return;
            if (!faction.IsFounder(playerId) && !faction.IsLeader(playerId))
            {
                MyVisualScriptLogicProvider.SendChatMessageColored("Only the faction owner or leaders can claim rewards.", Color.Red, "Server", playerId, "Red");
                return;
            }

            IMyPlayer player = GetPlayerFromId(playerId);
            if (player == null) return;
            
            if (player.Controller == null || player.Controller.ControlledEntity == null || player.Controller.ControlledEntity.Entity == null)
            {
                MyVisualScriptLogicProvider.SendChatMessageColored("You need to be in a seat to claim rewards.", Color.Red, "Server", playerId, "Red");
                return;
            }

            IMyEntity entity = player.Controller.ControlledEntity.Entity;
            if (entity == null) return;

            IMyTerminalBlock block = entity as IMyTerminalBlock;
            if (block == null)
            {
                MyVisualScriptLogicProvider.SendChatMessageColored("You need to be in a seat to claim rewards.", Color.Red, "Server", playerId, "Red");
                return;
            }

            IMyCubeGrid grid = block.CubeGrid;
            if (grid == null) return;

            List<IMySlimBlock> containers = new List<IMySlimBlock>();
            //MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid).GetBlocksOfType(containers, x => x.IsFunctional);
            grid.GetBlocks(containers, x => x.FatBlock as IMyCargoContainer != null && x.FatBlock.IsFunctional);
            int index = RewardDatabase.FindDataIndex(Core.Instance.Database, faction);
            if (index == -1) return;

            List<RewardIngots> rewards = new List<RewardIngots>();
            rewards = Core.Instance.Database[index].rewards.ToList();
            foreach (var slim in containers)
            {
                IMyCargoContainer item = (IMyCargoContainer)slim.FatBlock;
                if (item == null) continue;

                var inventory = item.GetInventory();
                if (inventory.IsFull) continue;

                
                for(int i = rewards.Count -1; i >= 0; i--)
                {
                    if (inventory.IsFull) break;
                    var ingot = rewards[i];
                    var def = item.BlockDefinition.SubtypeName;
                    double maxVolume = Definitions.GetContainerMaxVolume(def);
                    if ((MyFixedPoint)maxVolume - inventory.CurrentVolume < (MyFixedPoint).1) break;

                    MyDefinitionId ingotDef;
                    MyDefinitionId.TryParse($"MyObjectBuilder_Ingot/{ingot.Ingot}", out ingotDef);
                    if (ingotDef == null) continue;
                    MyPhysicalItemDefinition physicalItem;
                    MyDefinitionManager.Static.TryGetPhysicalItemDefinition(ingotDef, out physicalItem);
                    if (physicalItem == null) continue;

                    var content = (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(ingotDef);
                    float itemVolume = physicalItem.Volume;

                    var canAdd = (maxVolume - (double)inventory.CurrentVolume) / itemVolume;
                    if(canAdd >= (double)ingot.Amount)
                    {
                        //MyVisualScriptLogicProvider.ShowNotification($"Can Add = {canAdd}, Ingot type = {ingot.Ingot}, Ingot Amount = {ingot.Amount}", 20000);
                        MyFixedPoint amountMFP = ingot.Amount;
                        MyObjectBuilder_InventoryItem inventoryItem = new MyObjectBuilder_InventoryItem { Amount = amountMFP, Content = content };
                        inventory.AddItems(amountMFP, inventoryItem.Content);
                        string log = $"Added {ingot.Amount} {ingot.Ingot} ingots to container {item.CustomName} to {grid.CustomName}.";
                        string chat = $"Added {ingot.Amount} {ingot.Ingot} ingots to container {item.CustomName}";
                        MyVisualScriptLogicProvider.SendChatMessageColored(chat, Color.Green, "Server", playerId, "Green");
                        RewardDatabase.WriteLog(ref Core.Instance.Database[index].logger, log);
                        rewards.RemoveAt(i);
                        continue;
                    }
                    else
                    {
                        MyFixedPoint amountMFP = (MyFixedPoint)canAdd;
                        MyObjectBuilder_InventoryItem inventoryItem = new MyObjectBuilder_InventoryItem { Amount = amountMFP, Content = content };
                        inventory.AddItems(amountMFP, inventoryItem.Content);
                        string log = $"Added {ingot.Amount} {ingot.Ingot} ingots to container {item.CustomName} to {grid.CustomName}.";
                        string chat = $"Added {ingot.Amount} {ingot.Ingot} ingots to container {item.CustomName}";
                        MyVisualScriptLogicProvider.SendChatMessageColored(chat, Color.Green, "Server", playerId, "Green");
                        RewardDatabase.WriteLog(ref Core.Instance.Database[index].logger, log);
                        ingot.Amount -= (MyFixedPoint)canAdd;
                        rewards[i] = ingot;
                        break;
                    }
                }
            }

            Core.Instance.Database[index].rewards = rewards.ToArray();

            if(Core.Instance.Database[index].rewards.Length != 0)
            {
                MyVisualScriptLogicProvider.SendChatMessageColored("Insufficient cargo space to fit all rewards.", Color.Red, "Server", playerId, "Red");
            }
            else
            {
                MyVisualScriptLogicProvider.SendChatMessageColored("All rewards have been transferred successfully", Color.Green, "Server", playerId, "Green");
            }
        }

        public static void DisplayLeaderboards(long playerId)
        {
            var _instance = Core.Instance;

            if (!_instance.ActiveLeaderboards.Contains(playerId))
                _instance.ActiveLeaderboards.Add(playerId);

            if (_instance.ActiveLeaderboardsPVE.Contains(playerId))
                _instance.ActiveLeaderboardsPVE.Remove(playerId);

            Dictionary<IMyFaction, long> points = new Dictionary<IMyFaction, long>();

            foreach (var item in _instance.Database)
            {
                points.Add(item.faction, item.points);
            }

            var items = from pair in points
                        orderby pair.Value descending
                        select pair;

            MyVisualScriptLogicProvider.SetQuestlog(true, $"{Core.config.serverName}'s Top 5 PvP Leaderboard", playerId);

            int pos = 1;
            foreach (var pair in items)
            {
                IMyFaction faction = pair.Key;
                string point = pair.Value.ToString("n");

                MyVisualScriptLogicProvider.AddQuestlogDetail($"({pos}) {faction.Name}([{faction.Tag}]): {point} Points", false, true, playerId);
                pos++;

                if (pos == 6) break;
            }

            //if (_instance.ActiveLeaderboards.Contains(playerId)) return;
            //_instance.ActiveLeaderboards.Add(playerId);
        }

        public static void DisplayLeaderboardsPVE(long playerId)
        {
            var _instance = Core.Instance;

            if (_instance.ActiveLeaderboards.Contains(playerId))
                _instance.ActiveLeaderboards.Remove(playerId);

            if (!_instance.ActiveLeaderboardsPVE.Contains(playerId))
                _instance.ActiveLeaderboardsPVE.Add(playerId);

            Dictionary<IMyFaction, long> points = new Dictionary<IMyFaction, long>();

            foreach (var item in _instance.Database)
            {
                points.Add(item.faction, item.npcPoints);
            }

            var items = from pair in points
                        orderby pair.Value descending
                        select pair;

            MyVisualScriptLogicProvider.SetQuestlog(true, $"{Core.config.serverName}'s Top 5 PvE Leaderboard", playerId);

            int pos = 1;
            foreach (var pair in items)
            {
                IMyFaction faction = pair.Key;
                string point = pair.Value.ToString("n");

                MyVisualScriptLogicProvider.AddQuestlogDetail($"({pos}) {faction.Name}([{faction.Tag}]): {point} Points", false, true, playerId);
                pos++;

                if (pos == 6) break;
            }
        }

        public static void UpdateLeaderboards(bool isAttackerNPC = false, bool onlyLCDs = false)
        {
            var _instance = Core.Instance;
            //int factionCount = _instance.BlockRewards.Count - 1;

            Dictionary<IMyFaction, long> points = new Dictionary<IMyFaction, long>();

            if (!isAttackerNPC)
            {
                foreach (var item in _instance.Database)
                {
                    points.Add(item.faction, item.points);
                }
            }
            else
            {
                foreach (var item in _instance.Database)
                {
                    points.Add(item.faction, item.npcPoints);
                }
            }
            

            var items = from pair in points
                        orderby pair.Value descending
                        select pair;

            int pos = 1;
            int loop = 0;
            if (!onlyLCDs)
            {
                if (!isAttackerNPC)
                {
                    foreach (var actives in _instance.ActiveLeaderboards)
                    {
                        try
                        {
                            if (!IsPlayerOnline(actives)) continue;
                            MyVisualScriptLogicProvider.RemoveQuestlogDetails(actives);

                            foreach (var pair in items)
                            {
                                IMyFaction faction = pair.Key;
                                string point = pair.Value.ToString("n");

                                //MyVisualScriptLogicProvider.ReplaceQuestlogDetail(loop, $"({pos}) {faction.Name}({faction.Tag}): {point} Points", false, actives);
                                MyVisualScriptLogicProvider.AddQuestlogDetail($"({pos}) {faction.Name}([{faction.Tag}]): {point} Points", false, false, actives);

                                pos++;
                                loop++;
                                if (pos == 6) break;
                            }
                        }
                        catch (Exception ex)
                        {
                            //MyVisualScriptLogicProvider.ShowNotification($"Failed to replace quest log detail", 20000);
                        }
                    }
                }
                else
                {
                    foreach (var actives in _instance.ActiveLeaderboardsPVE)
                    {
                        try
                        {
                            if (!IsPlayerOnline(actives)) continue;
                            MyVisualScriptLogicProvider.RemoveQuestlogDetails(actives);

                            foreach (var pair in items)
                            {
                                IMyFaction faction = pair.Key;
                                string point = pair.Value.ToString("n");

                                //MyVisualScriptLogicProvider.ReplaceQuestlogDetail(loop, $"({pos}) {faction.Name}({faction.Tag}): {point} Points", false, actives);
                                MyVisualScriptLogicProvider.AddQuestlogDetail($"({pos}) {faction.Name}([{faction.Tag}]): {point} Points", false, false, actives);

                                pos++;
                                loop++;
                                if (pos == 6) break;
                            }
                        }
                        catch (Exception ex)
                        {
                            //MyVisualScriptLogicProvider.ShowNotification($"Failed to replace quest log detail", 20000);
                        }
                    }
                }
            }

            pos = 1;

            if (!isAttackerNPC)
            {
                foreach (var lcd in _instance.ActiveLCDs)
                {
                    var panel = lcd as IMyTextSurface;
                    if (panel == null) continue;
                    string title = $" {Core.config.serverName}'s Top 10 PvP Leaderboard:\n\n";

                    panel.WriteText(title);
                    foreach (var pair in items)
                    {
                        IMyFaction faction = pair.Key;
                        string point = pair.Value.ToString("n");

                        string rank = $"({pos}) {faction.Name}[{faction.Tag}]: {point} Points\n";
                        panel.WriteText(rank, true);
                        pos++;

                        if (pos == 11) break;
                    }

                    pos = 1;
                }
            }
            else
            {
                foreach (var lcd in _instance.ActiveLCDsPVE)
                {
                    var panel = lcd as IMyTextSurface;
                    if (panel == null) continue;
                    string title = $" {Core.config.serverName}'s Top 10 PvE Leaderboard:\n\n";

                    panel.WriteText(title);
                    foreach (var pair in items)
                    {
                        IMyFaction faction = pair.Key;
                        string point = pair.Value.ToString("n");

                        string rank = $"({pos}) {faction.Name}[{faction.Tag}]: {point} Points\n";
                        panel.WriteText(rank, true);
                        pos++;

                        if (pos == 11) break;
                    }

                    pos = 1;
                }
            }
            
        }

        public static void HideLeaderboards(long playerId)
        {
            var _instance = Core.Instance;
            MyVisualScriptLogicProvider.SetQuestlogVisible(false, playerId);

            if (_instance.ActiveLeaderboards.Contains(playerId))
            {
                _instance.ActiveLeaderboards.Remove(playerId);
            }

            if (_instance.ActiveLeaderboardsPVE.Contains(playerId))
            {
                _instance.ActiveLeaderboardsPVE.Remove(playerId);
            }
        }

        public static void ChangeRewardYield(long playerId, string message)
        {
            IMyPlayer player = GetPlayerFromId(playerId);
            if (player == null) return;
            if (player.PromoteLevel != MyPromoteLevel.Admin && player.PromoteLevel != MyPromoteLevel.Owner)
            {
                MyVisualScriptLogicProvider.SendChatMessageColored("Your Unauthorized Access Attempt Has Been Logged.", Color.Red, "Server", playerId, "Red");
                return;
            }

            string m = message.Replace("/rewardyield ", "");
            double value = 0;
            if (!double.TryParse(m, out value))
            {
                MyVisualScriptLogicProvider.SendChatMessageColored("Invalid request. Try ex. /rewardyield 20", Color.Red, "Server", playerId, "Red");
                return;
            }

            Core.config.IngotYieldPercentage = value;
            Core.Instance.RewardIngotYield = value / 100;
            BlockSettings.SaveSettings(Core.config);

            MyVisualScriptLogicProvider.SendChatMessageColored($"PvP ingot yield return is now set to {value} percent.", Color.Green, "Server", playerId, "Green");
        }

        public static void ChangeNPCRewardYield(long playerId, string message)
        {
            IMyPlayer player = GetPlayerFromId(playerId);
            if (player == null) return;
            if (player.PromoteLevel != MyPromoteLevel.Admin && player.PromoteLevel != MyPromoteLevel.Owner)
            {
                MyVisualScriptLogicProvider.SendChatMessageColored("Your Unauthorized Access Attempt Has Been Logged.", Color.Red, "Server", playerId, "Red");
                return;
            }

            string m = message.Replace("/rewardnpcyield ", "");
            double value = 0;
            if (!double.TryParse(m, out value))
            {
                MyVisualScriptLogicProvider.SendChatMessageColored("Invalid request. Try ex. /rewardnpcyield 10", Color.Red, "Server", playerId, "Red");
                return;
            }

            Core.config.NPCIngotYieldPercentage = value;
            Core.Instance.NPCRewardIngotYield = value / 100;
            BlockSettings.SaveSettings(Core.config);

            MyVisualScriptLogicProvider.SendChatMessageColored($"PvE/NPC ingot yield return is now set to {value} percent.", Color.Green, "Server", playerId, "Green");
        }

        public static void ChangeSCMultiplier(long playerId, string message)
        {
            IMyPlayer player = GetPlayerFromId(playerId);
            if (player == null) return;
            if (player.PromoteLevel != MyPromoteLevel.Admin && player.PromoteLevel != MyPromoteLevel.Owner)
            {
                MyVisualScriptLogicProvider.SendChatMessageColored("Your Unauthorized Access Attempt Has Been Logged.", Color.Red, "Server", playerId, "Red");
                return;
            }

            string m = message.Replace("/rewardmultiple ", "");
            float value = 0;
            if (!float.TryParse(m, out value))
            {
                MyVisualScriptLogicProvider.SendChatMessageColored("Invalid request. Try ex. /rewardmultiple 1", Color.Red, "Server", playerId, "Red");
                return;
            }

            Core.config.SCMultiplier = value;
            BlockSettings.SaveSettings(Core.config);
            
            
            MyVisualScriptLogicProvider.SendChatMessageColored($"SC multiplier is now set to {value}.", Color.Green, "Server", playerId, "Green");
        }

        public static bool IsPlayerOnline(long playerId)
        {
            List<IMyPlayer> playerList = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(playerList);

            foreach(var player in playerList)
            {
                if (player.IdentityId == playerId) return true;
            }

            return false;
        }

        public static IMyPlayer GetPlayerFromId(long playerId)
        {
            List<IMyPlayer> playerList = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(playerList);

            foreach (var player in playerList)
            {
                if (player.IdentityId == playerId) return player;
            }

            return null;
        }
    }
}
