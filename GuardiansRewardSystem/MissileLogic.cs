using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace RewardSystem
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Missile), false)]
    public class Logic : MyGameLogicComponent
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                if (Entity == null) return;
                if (!MyAPIGateway.Multiplayer.IsServer || !Core.config.enabled || Core.Instance == null) return;

                IMyEntity launcherEnt = null;
                MyObjectBuilder_Missile ob = (MyObjectBuilder_Missile)Entity.GetObjectBuilder();
                MyAPIGateway.Entities.TryGetEntityById(ob.LauncherId, out launcherEnt);
                MissileData data = new MissileData();
                data.ob = ob;
                data.lastPos = Entity.GetPosition();
                data.dateTime = DateTime.Now;
                if(launcherEnt != null)
                {
                    var launcher = launcherEnt as IMyUserControllableGun;
                    data.launcherId = launcher.OwnerId;
                }
                
                Core.Instance.missiles.Add(data);

                //MyVisualScriptLogicProvider.ShowNotification($"Added missile data to list", 10000);
            }
            catch(Exception ex)
            {

            }
        }
    }
}
