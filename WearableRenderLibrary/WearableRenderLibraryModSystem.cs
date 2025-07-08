using Vintagestory.API.Common;

namespace WearableRenderLibrary
{
    public class WearableRenderLibraryModSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            api.RegisterItemClass("ItemWearableShapeTexturesByAttributes", typeof(ItemWearableShapeTexturesByAttributes));
            if (api.ModLoader.IsModEnabled("overhaullib"))
            {
                api.RegisterItemClass("ItemWearableArmorShapeTexturesByAttributes", typeof(ItemWearableArmorShapeTexturesByAttributes));
            }
        }
    }
}
