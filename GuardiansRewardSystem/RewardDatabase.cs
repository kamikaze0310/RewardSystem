using ProtoBuf;
using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage;
using VRage.Game.ModAPI;

namespace RewardSystem
{
    [ProtoContract]
    public struct Log
    {
        [ProtoMember(1)]
        public string log;

        [ProtoMember(2)]
        public List<string> LogbyDate;
    }

    [ProtoContract]
    public struct RewardIngots
    {
        [ProtoMember(1)]
        public string Ingot;

        [ProtoMember(2)]
        public MyFixedPoint Amount;
    }

    [ProtoContract]
    public struct BountyHunter
    {
        [ProtoMember(1)]
        public string factionTag;

        [ProtoMember(2)]
        public long bountyPointsEarned;

        [ProtoMember(3)]
        public int BlocksDestroyed;
    }

    [ProtoContract]
    public class RewardDatabase
    {
        [ProtoMember(1)]
        public string factionTag;

        [ProtoMember(2)]
        public long factionId;

        [ProtoIgnore]
        public IMyFaction faction;

        [ProtoMember(3)]
        public Log logger;

        [ProtoMember(4)]
        public RewardIngots[] rewards;

        [ProtoMember(5)]
        public long points;

        [ProtoMember(6)]
        public bool isWanted;

        [ProtoMember(7)]
        public long npcPoints;

        [ProtoMember(8)]
        public BountyHunter[] hunter;

        [ProtoMember(9)]
        public long bountyPointsAgainst;

        public RewardDatabase()
        {
            factionTag = "";
            factionId = 0;
            faction = null;
            logger = new Log();
            rewards = new List<RewardIngots>().ToArray();
            points = 0;
            isWanted = false;
            npcPoints = 0;
            hunter = new List<BountyHunter>().ToArray();
            bountyPointsAgainst = 0;
        }

        public RewardDatabase(string tag, IMyFaction thisFaction)
        {
            factionTag = tag;
            factionId = thisFaction.FactionId;
            faction = thisFaction;
            logger = new Log();
            rewards = new List<RewardIngots>().ToArray();
            points = 0;
            isWanted = false;
            npcPoints = 0;
            hunter = new List<BountyHunter>().ToArray();
            bountyPointsAgainst = 0;
        }

        public static void WriteLog(ref Log myLog, string message)
        {
            DateTime date1 = DateTime.Now;
            if (myLog.LogbyDate == null)
            {
                myLog.LogbyDate = new List<string>();
            }

            string newLog = "";
            int index = StringListContains(myLog.LogbyDate, date1.Date.ToString("d"));
            if (index == -1)
            {
                newLog = date1.Date.ToString("d") + "\n";
                newLog += date1.ToString("[ HH:mm:ss ] ") + "EST: " + message + "\n";
                myLog.LogbyDate.Add(newLog);

                myLog.log += "\n";
                myLog.log += newLog;
                //return myLog;
            }
            else
            {
                newLog = date1.ToString($"[ HH:mm:ss ] ") + "EST: " + message + "\n";
                myLog.LogbyDate[index] += newLog;
                myLog.log += newLog;
                //return myLog;
            }
        }

        public static int StringListContains(List<string> stringList, string text)
        {
            for (int i = 0; i < stringList.Count; i++)
            {
                if (stringList[i].Contains(text))
                {
                    return i;
                }
            }

            return -1;
        }

