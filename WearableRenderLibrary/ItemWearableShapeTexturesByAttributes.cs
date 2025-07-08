using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

#nullable disable

namespace WearableRenderLibrary
{
    public class ItemWearableShapeTexturesByAttributes : ItemWearable, IContainedMeshSource, IAttachableToEntity, IWearableShapeSupplier
    {
        bool attachableToEntity;
        ICoreClientAPI _capi;
        Shape nowTesselatingShape;
        CreativeInventoryVariantsConfig _variantsConfig;

        public override void OnLoaded(ICoreAPI api)
        {
            attachableToEntity = IAttachableToEntity.FromCollectible(this) != null;
            base.OnLoaded(api);
            _capi = api as ICoreClientAPI;
            attrAtta = IAttachableToEntity.FromAttributes(this);
            JsonObject variantsAttribute = Attributes["creativeInventoryVariants"];
            _variantsConfig = variantsAttribute.AsObject<CreativeInventoryVariantsConfig>();
            AddAllTypesToCreativeInventory();
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            base.OnUnloaded(api);

            Dictionary<string, MultiTextureMeshRef> meshRefs = ObjectCacheUtil.TryGet<Dictionary<string, MultiTextureMeshRef>>(api, "wearableAttachmentMeshRefs");
            meshRefs?.Foreach(meshRef => meshRef.Value?.Dispose());
            ObjectCacheUtil.Delete(api, "wearableAttachmentMeshRefs");
        }

        public override string GetMeshCacheKey(ItemStack itemstack)
        {
            string cacheKey = "wearableModelRef-" + itemstack.Collectible.Code.ToString();
            if (itemstack.Attributes.TryGetAttribute("shape", out IAttribute shapePath))
            {
                cacheKey += "-" + (string)shapePath.GetValue();
            }
            ITreeAttribute textures = itemstack.Attributes.GetTreeAttribute("textures");
            if (textures != null)
            {
                foreach ((string textureCode, IAttribute textureAttribute) in textures)
                {
                    cacheKey += $"-{textureCode}:{(string)textureAttribute.GetValue()}";
                }
            }

            return cacheKey;
        }

        private Shape GetShapeFromAttributes(ItemStack itemstack)
        {
            CompositeShape cshape = null;
            // If Shape is defined in attributes, use that
            if (itemstack.Attributes.TryGetAttribute("shape", out IAttribute shapePath))
            {
                cshape = new CompositeShape();
                cshape.Base = (string)shapePath.GetValue();
            }
            // Otherwise, use the shape from the item
            else if (itemstack?.Item?.Shape != null)
            {
                cshape = itemstack.Item.Shape;
            }
            else
            {
                api.Logger.Error("Could not load shape for item {1}", itemstack.Item.Code);
                return null;
            }

            AssetLocation shapeloc = cshape.Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            Shape? shape = api.Assets.TryGet(shapeloc)?.ToObject<Shape>();
            if (shape == null)
            {
                api.Logger.Error("Could not load shape {0} for item {1}", shapeloc, itemstack.Item.Code);
                return null;
            }
            return shape;
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (!attachableToEntity) return;

            Dictionary<string, MultiTextureMeshRef> meshrefs = ObjectCacheUtil.GetOrCreate(capi, "wearableAttachmentMeshRefs", () => new Dictionary<string, MultiTextureMeshRef>());
            string key = GetMeshCacheKey(itemstack);

            if (!meshrefs.TryGetValue(key, out renderinfo.ModelRef))
            {
                ITexPositionSource texSource = GenTextureSource(itemstack, capi.ItemTextureAtlas);
                MeshData mesh = genMesh(capi, itemstack, texSource);
                renderinfo.ModelRef = meshrefs[key] = mesh == null ? renderinfo.ModelRef : capi.Render.UploadMultiTextureMesh(mesh);
            }

            if (Attributes["visibleDamageEffect"].AsBool())
            {
                renderinfo.DamageEffect = Math.Max(0, 1 - (float)GetRemainingDurability(itemstack) / GetMaxDurability(itemstack) * 1.1f);
            }
        }

        private ContainedTextureSource GenTextureSource(ItemStack itemstack, ITextureAtlasAPI targetAtlas)
        {
            ContainedTextureSource textureSource = new ContainedTextureSource(_capi, targetAtlas, new Dictionary<string, AssetLocation>(), $"For render in '{itemstack.Item.Code}'");

            textureSource.Textures.Clear();

            // Populate default textures based on the shape first
            Shape shape = GetShapeFromAttributes(itemstack); // Seems inefficient to do this a 2nd time, we're already doing it in genMesh. Perhaps add a second version of GenTextureSource that takes a Shape as an argument?

            if (shape != null)
            {
                foreach (var ctex in shape.Textures)
                {
                    textureSource.Textures[ctex.Key] = ctex.Value;
                }
            }

            // Next, any textures defined on the item variant itself takes priority over the defaults from the Shape
            // This is needed so Steel Plate Bodies don't use Bronze texture from the shape, for example; need to read the values for the Steel variant specifically
            foreach (var ctex in itemstack.Item.Textures)
            {
                textureSource.Textures[ctex.Key] = ctex.Value.Base;
            }

            // Finally, use textures defined in the item attributes as the highest priority
            ITreeAttribute texturesTree = itemstack.Attributes.GetTreeAttribute("textures");
            if (texturesTree != null)
            {
                foreach ((string textureCode, IAttribute textureAttribute) in texturesTree)
                {
                    // No point adding textures which aren't used in the shape
                    if (textureSource.Textures.ContainsKey(textureCode))
                    {
                        textureSource.Textures[textureCode] = new AssetLocation((string)textureAttribute.GetValue());

                    }
                }
            }

            return textureSource;
        }

