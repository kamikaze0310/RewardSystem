using ProtoBuf;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using VRage.Game;
using VRage.ObjectBuilders;

namespace RewardSystem
{
    [XmlRoot("Settings")]
    public class BlockSettings
    {
        [XmlElement("Version")]
        public string version;

        [XmlElement("ServerName")]
        public string serverName;

        [XmlElement("EnableRewards")]
        public bool enabled;

        [XmlElement("EnablePlayerBounty")]
        public bool enablePlayerBounty;

        [XmlElement("EnableNPCBounty")]
        public bool enableNPCBounty;

        [XmlElement("SupportCrossServer")]
        public bool crossServer;

        [XmlElement("IngotYieldPercentage")]
        public double IngotYieldPercentage;

        [XmlElement("SCMultiplier")]
        public float SCMultiplier;

        [XmlElement("NPCIngotYieldPercentage")]
        public double NPCIngotYieldPercentage;

        [XmlElement("PlayerBountyTriggerPoints")]
        public long PlayerBountyTrigger;

        [XmlElement("NPCBountyTriggerPoints")]
        public long NPCBountyTrigger;

        [XmlElement("NPCBountyRewards")]
        public NPCBountyRewards npcBountyReward;

        [XmlElement("SuccessfulBountyAmount")]
        public long bountyAmount;

        [XmlElement("DynamicalPoints")]
        public DynamicalPointSystem dynamicalPoints;

        [XmlElement("BlockSettings")]
        public BlockMonitor[] Block;

        public BlockSettings()
        {
            version = "1.01";
            serverName = "Server";
            enabled = false;
            enablePlayerBounty = false;
            enableNPCBounty = false;
            crossServer = false;
            IngotYieldPercentage = 10;
            SCMultiplier = 1f;
            NPCIngotYieldPercentage = 10;
            PlayerBountyTrigger = 100000;
            NPCBountyTrigger = 100000;
            dynamicalPoints = new DynamicalPointSystem();
            bountyAmount = 1500;
        }



        public static void SaveSettings(BlockSettings settings)
        {
            if (settings == null) return;
            try
            {
                using (var writer = MyAPIGateway.Utilities.WriteFileInLocalStorage("Reward_Settings.xml", typeof(BlockSettings)))
                {
                    writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
                    writer.Close();
                }
            }
            catch (Exception ex)
            {

                VRage.Utils.MyLog.Default.WriteLineAndConsole($"PVPRewards: Error trying to save settings!\n {ex.ToString()}");
            }
        }

        public static BlockSettings LoadSettings()
        {
            if (MyAPIGateway.Utilities.FileExistsInLocalStorage("Reward_Settings.xml", typeof(BlockSettings)) == true)
            {
                try
                {
                    BlockSettings defaults = new BlockSettings();
                    var reader = MyAPIGateway.Utilities.ReadFileInLocalStorage("Reward_Settings.xml", typeof(BlockSettings));
                    string content = reader.ReadToEnd();

                    reader.Close();
                    var settings = MyAPIGateway.Utilities.SerializeFromXML<BlockSettings>(content);
                    if (settings == null)
                        return CreateNewFile();

                    if (settings.version == defaults.version)
                        return settings;

                    settings.version = defaults.version;
                    settings.dynamicalPoints = new DynamicalPointSystem();
                    SaveSettings(settings);
                    return settings;
                    //return MyAPIGateway.Utilities.SerializeFromXML<BlockSettings>(content);
                }
                catch (Exception ex)
                {
                    return CreateNewFile();
                }
            }

            return CreateNewFile();
        }

        public static BlockSettings CreateNewFile()
        {
            var blockData = new BlockSettings();
            var blockMonitorList = new List<BlockMonitor>();
            var npcBounty = new NPCBountyRewards();

            blockData.npcBountyReward = npcBounty;

            var definitions = MyDefinitionManager.Static.GetAllDefinitions();
            foreach (var def in definitions.Where(x => x as MyCubeBlockDefinition != null))
            {
                var defId = def.Id;
                if (defId.SubtypeName.Contains("Armor")) continue;
                if (defId.SubtypeName.Contains("Debug")) continue;

                if (!def.Public || !def.Enabled) continue;

                var blockMonitor = new BlockMonitor();
                var reward = new RewardDetail();
                var rewardList = new List<RewardDetail>();

                blockMonitor.BlockType = defId.ToString();
                rewardList.Add(reward);

                blockMonitor.RewardDetails = rewardList.ToArray();
                blockMonitorList.Add(blockMonitor);
            }

            blockData.Block = blockMonitorList.ToArray();
            SaveSettings(blockData);
            return blockData;
        }