        public static int FindDataIndex(List<RewardDatabase> database, IMyFaction faction = null)
        {
            if (faction != null)
            {
                if (faction.IsEveryoneNpc()) return -1;
                for (int i = 0; i <= database.Count; i++)
                {
                    // FactionCheck
                    if (database[i].faction.FactionId == faction.FactionId)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }
    }

    [ProtoContract]
    public struct SavePackage
    {
        //[ProtoMember(1)]
        //public byte[] Data;

        [ProtoMember(1)]
        public RewardDatabase[] Files;

        /*public SavePackage(List<RewardDatabase> database)
        {
            Files = database.ToArray();
            //Data = MyAPIGateway.Utilities.SerializeToBinary(Files);
        } */
    }

    public class RewardConfig
    {
        const string saveTag = "{0}.xml";

        public static void SaveDatabase(List<RewardDatabase> database)
        {
            if (database.Count == 0) return;

            /*foreach (var item in database)
            {
                try
                {
                    var newByteData = MyAPIGateway.Utilities.SerializeToBinary(item);
                    var base64string = Convert.ToBase64String(newByteData);

                    using (var writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(string.Format(saveTag, item.faction.FactionId), typeof(RewardDatabase)))
                    {
                        writer.Write(base64string);
                        writer.Close();
                    }
                }
                catch (Exception ex)
                {

                }
            }*/

            SavePackage package = new SavePackage();
            package.Files = database.ToArray();
            var data = MyAPIGateway.Utilities.SerializeToBinary(package);
            var base64string = Convert.ToBase64String(data);

            using (var writer = MyAPIGateway.Utilities.WriteFileInLocalStorage("RewardDatabaseFiles", typeof(SavePackage)))
            {
                writer.Write(base64string);
                writer.Close();
            }
        }

        public static List<RewardDatabase> LoadFactionData()
        {
            var factions = MyAPIGateway.Session.Factions.Factions;
            List<RewardDatabase> list = new List<RewardDatabase>();

            if (MyAPIGateway.Utilities.FileExistsInLocalStorage("RewardDatabaseFiles", typeof(SavePackage)) == true)
            {
                try
                {
                    SavePackage data;
                    var reader = MyAPIGateway.Utilities.ReadFileInLocalStorage("RewardDatabaseFiles", typeof(SavePackage));
                    string content = reader.ReadToEnd();

                    reader.Close();
                    byte[] byteData = Convert.FromBase64String(content);
                    data = MyAPIGateway.Utilities.SerializeFromBinary<SavePackage>(byteData);
                    list = data.Files.ToList();

                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        IMyFaction faction = MyAPIGateway.Session.Factions.TryGetFactionById(list[i].factionId);
                        if (faction == null)
                        {
                            list.RemoveAt(i);
                            continue;
                        }

                        list[i].faction = faction;
                    }
                }
                catch (Exception ex)
                {
                }
            }

            /*foreach (var factionId in factions.Keys)
            {
                if (factions[factionId].IsEveryoneNpc()) continue;

                if (MyAPIGateway.Utilities.FileExistsInLocalStorage(string.Format(saveTag, factions[factionId]), typeof(RewardDatabase)) == true)
                {
                    try
                    {
                        RewardDatabase data;
                        var reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(string.Format(saveTag, factions[factionId]), typeof(RewardDatabase));
                        string content = reader.ReadToEnd();

                        reader.Close();
                        byte[] byteData = Convert.FromBase64String(content);
                        data = MyAPIGateway.Utilities.SerializeFromBinary<RewardDatabase>(byteData);

                        if (data == null) continue;
                        data.faction = factions[factionId];
                        list.Add(data);
                    }
                    catch (Exception ex)
                    {
                        //list = GetNewFactionsDatabase();
                        //SaveDatabase(list);
                        //return list;
                    }
                }
            }*/

            return list;
        }

        public static List<RewardDatabase> GetNewFactionsDatabase()
        {
            var factions = MyAPIGateway.Session.Factions.Factions;
            var list = new List<RewardDatabase>();

            foreach (var factionId in factions.Keys)
            {
                if (factions[factionId].IsEveryoneNpc()) continue;

                RewardDatabase data = new RewardDatabase(factions[factionId].Tag, factions[factionId]);
                list.Add(data);
            }

            return list;
        }

        public static void RemoveFile(long factionId)
        {
            try
            {
                /*if (MyAPIGateway.Utilities.FileExistsInLocalStorage(string.Format(saveTag, factionId.ToString()), typeof(RewardDatabase)) == true)
                {
                    MyAPIGateway.Utilities.DeleteFileInLocalStorage(string.Format(saveTag, factionId.ToString()), typeof(RewardDatabase));
                }*/

                List<RewardDatabase> list = new List<RewardDatabase>();

                if (MyAPIGateway.Utilities.FileExistsInLocalStorage("RewardDatabaseFiles", typeof(SavePackage)) == true)
                {
                    try
                    {
                        SavePackage data;
                        var reader = MyAPIGateway.Utilities.ReadFileInLocalStorage("RewardDatabaseFiles", typeof(SavePackage));
                        string content = reader.ReadToEnd();

                        reader.Close();
                        byte[] byteData = Convert.FromBase64String(content);
                        data = MyAPIGateway.Utilities.SerializeFromBinary<SavePackage>(byteData);
                        list = data.Files.ToList();
                    }
                    catch (Exception ex)
                    {
                    }

                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        if(list[i].factionId == factionId)
                        {
                            list.RemoveAt(i);
                            break;
                        }
                    }

                    //SaveDatabase(list);
                }
            }
            catch(Exception ex)
            {

            }
        }
    }
}
