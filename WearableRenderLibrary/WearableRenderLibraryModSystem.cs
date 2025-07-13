using System;
using System.IO;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.Common;

namespace WearableRenderLibrary
{
    public class WearableRenderLibraryModSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            api.RegisterItemClass("ItemWearableShapeTexturesByAttributes", typeof(ItemWearableShapeTexturesByAttributes));
            if (api.ModLoader.IsModEnabled("overhaullib"))
            {
                Assembly overhaullibCompatability = Assembly.LoadFile($"{new FileInfo(((ModContainer)this.Mod).FolderPath).FullName}/native/OverhaulLibCompat.dll");
                if (overhaullibCompatability == null)
                {
                    api.Logger.Error("Could not load OverhaulLibCompat.dll");
                    return;
                }
                Type armorClassType = overhaullibCompatability.GetType("WearableRenderLibrary.OverhaulLibCompat.ItemWearableArmorShapeTexturesByAttributes");
                if (armorClassType == null)
                {
                    api.Logger.Error("Could not find ItemWearableArmorShapeTexturesByAttributes type in OverhaulLibCompat.dll");
                    return;
                }
                api.RegisterItemClass("ItemWearableArmorShapeTexturesByAttributes", armorClassType);
            }
        }
    }
}