        public static float GetMaxIntegrity()
        {
            return 123660f;
            float largestIntegrity = 0;

            var definitions = MyDefinitionManager.Static.GetAllDefinitions();
            foreach (var def in definitions.Where(x => x as MyCubeBlockDefinition != null))
            {
                var defId = def.Id;
                if (defId.SubtypeName.Contains("Armor")) continue;
                if (defId.SubtypeName.Contains("Debug")) continue;
                if (!def.Public || !def.Enabled) continue;

                var cubeDef = def as MyCubeBlockDefinition;
                if (def == null) continue;
                if (largestIntegrity >= cubeDef.MaxIntegrity) continue;
                largestIntegrity = cubeDef.MaxIntegrity;
            }

            return largestIntegrity;
        }

        public static ConcurrentDictionary<string, long> GetConfigToDictionary(BlockSettings blockSettings)
        {
            ConcurrentDictionary<string, long> temp = new ConcurrentDictionary<string, long>();

            foreach(var settings in blockSettings.Block)
            {
                string blockType = settings.BlockType;
                long reward = settings.RewardDetails[0].BlockPointsReward;

                temp.TryAdd(blockType, reward);
            }

            return temp;
        }
    }

    [XmlRoot("CrossSupport")]
    public class CrossSupport
    {
        [XmlElement("LookForFile")]
        public string lookforFile;

        [XmlElement("WriteFile")]
        public string writeFile;

        public CrossSupport()
        {
            lookforFile = "Server2";
            writeFile = "Server1";
        }

        public static CrossSupport SaveCrossSupport(CrossSupport settings = null)
        {
            CrossSupport data = new CrossSupport();
            try
            {
                if (settings == null)
                {
                    using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage("CrossSupport_Settings.xml", typeof(CrossSupport)))
                    {
                        writer.Write(MyAPIGateway.Utilities.SerializeToXML(data));
                        writer.Close();
                    }

                    return data;
                }

                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage("CrossSupport_Settings.xml", typeof(CrossSupport)))
                {
                    writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
                    writer.Close();
                }
            }
            catch (Exception ex)
            {

                VRage.Utils.MyLog.Default.WriteLineAndConsole($"PVPRewards: Error trying to save cross support settings!\n {ex.ToString()}");
            }

            return data;
        }

        public static CrossSupport LoadCrossSupportSettings()
        {
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage("CrossSupport_Settings.xml", typeof(CrossSupport)) == true)
            {
                try
                {
                    var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage("CrossSupport_Settings.xml", typeof(CrossSupport));
                    string content = reader.ReadToEnd();

                    reader.Close();
                    return MyAPIGateway.Utilities.SerializeFromXML<CrossSupport>(content);
                }
                catch (Exception ex)
                {
                    return SaveCrossSupport();
                }
            }

            return SaveCrossSupport();
        }
    }

    [ProtoContract]
    public class CrossServerSync
    {
        [ProtoMember(1)]
        public RewardDatabase[] factionData;
    }

    [ProtoContract]
    public class DynamicalPointSystem
    {
        [XmlElement("EnableDynamicalPoints")]
        public bool enableDynamical;

        [XmlElement("MaxPoints")]
        public long maxPoints;

        public DynamicalPointSystem()
        {
            enableDynamical = true;
            maxPoints = 700;
        }
    }

    public class NPCBountyRewards
    {
        [XmlElement("Min")]
        public long min;

        [XmlElement("Max")]
        public long max;

        public NPCBountyRewards()
        {
            min = 5000;
            max = 50000;
        }
    }

    public class BlockMonitor
    {
        [XmlElement("BlockType")]
        public string BlockType;

        [XmlElement("RewardDetails")]
        public RewardDetail[] RewardDetails;

        public BlockMonitor()
        {
            BlockType = "MyObjectBuilder_TypeId/SubtypeId";
        }
    }

    public class RewardDetail
    {
        [XmlElement("BlockPointsReward")]
        public long BlockPointsReward;

        public RewardDetail()
        {
            BlockPointsReward = 100;
        }
    }


}