using Vintagestory.API.Common;

namespace WearableRenderLibrary
{
    public class WearableRenderLibraryModSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            api.RegisterItemClass("ItemWearableArmorMeshByAttributes", typeof(ItemWearableArmorShapeTexturesByAttributes));
        }
    }
}
