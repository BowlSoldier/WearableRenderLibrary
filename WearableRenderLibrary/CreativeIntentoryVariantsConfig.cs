using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WearableRenderLibrary
{
    // Used to define which visual variants should appear in the creative inventory and handbook.
    public class ShapeTextureConfig
    {
        public string Shape { get; set; } = "";
        public Dictionary<string, string> Textures { get; set; } = new Dictionary<string, string>();
    }

    public class CreativeInventoryVariantsConfig
    {
        public ShapeTextureConfig[] ShapeTextureVariants { get; set; } = Array.Empty<ShapeTextureConfig>();
        public string[] CreativeTabs { get; set; } = Array.Empty<string>();
    }
}
