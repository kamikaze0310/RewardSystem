using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Components;
using VRage.ObjectBuilders;

namespace RewardSystem
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Warhead), false)]
    public class WarheadLogic : MyGameLogicComponent
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
        }

        public override void OnRemovedFromScene()
        {
            base.OnRemovedFromScene();
            if (Entity == null) return;
            if (!MyAPIGateway.Multiplayer.IsServer || !Core.config.enabled) return;

            var warhead = (IMyWarhead)Entity;
            var slim = warhead.SlimBlock;
            if (warhead == null) return;
            var ob = warhead.GetObjectBuilderCubeBlock() as MyObjectBuilder_Warhead;

            WarheadData data = new WarheadData();
            data.ob = ob;
            data.owner = slim.BuiltBy;
            data.lastPos = Entity.GetPosition();
            data.dateTime = DateTime.Now;
            Core.Instance.warHeads.Add(data);

            //MyVisualScriptLogicProvider.ShowNotification($"Added warhead data to list = {slim.BuiltBy}", 10000);
        }
    }
}
