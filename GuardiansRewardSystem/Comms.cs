using ProtoBuf;
using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI;

namespace RewardSystem
{
    public enum DataType
    {
        String,
        RequestLog,
        SendLog,
        SendRewardList,
        ChatMessage,
    }

    [ProtoContract]
    public class CommPackage
    {
        [ProtoMember(1)]
        public DataType Type;

        [ProtoMember(2)]
        public byte[] Data;

        public CommPackage()
        {

            Type = DataType.String;
            Data = new byte[0];

        }

        public CommPackage(DataType type, object data)
        {
            Type = type;
            Data = MyAPIGateway.Utilities.SerializeToBinary(data);
        }

        public CommPackage(DataType type, string data)
        {
            Type = type;
            Data = MyAPIGateway.Utilities.SerializeToBinary(data);
        }
    }

    public class Comms
    {
        public static void MessageHandler(byte[] data)
        {
            try
            {
                var package = MyAPIGateway.Utilities.SerializeFromBinary<CommPackage>(data);
                if (package == null) return;

                if (package.Type == DataType.ChatMessage)
                {
                    var encasedData = MyAPIGateway.Utilities.SerializeFromBinary<string>(package.Data);
                    if (encasedData == null) return;

                    var split = encasedData.Split('\n');
                    string message = split[0];
                    long clientId = 0;
                    long.TryParse(split[1], out clientId);
                    if (clientId == 0) return;

                    if (message == "/rewardhelp")
                    {
                        ChatHandler.HelpList(clientId);
                        return;
                    }

                    if (message == "/leaderboard")
                    {
                        ChatHandler.DisplayLeaderboards(clientId);
                        return;
                    }

                    if (message == "/leaderboardpve")
                    {
                        ChatHandler.DisplayLeaderboardsPVE(clientId);
                        return;
                    }

                    if (message == "/hideleaderboard")
                    {
                        ChatHandler.HideLeaderboards(clientId);
                        return;
                    }

                    if (message.Contains("/rewardlog"))
                    {
                        ChatHandler.DisplayLog(clientId, message);
                        return;
                    }

                    if (message.Contains("/claimreward"))
                    {
                        ChatHandler.ClaimReward(clientId);
                        return;
                    }

                    if (message.Contains("/rewards"))
                    {
                        ChatHandler.GetCurrentRewards(clientId);
                        return;
                    }

                    if (message == "/rewardrank")
                    {
                        ChatHandler.ShowRank(clientId);
                        return;
                    }

                    if (message == "/disablerewards")
                    {
                        ChatHandler.DisableRewards(clientId);
                        return;
                    }

                    if (message == "/enablerewards")
                    {
                        ChatHandler.EnableRewards(clientId);
                        return;
                    }

                    if (message == "/resetAllRewards")
                    {
                        ChatHandler.ResetAllRewards(clientId);
                        return;
                    }

                    if (message.Contains("/resetRewardFaction"))
                    {
                        ChatHandler.ResetFactionRewards(clientId, message);
                        return;
                    }

                    if (message == "/resetLog")
                    {
                        ChatHandler.ResetLogs(clientId);
                        return;
                    }

                    if (message.Contains("/rewardyield "))
                    {
                        ChatHandler.ChangeRewardYield(clientId, message);
                        return;
                    }

                    if (message.Contains("/rewardnpcyield "))
                    {
                        ChatHandler.ChangeNPCRewardYield(clientId, message);
                        return;
                    }

                    if (message.Contains("/rewardmultiple "))
                    {
                        ChatHandler.ChangeSCMultiplier(clientId, message);
                        return;
                    }
                }

                if(package.Type == DataType.SendLog)
                {
                    var encasedData = MyAPIGateway.Utilities.SerializeFromBinary<RewardDatabase>(package.Data);
                    if (encasedData == null) return;
                    // Match faction data
                    Log myLog = encasedData.logger;
                    IMyFaction faction = MyAPIGateway.Session.Factions.TryGetFactionById(encasedData.factionId);
                    if (faction == null) return;

                    MyAPIGateway.Utilities.ShowMissionScreen($"{faction.Name} Battle Log", "", null, myLog.log, null, "Ok");
                }

                if (package.Type == DataType.SendRewardList)
                {
                    var encasedData = MyAPIGateway.Utilities.SerializeFromBinary<RewardDatabase>(package.Data);
                    if (encasedData == null) return;

                    string message = "Ingot:   Qty:\n-------------------\n\n";

                    foreach(var item in encasedData.rewards)
                    {
                        message += $"{item.Ingot}: {item.Amount}\n";
                    }
                    // Match faction data
                    IMyFaction faction = MyAPIGateway.Session.Factions.TryGetFactionById(encasedData.factionId);
                    if (faction == null) return;

                    MyAPIGateway.Utilities.ShowMissionScreen($"{faction.Name} Current Rewards", "", null, message, null, "Ok");
                }
            }
            catch (Exception ex)
            {

            }
        }

        public static void SendChatToServer(string message)
        {
            try
            {
                CommPackage package = new CommPackage(DataType.ChatMessage, message);
                var sendData = MyAPIGateway.Utilities.SerializeToBinary(package);
                MyAPIGateway.Multiplayer.SendMessageToServer(4705, sendData);
            }
            catch (Exception exc)
            {

            }
        }

        public static void SendLogToClient(RewardDatabase database, IMyPlayer player)
        {
            CommPackage package = new CommPackage(DataType.SendLog, database);
            var sendData = MyAPIGateway.Utilities.SerializeToBinary(package);
            MyAPIGateway.Multiplayer.SendMessageTo(4705, sendData, player.SteamUserId);
        }

        public static void SendCurrentRewardsToClient(RewardDatabase database, IMyPlayer player)
        {
            CommPackage package = new CommPackage(DataType.SendRewardList, database);
            var sendData = MyAPIGateway.Utilities.SerializeToBinary(package);
            MyAPIGateway.Multiplayer.SendMessageTo(4705, sendData, player.SteamUserId);
        }
    }
}
