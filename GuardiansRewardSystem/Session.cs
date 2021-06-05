using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace RewardSystem
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]

    public class Core : MySessionComponentBase
    {
        public static Core Instance;
        public static CrossSupport CrossServer;
        public bool isServer;
        public bool isDedicated;
        public float maxIntegrityGlobal;
        private bool getPlayer;
        private volatile bool processQueue;
        private bool processIntegrityQueue;
        public double RewardIngotYield;
        public double NPCRewardIngotYield;
        public List<RewardDatabase> Database;
        public static BlockSettings config;
        public ConcurrentDictionary<string, long> BlockRewards = new ConcurrentDictionary<string, long>();
        public List<long> ActiveLeaderboards = new List<long>();
        public List<long> ActiveLeaderboardsPVE = new List<long>();
        public List<IMyTerminalBlock> ActiveLCDs = new List<IMyTerminalBlock>();
        public List<IMyTerminalBlock> ActiveLCDsPVE = new List<IMyTerminalBlock>();
        public ConcurrentDictionary<IMyCubeBlock, float> cachedIntegrity = new ConcurrentDictionary<IMyCubeBlock, float>();
        public ConcurrentDictionary<IMyTerminalBlock, long> blockOwners = new ConcurrentDictionary<IMyTerminalBlock, long>();
        public List<MissileData> missiles = new List<MissileData>();
        public List<WarheadData> warHeads = new List<WarheadData>();
        public ConcurrentQueue<QueueData> queueList = new ConcurrentQueue<QueueData>();
        public Queue<IMyCubeBlock> integrityQueue = new Queue<IMyCubeBlock>();

        public override void LoadData()
        {
            Instance = this;
        }

        public override void BeforeStart()
        {
            isServer = MyAPIGateway.Multiplayer.IsServer;
            isDedicated = MyAPIGateway.Utilities.IsDedicated;
            MyAPIGateway.Multiplayer.RegisterMessageHandler(4705, Comms.MessageHandler);
            MyAPIGateway.Utilities.MessageEntered += ChatHandler.ChatCommands;

            if (!isServer) return;
            config = BlockSettings.LoadSettings();
            //if (!config.enabled) return;

            //MyAPIGateway.Session.DamageSystem.RegisterDestroyHandler(1, WarheadDestroyed);
            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(1, BeforeDamage);
            MyAPIGateway.Session.Factions.FactionCreated += NewFaction;
            MyAPIGateway.Session.Factions.FactionStateChanged += FactionChanged;
            MyAPIGateway.Session.Factions.FactionEdited += FactionEdited;
            MyAPIGateway.Entities.OnEntityAdd += EntityAdded;
            MyAPIGateway.Entities.OnEntityRemove += EntityRemoved;

            Database = RewardConfig.LoadFactionData();
            CrossServer = CrossSupport.LoadCrossSupportSettings();

            if (Database == null || Database.Count == 0)
            {
                Database = new List<RewardDatabase>();
                Database = RewardConfig.GetNewFactionsDatabase();
            }

            BlockRewards = BlockSettings.GetConfigToDictionary(config);
            maxIntegrityGlobal = BlockSettings.GetMaxIntegrity();
            RewardIngotYield = config.IngotYieldPercentage / 100;
            NPCRewardIngotYield = config.NPCIngotYieldPercentage / 100;

            HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities);
            foreach(var entity in entities)
            {
                EntityAdded(entity);
            }

            ChatHandler.UpdateLeaderboards(false, true);
        }

        public override void UpdateBeforeSimulation()
        {
            GetLocalPlayer();
            if (!isServer) return;

            if (!config.crossServer || !config.enabled) return;
            LookForFile();
        }

        private void GetLocalPlayer()
        {
            if (isServer && isDedicated) return;
            if (getPlayer) return;
            IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
            if (player == null) return;

            getPlayer = true;
            MyVisualScriptLogicProvider.SetQuestlogVisible(false, 0);
            MyVisualScriptLogicProvider.ShowNotification($"Reward System Initialized, Type /rewardhelp for more info", 60000, "Green");
        }

        private void LookForFile()
        {
            if (MyAPIGateway.Utilities.FileExistsInLocalStorage($"{CrossServer.lookforFile}.xml", typeof(CrossServerSync)) == true)
            {
                CrossServerSync sync = new CrossServerSync();
                try
                {
                    using (var reader = MyAPIGateway.Utilities.ReadFileInLocalStorage($"{CrossServer.lookforFile}.xml", typeof(CrossServerSync)))
                    {
                        string content = reader.ReadToEnd();

                        reader.Close();
                        byte[] byteData = Convert.FromBase64String(content);
                        sync = MyAPIGateway.Utilities.SerializeFromBinary<CrossServerSync>(byteData);
                        UpdateDatabase(sync);
                    }

                    
                    MyAPIGateway.Utilities.DeleteFileInLocalStorage($"{CrossServer.lookforFile}.xml", typeof(CrossServerSync));
                }
                catch (Exception ex)
                {
                    //MyVisualScriptLogicProvider.ShowNotificationToAll($"Failed to look for file {ex}", 10000);
                }
            }
        }

        private void UpdateDatabase(CrossServerSync data)
        {
            foreach (var item in data.factionData)
            {
                for (int i = 0; i < Database.Count; i++)
                {
                    // Match faction data
                    if (Database[i].factionTag == item.factionTag)
                    {
                        Database[i] = item;
                        Database[i].faction = MyAPIGateway.Session.Factions.TryGetFactionByTag(Database[i].factionTag);
                        break;
                    }
                }
            }

            ChatHandler.UpdateLeaderboards();
        }

        public void EntityAdded(IMyEntity entity)
        {
            MyAPIGateway.Parallel.StartBackground(() =>
            {
                try
                {
                    if (entity == null || entity.MarkedForClose || entity.Physics == null) return;

                    var grid = entity as IMyCubeGrid;
                    var cubeGrid = entity as MyCubeGrid;
                    if (grid == null || cubeGrid == null) return;

                    List<IMySlimBlock> slim = new List<IMySlimBlock>();
                    grid.GetBlocks(slim);
                    //MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid).GetBlocksOfType(cube);
                    //MyVisualScriptLogicProvider.ShowNotification($"Got Grid = {grid.CustomName}", 50000);

                    foreach (var item in slim)
                    {
                        var fat = item.FatBlock;
                        if (fat == null) continue;

                        if (!cachedIntegrity.ContainsKey(fat))
                        {
                            cachedIntegrity.TryAdd(fat, item.BuildLevelRatio);
                            //MyVisualScriptLogicProvider.ShowNotification($"cached block", 50000);
                        }

                        var terminal = fat as IMyTerminalBlock;
                        if (terminal == null) continue;

                        if (!blockOwners.ContainsKey(terminal))
                        {
                            blockOwners.TryAdd(terminal, terminal.OwnerId);
                            terminal.OwnershipChanged += ChangedOwner;
                        }

                        if (fat as IMyTextSurface == null) continue;

                        var lcd = fat as IMyTextSurface;
                        if (lcd != null)
                        {
                            if (terminal.CustomName.Contains("[RewardLeaderboard]"))
                            {
                                if (!ActiveLCDs.Contains(terminal))
                                {
                                    ActiveLCDs.Add(terminal);
                                }
                            }

                            if (terminal.CustomName.Contains("[RewardLeaderboardPvE]"))
                            {
                                if (!ActiveLCDsPVE.Contains(terminal))
                                {
                                    ActiveLCDsPVE.Add(terminal);
                                }
                            }

                            terminal.CustomNameChanged += TextSurfaceNameChanged;
                            continue;
                        }

                        //MyVisualScriptLogicProvider.ShowNotification($"LCD found", 50000);
                    }

                    grid.OnBlockIntegrityChanged += OnBlockIntegrityChanged;
                    cubeGrid.OnFatBlockAdded += FatBlockAdded;
                    //MyVisualScriptLogicProvider.ShowNotification($"Added Grid Integrity Event", 50000);
                }
                catch (Exception ex)
                {
                    //MyVisualScriptLogicProvider.ShowNotification($"Caught {ex}", 50000);
                }
            });
        }

        public void EntityRemoved(IMyEntity entity)
        {
            MyAPIGateway.Parallel.StartBackground(() =>
            {
                if (entity == null || entity.Physics == null) return;

                var grid = entity as IMyCubeGrid;
                var cubeGrid = entity as MyCubeGrid;
                if (grid == null || cubeGrid == null) return;

                try
                {
                    grid.OnBlockIntegrityChanged -= OnBlockIntegrityChanged;
                    cubeGrid.OnFatBlockAdded -= FatBlockAdded;

                    grid = entity as IMyCubeGrid;
                    cubeGrid = entity as MyCubeGrid;

                    List<IMySlimBlock> slim = new List<IMySlimBlock>();
                    grid.GetBlocks(slim);

                    foreach (var item in slim)
                    {
                        var fat = item.FatBlock;
                        if (fat == null) continue;

                        if (cachedIntegrity.ContainsKey(fat))
                            cachedIntegrity.Remove(fat);

                        var terminal = fat as IMyTerminalBlock;
                        if (terminal == null) continue;

                        if (blockOwners.ContainsKey(terminal))
                        {
                            blockOwners.Remove(terminal);
                            terminal.OwnershipChanged -= ChangedOwner;
                        }

                        if (fat as IMyTextSurface == null) continue;
                        
                        var lcd = fat as IMyTextSurface;
                        if (lcd != null)
                        {
                            if (terminal.CustomName.Contains("[RewardLeaderboard]"))
                            {
                                if (ActiveLCDs.Contains(terminal))
                                {
                                    ActiveLCDs.Remove(terminal);
                                }
                            }
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {

                }
            });
        }

        public void FatBlockAdded(IMyCubeBlock block)
        {
            var slim = block.SlimBlock;
            if (slim == null) return;

            if (!cachedIntegrity.ContainsKey(block))
            {
                cachedIntegrity.TryAdd(block, slim.BuildLevelRatio);
            }

            var terminal = block as IMyTerminalBlock;
            if (terminal == null) return;

            if (!blockOwners.ContainsKey(terminal))
            {
                blockOwners.TryAdd(terminal, terminal.OwnerId);
                terminal.OwnershipChanged += ChangedOwner;
            }

            if (block as IMyTextSurface == null) return;


            if(block as IMyTextSurface != null)
            {
                terminal.CustomNameChanged += TextSurfaceNameChanged;
            }
        }

        public void ChangedOwner(IMyTerminalBlock block)
        {
            if (block.OwnerId == 0) return;
            if (blockOwners.ContainsKey(block))
            {
                blockOwners[block] = block.OwnerId;
            }
        }

        public void OnBlockIntegrityChanged(IMySlimBlock slim)
        {
            MyAPIGateway.Parallel.StartBackground(() =>
            {
                if (slim == null) return;
                var fat = slim.FatBlock;
                if (fat == null) return;

                lock (integrityQueue)
                {
                    integrityQueue.Enqueue(fat);
                }

                if (!processIntegrityQueue)
                {
                    processIntegrityQueue = true;
                    ProcessIntegrityQueue();
                }
            });
        }

        private void ProcessIntegrityQueue()
        {
            ProcessQueue:
            IMyCubeBlock block = null;
            integrityQueue.TryDequeue(out block);
            if (block == null) goto CheckCount;

            var slim = block.SlimBlock;
            if (slim == null) goto CheckCount;

            if (!cachedIntegrity.ContainsKey(block)) return;
            if (slim.BuildLevelRatio != cachedIntegrity[block])
            {
                cachedIntegrity[block] = slim.BuildLevelRatio;
                //MyVisualScriptLogicProvider.ShowNotification($"BuildLevel Changed = {slim.BuildLevelRatio}", 3000);
            }

            CheckCount:
            if(integrityQueue.Count == 0)
            {
                processIntegrityQueue = false;
                return;
            }

            MyAPIGateway.Parallel.Sleep(200);
            goto ProcessQueue;
        }

        public void TextSurfaceNameChanged(IMyTerminalBlock block)
        {
            if (block as IMyTextSurface == null) return;

            if (block.CustomName.Contains("[RewardLeaderboard]"))
            {
                if (ActiveLCDs.Contains(block)) return;
                ActiveLCDs.Add(block);

                ChatHandler.UpdateLeaderboards(false, true);
                return;
            }

            if (block.CustomName.Contains("[RewardLeaderboardPvE]"))
            {
                if (ActiveLCDsPVE.Contains(block)) return;
                ActiveLCDsPVE.Add(block);

                ChatHandler.UpdateLeaderboards(true, true);
                return;
            }

            if (ActiveLCDs.Contains(block))
                ActiveLCDs.Remove(block);

            if (ActiveLCDsPVE.Contains(block))
                ActiveLCDsPVE.Remove(block);
        }

        public void BeforeDamage(object baseObject, ref MyDamageInformation info)
        {
            if (!config.enabled) return;
            var slim = baseObject as IMySlimBlock;
            if (slim == null) return;

            var fat = slim.FatBlock;
            if (fat == null) return;

            
            string damageType = info.Type.String;
            long attackerId = info.AttackerId;

            IMyEntity entity = null;
            MyAPIGateway.Entities.TryGetEntityById(info.AttackerId, out entity);

            MyAPIGateway.Parallel.StartBackground(() =>
            {
                try
                {
                    Vector3D fatPOS = fat.GetPosition();
                    //MyVisualScriptLogicProvider.ShowNotificationToAll($"Block integrity before = {slim.Integrity}", 1000, "Red");
                    MyAPIGateway.Parallel.Sleep(200);
                    //MyVisualScriptLogicProvider.ShowNotificationToAll($"Block integrity after = {slim.Integrity}", 1000, "Green");
                    if (slim.Integrity != 0) return;

                    var data = new QueueData();
                    data.attacker = entity;
                    data.attackerID = attackerId;
                    data.damageType = damageType;
                    data.fat = fat;
                    data.slim = slim;
                    data.fatPOS = fatPOS;
                    //MyVisualScriptLogicProvider.ShowNotificationToAll($"Process Queue = {processQueue}", 2000, "Red");
                    if (queueList.Count == 0) processQueue = false;
                    queueList.Enqueue(data);

                    if (!processQueue)
                    {
                        processQueue = true;
                        ProcessQueue();
                    }
                }
                catch (Exception ex)
                {
                    //MyVisualScriptLogicProvider.ShowNotificationToAll($"Error {ex}", 15000);
                    processQueue = false;
                }
            });
        }

        private void ProcessQueue()
        {
        ProcessData:
            if (Instance == null) return;
            QueueData data;
            //MyVisualScriptLogicProvider.ShowNotification($"Number in queue = {queueList.Count}", 2000);
            if (!queueList.TryDequeue(out data)) goto CheckListCount;

            IMySlimBlock slim = data.slim;
            IMyCubeBlock fat = data.fat;
            long attackerId = data.attackerID;
            Vector3D fatPOS = data.fatPOS;
            string damageType = data.damageType;
            IMyEntity entity = data.attacker;
            IMyTerminalBlock block = fat as IMyTerminalBlock;
            bool isAttackerNPC = false;
            /*if (!fat.MarkedForClose)
                    {
                        //MyVisualScriptLogicProvider.ShowNotificationToAll($"Block NOT closed", 5000, "Green");
                        return;
                    }*/

            //MyVisualScriptLogicProvider.ShowNotificationToAll($"Block closed", 5000, "Red");
            long blockOwner = 0;
            if(block != null && blockOwners.ContainsKey(block))
            {
                blockOwners.TryGetValue(block, out blockOwner);
                //blockOwners.Remove(block);
            }

            if (blockOwner == 0)
            {
                IMyCubeGrid grid = fat.CubeGrid;
                blockOwner = grid.BigOwners.FirstOrDefault();
            }

            IMyFaction ownerFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(blockOwner);
            if (ownerFaction == null)
            {
                //MyVisualScriptLogicProvider.ShowNotificationToAll($"Ownerfaction is null", 5000);
                goto CheckListCount;
            }

            IMyFaction attackerFaction = null;
            /*if (damageType == "Deformation")
            {
                if(attackerId == 0)
                {
                    attackerFaction = CheckForMissile(fat.GetPosition(), damageType, ownerFaction);
                }
                MyAPIGateway.Parallel.Sleep(200);
            }*/

            //MyVisualScriptLogicProvider.ShowNotificationToAll($"AttackerId = {attackerId}", 10000);
            if (cachedIntegrity.ContainsKey(fat))
            {
                //MyVisualScriptLogicProvider.ShowNotificationToAll($"CheckPoint 1", 20000);
                //MyVisualScriptLogicProvider.ShowNotificationToAll($"BuildLevel = {cachedIntegrity[fat]}", 10000);
                if (cachedIntegrity[fat] < .90f)
                {
                    cachedIntegrity.Remove(fat);
                    goto CheckListCount;
                }

                cachedIntegrity.Remove(fat);
            }
            else
            {
                //MyVisualScriptLogicProvider.ShowNotificationToAll($"Block not found", 10000, "Red");
                goto CheckListCount;
            }

            if (damageType == "Grind") goto CheckListCount;

            if (attackerId == 0)
            {
                //MyVisualScriptLogicProvider.ShowNotificationToAll($"Looking for missile", 2000, "Green");
                //MyAPIGateway.Parallel.Sleep(200);
                attackerFaction = CheckForMissile(fatPOS, damageType, ownerFaction);
            }


            //MyVisualScriptLogicProvider.ShowNotificationToAll($"CheckPoint 2", 30000);
            /*long blockOwner = slim.OwnerId;
            if (blockOwner == 0)
            {
                IMyCubeGrid grid = fat.CubeGrid;
                blockOwner = grid.BigOwners.FirstOrDefault();
            }

            IMyFaction ownerFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(blockOwner);
            if (ownerFaction == null)
            {
                MyVisualScriptLogicProvider.ShowNotificationToAll($"Ownerfaction is null", 20000);
                return;
            }*/

            

            if (attackerId != 0)
            {
                //MyVisualScriptLogicProvider.ShowNotificationToAll($"Attacker is found", 6000, "Green");
                attackerFaction = GetAttackerFromType(entity, damageType, ownerFaction, fatPOS);
            }

            /*if (attackerId == 0)
            {
                attackerFaction = CheckForMissile(fat.GetPosition(), damageType);
            }
            else
            {
                attackerFaction = GetAttackerFromType(entity, damageType);
            }*/
            //MyVisualScriptLogicProvider.ShowNotificationToAll($"CheckPoint 3", 20000);
            if (attackerFaction == null)
            {
                //MyVisualScriptLogicProvider.ShowNotificationToAll($"Attacker Faction null", 10000);
                goto CheckListCount;
            }

            if (ownerFaction == attackerFaction) goto CheckListCount;
            var relation = MyAPIGateway.Session.Factions.GetRelationBetweenFactions(ownerFaction.FactionId, attackerFaction.FactionId);
            if (relation != MyRelationsBetweenFactions.Enemies) goto CheckListCount;

            Dictionary<string, MyFixedPoint> rewardYieldFromBlock = GetBlockRewardYield(slim, attackerFaction);

            //IMyCubeGrid cubeGridOwner = slim.CubeGrid;
            //MyVisualScriptLogicProvider.ShowNotificationToAll($"CheckPoint 4", 20000);
            int attackerIndex = RewardDatabase.FindDataIndex(Database, attackerFaction);
            int ownerIndex = RewardDatabase.FindDataIndex(Database, ownerFaction);
            //if (attackerIndex == -1 || ownerIndex == -1) return;
            //MyVisualScriptLogicProvider.ShowNotificationToAll($"OwnerIndex = {ownerIndex}", 10000);
            //MyVisualScriptLogicProvider.ShowNotificationToAll($"AttackerIndex = {attackerIndex}", 10000);
            long SCReward = GetSCReward(slim);
            //MyVisualScriptLogicProvider.ShowNotification($"CheckPoint 6", 20000);
            AddRewardsToFaction(attackerFaction, ownerFaction, attackerIndex, rewardYieldFromBlock, SCReward, ref isAttackerNPC);
            //MyVisualScriptLogicProvider.ShowNotification($"CheckPoint 7", 20000);
            LogAttacker(attackerFaction, ownerFaction, attackerIndex, SCReward, fat);
            //MyVisualScriptLogicProvider.ShowNotification($"CheckPoint 8", 20000);
            RemovePointsFromOwner(ownerFaction, attackerFaction, ownerIndex, SCReward, ref isAttackerNPC);
            //MyVisualScriptLogicProvider.ShowNotification($"CheckPoint 9", 20000);
            LogOwner(ownerFaction, attackerFaction, ownerIndex, SCReward, fat);
            //MyVisualScriptLogicProvider.ShowNotification($"CheckPoint 10", 20000);

            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                ChatHandler.UpdateLeaderboards(isAttackerNPC);
                if (config.crossServer) UpdateCrossServer(attackerIndex, ownerIndex);
            });

            CheckListCount:
            if(queueList.Count == 0)
            {
                processQueue = false;
                missiles.Clear();
                warHeads.Clear();
                //MyVisualScriptLogicProvider.ShowNotification($"Process Queue False", 3000, "Green");
                return;
            }

            MyAPIGateway.Parallel.Sleep(50);
            if (Instance == null) return;
            goto ProcessData;
        }

        public void UpdateCrossServer(int attacker, int owner, bool updateAll = false)
        {
            CrossServerSync data = new CrossServerSync();
            if (MyAPIGateway.Utilities.FileExistsInLocalStorage($"{CrossServer.writeFile}.xml", typeof(CrossServerSync)) == true)
            {
                try
                {
                    using (var reader = MyAPIGateway.Utilities.ReadFileInLocalStorage($"{CrossServer.writeFile}.xml", typeof(CrossServerSync)))
                    {
                        string content = reader.ReadToEnd();

                        reader.Close();
                        byte[] byteData = Convert.FromBase64String(content);
                        data = MyAPIGateway.Utilities.SerializeFromBinary<CrossServerSync>(byteData);
                    }

                    data = AddFactionData(data, attacker, owner, updateAll);

                    using (var writer = MyAPIGateway.Utilities.WriteFileInLocalStorage($"{CrossServer.writeFile}.xml", typeof(CrossServerSync)))
                    {
                        var newByteData = MyAPIGateway.Utilities.SerializeToBinary(data);
                        var base64string = Convert.ToBase64String(newByteData);
                        writer.Write(base64string);
                        writer.Close();
                    }

                    RewardConfig.SaveDatabase(Database);
                    return;
                }
                catch (Exception ex)
                {
                    //MyVisualScriptLogicProvider.ShowNotification($"failed to write synce data {ex}", 10000);
                    return;
                }
            }

            List<RewardDatabase> temp = new List<RewardDatabase>();
            if (attacker != -1 && !updateAll)
            {
                temp.Add(Database[attacker]);
            }

            if (owner != -1 && !updateAll)
            {
                temp.Add(Database[owner]);
            }

            if (updateAll)
            {
                foreach(var item in Database)
                {
                    temp.Add(item);
                }
            }

            data.factionData = temp.ToArray();

            using (var writer = MyAPIGateway.Utilities.WriteFileInLocalStorage($"{CrossServer.writeFile}.xml", typeof(CrossServerSync)))
            {
                var newByteData = MyAPIGateway.Utilities.SerializeToBinary(data);
                var base64string = Convert.ToBase64String(newByteData);
                writer.Write(base64string);
                writer.Close();
            }
        }

        private CrossServerSync AddFactionData(CrossServerSync data, int attacker, int owner, bool updateAll)
        {
            
            bool foundAttacker = false;
            bool foundOwner = false;
            CrossServerSync newSync = new CrossServerSync();
            List<RewardDatabase> temp = new List<RewardDatabase>(data.factionData.ToList());

            if (updateAll)
            {
                temp.Clear();
                temp = Database.ToList();
                newSync.factionData = temp.ToArray();
                return newSync;
            }

            for (int i = 0; i < temp.Count; i++)
            {
                if(attacker != -1)
                {
                    // FactionCheck
                    if (temp[i].factionId == Database[attacker].factionId)
                    {
                        temp[i] = Database[attacker];
                        foundAttacker = true;
                    }
                }
                
                if(owner != -1)
                {
                    // FactionCheck
                    if (temp[i].factionId == Database[owner].factionId)
                    {
                        temp[i] = Database[owner];
                        foundOwner = true;
                    }
                }
            }

            if(foundAttacker && foundOwner)
            {
                newSync.factionData = temp.ToArray();
                return newSync;
            }

            if (!foundAttacker && attacker != -1)
            {
                temp.Add(Database[attacker]);
            }

            if (!foundOwner && owner != -1)
            {
                temp.Add(Database[owner]);
            }

            newSync.factionData = temp.ToArray();
            return newSync;
        }

        public IMyFaction CheckForMissile(Vector3D pos, string damageType, IMyFaction ownerFaction)
        {
            /*if(damageType == "Deformation")
            {
                MyVisualScriptLogicProvider.ShowNotificationToAll($"Damage is Deformation", 10000, "Red");
                var sphere = new BoundingSphereD(pos, 50);
                List<IMyEntity> ents = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);

                foreach (var entity in ents)
                {
                    var grid = entity as MyCubeGrid;
                    if (grid == null) continue;
                    
                    var owner = grid.BigOwners.FirstOrDefault();
                    //MyVisualScriptLogicProvider.ShowNotificationToAll($"Owner = {owner}", 50000, "Red");
                    IMyFaction attackerFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(owner);
                    if (attackerFaction == null) continue;
                    if (ownerFaction == attackerFaction) continue;
                    var relation = MyAPIGateway.Session.Factions.GetRelationBetweenFactions(ownerFaction.FactionId, attackerFaction.FactionId);
                    if (relation != VRage.Game.MyRelationsBetweenFactions.Enemies) continue;

                    return attackerFaction;
                }

                return null;
            }*/

            var temp = new Dictionary<MissileData, TimeSpan>();
            IMyEntity launcherEnt = null;
            IMyFaction attacker = null;
            long ownerId = 0;
            if (missiles.Count != 0)
            {
                List<MissileData> tmp = new List<MissileData>(missiles);
                foreach (var item in tmp)
                {
                    var time = DateTime.Now - item.dateTime;
                    temp.Add(item, time);
                }

                var items = from pair in temp
                            orderby pair.Value ascending
                            select pair;

                foreach (var pair in items)
                {
                    if (Vector3D.Distance(pos, pair.Key.lastPos) < 10)
                    {
                        ownerId = pair.Key.launcherId;
                        if (ownerId == 0)
                        {
                            //MyVisualScriptLogicProvider.ShowNotificationToAll($"Owner is 0", 9000, "Red");
                            continue;
                        }

                        //blockEnts.TryGetValue(pair.Key.ob.LauncherId, out launcherEnt);

                        /*MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {
                            //MyVisualScriptLogicProvider.ShowNotificationToAll($"Distance = {Vector3D.Distance(pos, pair.Key.lastPos)}", 20000);
                            //MyVisualScriptLogicProvider.ShowNotificationToAll($"LauncherId = {pair.Key.ob.LauncherId}", 20000);
                            MyAPIGateway.Entities.TryGetEntityById(pair.Key.ob.LauncherId, out launcherEnt);
                            if (launcherEnt == null)
                            {
                                MyVisualScriptLogicProvider.ShowNotificationToAll($"Launcher is null", 20000, "Red");
                            }
                        });*/

                        //MyAPIGateway.Parallel.Sleep(500);
                        break;
                    }
                }
            }
            
            if (ownerId == 0)
            {
                if (warHeads.Count != 0)
                {
                    attacker = CheckForWarhead(pos, damageType, ownerFaction);
                    if (attacker != null) return attacker;
                }

                //MyVisualScriptLogicProvider.ShowNotificationToAll($"Launcher is null 2", 3000, "Red");
                return attacker = CheckCollision(pos, damageType, ownerFaction);
            }
            /*var launcher = launcherEnt as IMyUserControllableGun;
            if (launcher == null)
            {
                MyVisualScriptLogicProvider.ShowNotificationToAll($"Launcher is null 3", 20000, "Red");
                return null;
            }
            
            MyVisualScriptLogicProvider.ShowNotificationToAll($"Launcher Owner = {launcher.OwnerId}", 9000, "Green");*/
            attacker = MyAPIGateway.Session.Factions.TryGetPlayerFaction(ownerId);
            if(attacker == null)
            {
                return attacker = CheckCollision(pos, damageType, ownerFaction);
                
            }
            //MyVisualScriptLogicProvider.ShowNotificationToAll($"Faction = {attacker.Tag}", 5000, "Green");
            return attacker;
        }

        public IMyFaction CheckCollision(Vector3D pos, string damageType, IMyFaction ownerFaction)
        {
            if(damageType == "Deformation")
            {
                //MyVisualScriptLogicProvider.ShowNotificationToAll($"Damage is Deformation", 3000, "Red");
                var sphere = new BoundingSphereD(pos, 200);
                List<IMyEntity> ents = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref sphere);

                foreach (var entity in ents)
                {
                    var grid = entity as MyCubeGrid;
                    if (grid == null) continue;
                    
                    var owner = grid.BigOwners.FirstOrDefault();
                    //MyVisualScriptLogicProvider.ShowNotificationToAll($"Owner = {owner}", 50000, "Red");
                    IMyFaction attackerFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(owner);
                    if (attackerFaction == null) continue;
                    if (ownerFaction == attackerFaction) continue;
                    var relation = MyAPIGateway.Session.Factions.GetRelationBetweenFactions(ownerFaction.FactionId, attackerFaction.FactionId);
                    if (relation != VRage.Game.MyRelationsBetweenFactions.Enemies) continue;

                    return attackerFaction;
                }

                return null;
            }

            return null;
        }

        public IMyFaction CheckForWarhead(Vector3D pos, string damageType, IMyFaction ownerFaction)
        {
            
            var temp = new Dictionary<WarheadData, TimeSpan>();
            IMyFaction attacker = null;
            if (warHeads.Count == 0) return null;
            foreach (var item in warHeads)
            {
                var time = DateTime.Now - item.dateTime;
                temp.Add(item, time);
            }

            var items = from pair in temp
                        orderby pair.Value ascending
                        select pair;
            
            foreach (var pair in items)
            {
                if (Vector3D.Distance(pos, pair.Key.lastPos) < 25)
                {
                    long owner = pair.Key.owner;
                    //MyVisualScriptLogicProvider.ShowNotificationToAll($"warhead owner = {owner}", 20000, "Red");
                    attacker = MyAPIGateway.Session.Factions.TryGetPlayerFaction(owner);
                    
                    break;
                }
            }
            //MyAPIGateway.Parallel.Sleep(1000);
            //warHeads.Clear();
            return attacker;
        }

        public void LogAttacker(IMyFaction attacker, IMyFaction owner, int index, long points, IMyCubeBlock block)
        {
            if (attacker.IsEveryoneNpc()) return;
            if (index == -1) return;
            //MyAPIGateway.Parallel.Sleep(100);
            IMyCubeGrid cubeGrid = block.CubeGrid;
            string blockId = block.BlockDefinition.TypeId.ToString();
            blockId = blockId.Replace("MyObjectBuilder_", "");
            string log = $"Your faction destroyed block [ {blockId} ] from grid [ {cubeGrid.CustomName} ] against [ {owner.Name} ] for a gain of {points} points.";

            RewardDatabase.WriteLog(ref Database[index].logger, log);
        }

        public void LogOwner(IMyFaction owner, IMyFaction attacker, int index, long points, IMyCubeBlock block)
        {
            if (owner.IsEveryoneNpc()) return;
            if (index == -1) return;
            //MyAPIGateway.Parallel.Sleep(100);
            IMyCubeGrid cubeGrid = block.CubeGrid;
            string blockId = block.BlockDefinition.TypeId.ToString();
            blockId = blockId.Replace("MyObjectBuilder_", "");
            string log = $"[ {attacker.Name} ] destroyed block [ {blockId} ] from grid [ {cubeGrid.CustomName} ] for a loss of {points} points.";

            RewardDatabase.WriteLog(ref Database[index].logger, log);
        }

        public void RemovePointsFromOwner(IMyFaction ownerFaction, IMyFaction attackerFaction, int index, long points, ref bool isAttackerNPC)
        {
            if (ownerFaction.IsEveryoneNpc()) return;
            if (index == -1) return;

            if (attackerFaction.IsEveryoneNpc())
            {
                if (Database[index].npcPoints <= 0) return;
                Database[index].npcPoints -= points;
                isAttackerNPC = true;
            }
            else
            {
                if (Database[index].points <= 0) return;
                Database[index].points -= points;
            }
        }

        // Uncommit code in here when ready for bounty
        public void AddRewardsToFaction(IMyFaction attackerFaction, IMyFaction ownerFaction, int index, Dictionary<string, MyFixedPoint> ingotRewards, long scReward, ref bool isAttackerNPC)
        {
            if (attackerFaction.IsEveryoneNpc()) return;
            if (index == -1) return;
            foreach (var key in ingotRewards.Keys)
            {
                bool exists = false;
                if (Database[index].rewards != null)
                {
                    if (Database[index].rewards.Length != 0)
                    {
                        for (int i = 0; i < Database[index].rewards.Length; i++)
                        {
                            if (Database[index].rewards[i].Ingot == key)
                            {
                                Database[index].rewards[i].Amount += ingotRewards[key];
                                exists = true;
                                break;
                            }
                        }
                    }                   
                }

                if (exists) continue;
                RewardIngots newReward = new RewardIngots();
                newReward.Ingot = key;
                newReward.Amount = ingotRewards[key];
                List<RewardIngots> temp = new List<RewardIngots>();

                if(Database[index].rewards.Length == 0)
                {
                    temp.Add(newReward);
                }
                else
                {
                    temp = Database[index].rewards.ToList();
                    temp.Add(newReward);
                }

                Database[index].rewards = temp.ToArray();
            }

            if (ownerFaction.IsEveryoneNpc())
            {
                // Uncommit this when ready to init bounty for NPCs
                Database[index].npcPoints += scReward;
                isAttackerNPC = true;
            }
            else
            {
                Database[index].points += scReward;
            }

            IMyFaction faction = Database[index].faction;
            float sc = scReward * config.SCMultiplier;
            faction.RequestChangeBalance((long)sc);
        }

        public long GetSCReward(IMySlimBlock slim)
        {
            if (config.dynamicalPoints.enableDynamical)
            {
                float integrityRatio = slim.MaxIntegrity / maxIntegrityGlobal;
                return (long)MathHelper.Lerp(0, config.dynamicalPoints.maxPoints, (double)integrityRatio);
            }

            string defId = slim.BlockDefinition.Id.ToString();
            if (BlockRewards.ContainsKey(defId))
            {
                return BlockRewards[defId];
            }

            return 0;
        }

        public IMyFaction GetAttackerFromType(IMyEntity entity, string type, IMyFaction ownerFaction, Vector3D ownerPosition)
        {
            if (entity == null) return null;
            IMyFaction faction = null;
            var ent = entity as MyEntity;
            
            if (entity.GetType() == typeof(MyCubeGrid))
            {
                long owner = 0;
                //MyVisualScriptLogicProvider.ShowNotificationToAll($"Type is MyCubeGrid", 15000, "Green");
                MyCubeGrid cubegrid = entity as MyCubeGrid;
                if (cubegrid.BigOwners.Count == 0)
                {
                    var block = entity as MyCubeBlock;
                    if(block == null)
                    {
                        faction = CheckForWarhead(ownerPosition, type, ownerFaction);
                        if(faction == null)
                        {
                            //MyVisualScriptLogicProvider.ShowNotificationToAll($"warhead faction is null", 20000, "Red");
                        }

                        return faction;
                    }

                    owner = block.BuiltBy;
                    //MyVisualScriptLogicProvider.ShowNotificationToAll($"Owner = {owner}", 32000, "Green");
                    return MyAPIGateway.Session.Factions.TryGetPlayerFaction(owner);
                }
                faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(cubegrid.BigOwners.FirstOrDefault());
                return faction;
            }
            
            if (entity as IMyUserControllableGun != null)
            {
                var cube = (IMyCubeBlock)entity;
                var slim = cube.SlimBlock;
                var builtBy = slim.OwnerId;
                faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(builtBy);
                return faction;
            }
            
            if (entity as IMyAutomaticRifleGun != null)
            {
                var rifle = (IMyAutomaticRifleGun)entity;
                faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(rifle.OwnerIdentityId);
                return faction;
            }

            if (entity as IMyCharacter != null)
            {
                var character = (IMyCharacter)entity;
                MyIDModule _module;
                ((IMyComponentOwner<MyIDModule>)character).GetComponent(out _module);
                faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(_module.Owner);
                return faction;
            }

            if (entity as IMyHandDrill != null)
            {
                var drill = (IMyHandDrill)entity;
                faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(drill.OwnerIdentityId);
                return faction;
            }

            //MyAPIGateway.Parallel.Sleep(200);
            return CheckForMissile(ownerPosition, type, ownerFaction);
            //MyVisualScriptLogicProvider.ShowNotificationToAll($"Entity Id = {entity.EntityId}", 15000, "Green");
            return null;
        }

        public Dictionary<string, MyFixedPoint> GetBlockRewardYield(IMySlimBlock block, IMyFaction faction)
        {
            Dictionary<string, MyFixedPoint> temp = new Dictionary<string, MyFixedPoint>();
            var definition = block.BlockDefinition;
            MyCubeBlockDefinition baseDef = (MyCubeBlockDefinition)definition;
            var components = baseDef.Components;
            foreach (var comp in components)
            {
                var compCount = comp.Count;
                var compDef = comp.Definition.Id;

                MyBlueprintDefinitionBase bpDef = MyDefinitionManager.Static.TryGetBlueprintDefinitionByResultId(compDef);
                if (bpDef == null)
                {
                    //MyVisualScriptLogicProvider.ShowNotification($"bp is null", 20000);
                }
                MyBlueprintDefinitionBase.Item[] ingots = bpDef.Prerequisites;

                foreach (var item in ingots)
                {
                    string ingotType = item.Id.SubtypeName;
                    MyFixedPoint amount = item.Amount * (MyFixedPoint)compCount;

                    MyFixedPoint finalAmount = amount * (MyFixedPoint)RewardIngotYield;
                    if (faction.IsEveryoneNpc())
                    {
                        finalAmount = amount * (MyFixedPoint)NPCRewardIngotYield;
                    }

                    if (temp.ContainsKey(ingotType))
                    {
                        temp[ingotType] += finalAmount;
                    }
                    else
                    {
                        temp.Add(ingotType, finalAmount);
                    }
                }
            }

            return temp;
        }

        public void NewFaction(long factionId)
        {
            IMyFaction faction = MyAPIGateway.Session.Factions.TryGetFactionById(factionId);
            if (faction == null) return;

            RewardDatabase data = new RewardDatabase(faction.Tag, faction);
            Database.Add(data);
            ChatHandler.UpdateLeaderboards();
        }

        public void FactionEdited(long factionId)
        {
            IMyFaction faction = MyAPIGateway.Session.Factions.TryGetFactionById(factionId);
            if (faction == null) return;

            int index = RewardDatabase.FindDataIndex(Database, faction);
            if (index == -1) return;

            Database[index].factionTag = faction.Tag;
        }

        public void FactionChanged(MyFactionStateChange type, long fromFaction, long toFaction, long playerId, long senderId)
        {
            if(type == MyFactionStateChange.RemoveFaction)
            {
                int index = -1;
                for (int i = 0; i < Database.Count; i++)
                {
                    if(fromFaction == Database[i].factionId)
                    {
                        index = i;
                        break;
                    }
                }

                if (index == -1) return;

                RewardConfig.RemoveFile(fromFaction);
                Database.RemoveAt(index);
                ChatHandler.UpdateLeaderboards();
            }
        }

        protected override void UnloadData()
        {
            base.UnloadData();

            Instance = null;
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(4705, Comms.MessageHandler);
            MyAPIGateway.Utilities.MessageEntered -= ChatHandler.ChatCommands;
            if (!isServer) return;

            MyAPIGateway.Session.Factions.FactionCreated -= NewFaction;
            MyAPIGateway.Session.Factions.FactionStateChanged -= FactionChanged;
            MyAPIGateway.Session.Factions.FactionEdited -= FactionEdited;
            MyAPIGateway.Entities.OnEntityAdd -= EntityAdded;
            MyAPIGateway.Entities.OnEntityRemove -= EntityRemoved;
        }

        public override void SaveData()
        {
            MyAPIGateway.Parallel.StartBackground(() =>
            {
                if (!isServer || !config.enabled) return;
                RewardConfig.SaveDatabase(Database);
            });
        }
    }
}
