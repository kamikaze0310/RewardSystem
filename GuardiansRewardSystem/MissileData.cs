using Sandbox.Common.ObjectBuilders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace RewardSystem
{
    public struct MissileData
    {
        public MyObjectBuilder_Missile ob;
        public Vector3D lastPos;
        public DateTime dateTime;
        public long launcherId;
    }

    public struct WarheadData
    {
        public MyObjectBuilder_Warhead ob;
        public Vector3D lastPos;
        public DateTime dateTime;
        public long owner;
    }

    public struct QueueData
    {
        public IMyEntity attacker;
        public string damageType;
        public long attackerID;
        public IMyCubeBlock fat;
        public IMySlimBlock slim;
        public Vector3D fatPOS;

    }
}
