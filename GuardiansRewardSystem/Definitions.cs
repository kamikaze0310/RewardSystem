using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RewardSystem
{
    public class Definitions
    {
        private static readonly float InventoryMultiplier = MyAPIGateway.Session.SessionSettings.BlocksInventorySizeMultiplier;
        
        // Large Grid Max Volumes
        private static readonly double LargeBlockSmallContainer = 15.625 * InventoryMultiplier;
        private static readonly double LargeBlockLargeContainer = 421.875008 * InventoryMultiplier;

        // Small Grid Max Volumes
        private static readonly double SmallBlockSmallContainer = 0.125 * InventoryMultiplier;
        private static readonly double SmallBlockMediumContainer = 3.375 * InventoryMultiplier;
        private static readonly double SmallBlockLargeContainer = 15.625 * InventoryMultiplier;

        public static double GetContainerMaxVolume(string blockSubname)
        {
            if (blockSubname == "LargeBlockSmallContainer") return LargeBlockSmallContainer;
            if (blockSubname == "LargeBlockLargeContainer") return LargeBlockLargeContainer;
            if (blockSubname == "SmallBlockSmallContainer") return SmallBlockSmallContainer;
            if (blockSubname == "SmallBlockMediumContainer") return SmallBlockMediumContainer;
            if (blockSubname == "SmallBlockLargeContainer") return SmallBlockLargeContainer;

            return 0;
        }
    }
}