        protected new MeshData genMesh(ICoreClientAPI capi, ItemStack itemstack, ITexPositionSource texSource)
        {
            JsonObject attrObj = itemstack.Collectible.Attributes;
            EntityProperties props = capi.World.GetEntityType(new AssetLocation(attrObj?["wearerEntityCode"].ToString() ?? "player"));
            Shape entityShape = props.Client.LoadedShape;
            AssetLocation shapePathForLogging = props.Client.Shape.Base;
            Shape newShape;

            if (!attachableToEntity)
            {
                // No need to step parent anything if its just a texture on the seraph
                newShape = entityShape;
            }
            else
            {
                newShape = new Shape()
                {
                    Elements = entityShape.CloneElements(),
                    Animations = entityShape.CloneAnimations(),
                    AnimationsByCrc32 = entityShape.AnimationsByCrc32,
                    JointsById = entityShape.JointsById,
                    TextureWidth = entityShape.TextureWidth,
                    TextureHeight = entityShape.TextureHeight,
                    Textures = null,
                };
            }

            Shape? armorShape = GetShapeFromAttributes(itemstack);

            if (null == armorShape) return new MeshData();

            //TODO: Want to have the armor shape's path for logging, but don't have access to it with the way the methods currently work
            newShape.StepParentShape(armorShape, "foo", shapePathForLogging.ToShortString(), capi.Logger, (key, code) => { });

            nowTesselatingShape = newShape;
            capi.Tesselator.TesselateShapeWithJointIds("entity", newShape, out MeshData mesh, texSource, new Vec3f());
            nowTesselatingShape = null;

            return mesh;
        }

        IAttachableToEntity attrAtta;
        #region IAttachableToEntity
        public int RequiresBehindSlots { get; set; } = 0;
        string IAttachableToEntity.GetCategoryCode(ItemStack stack) => attrAtta?.GetCategoryCode(stack);
        CompositeShape IAttachableToEntity.GetAttachedShape(ItemStack stack, string slotCode) => attrAtta.GetAttachedShape(stack, slotCode);
        string[] IAttachableToEntity.GetDisableElements(ItemStack stack) => attrAtta.GetDisableElements(stack);
        string[] IAttachableToEntity.GetKeepElements(ItemStack stack) => attrAtta.GetKeepElements(stack);
        string IAttachableToEntity.GetTexturePrefixCode(ItemStack stack)
        {
            return attrAtta.GetTexturePrefixCode(stack) + "-customizedarmor";
        }

        void IAttachableToEntity.CollectTextures(ItemStack itemstack, Shape intoShape, string texturePrefixCode, Dictionary<string, CompositeTexture> intoDict)
        {
            ContainedTextureSource textureSource = GenTextureSource(itemstack, _capi.ItemTextureAtlas);
            foreach (var val in textureSource.Textures)
            {
                intoShape.Textures[val.Key] = val.Value;
            }
        }

        public bool IsAttachable(Entity toEntity, ItemStack itemStack) => attachableToEntity;
        #endregion

        Shape IWearableShapeSupplier.GetShape(ItemStack stack, Entity forEntity, string texturePrefixCode)
        {
            Shape shape = GetShapeFromAttributes(stack);

            shape.SubclassForStepParenting(texturePrefixCode);

            return shape;
        }

        private void AddAllTypesToCreativeInventory()
        {
            if (_variantsConfig == null) return;

            List<JsonItemStack> stacks = new();

            foreach (var shapeTextureConfig in _variantsConfig.ShapeTextureVariants)
            {
                string jsonAttributes = "{";
                if (shapeTextureConfig.Shape != "")
                {
                    jsonAttributes += $"shape: \"{shapeTextureConfig.Shape}\"";
                }
                if (shapeTextureConfig.Textures.Count > 0)
                {
                    if (jsonAttributes != "{") jsonAttributes += ", ";
                    jsonAttributes += $"\"textures\": {JsonConvert.SerializeObject(shapeTextureConfig.Textures)}";
                }
                jsonAttributes += "}";
                stacks.Add(GenStackJson(jsonAttributes));
            }

            JsonItemStack noAttributesStack = new()
            {
                Code = this.Code,
                Type = EnumItemClass.Item
            };
            noAttributesStack.Resolve(this.api?.World, "handle type");

            this.CreativeInventoryStacks = new CreativeTabAndStackList[] {
                new() { Stacks = stacks.ToArray(), Tabs = _variantsConfig.CreativeTabs },
                new() { Stacks = new JsonItemStack[] { noAttributesStack }, Tabs = this.CreativeInventoryTabs }
            };
        }

        private JsonItemStack GenStackJson(string json)
        {
            JsonItemStack stackJson = new()
            {
                Code = this.Code,
                Type = EnumItemClass.Item,
                Attributes = new JsonObject(JToken.Parse(json))
            };

            stackJson.Resolve(this.api?.World, "ItemWearableArmorMeshByAttributes type");

            return stackJson;
        }
    }
}
