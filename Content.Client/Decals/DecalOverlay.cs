using Content.Shared.Decals;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Decals
{
    public sealed class DecalOverlay : Overlay
    {
        private readonly DecalSystem _decals;
        private readonly SpriteSystem _sprites;
        private readonly IEntityManager _entManager;
        private readonly IPrototypeManager _prototypeManager;

        public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

        private readonly Dictionary<string, (Texture Texture, bool SnapCardinals)> _cachedTextures = new(64);

        public DecalOverlay(
            DecalSystem decals,
            SpriteSystem sprites,
            IEntityManager entManager,
            IPrototypeManager prototypeManager)
        {
            _decals = decals;
            _sprites = sprites;
            _entManager = entManager;
            _prototypeManager = prototypeManager;
        }

        protected override void Draw(in OverlayDrawArgs args)
        {
            // Shouldn't need to clear cached textures unless the prototypes get reloaded.
            var handle = args.WorldHandle;
            var xformQuery = _entManager.GetEntityQuery<TransformComponent>();
            var eyeAngle = args.Viewport.Eye?.Rotation ?? Angle.Zero;

            foreach (var (gridId, zIndexDictionary) in _decals.DecalRenderIndex)
            {
                if (zIndexDictionary.Count == 0)
                    continue;

                if (!xformQuery.TryGetComponent(gridId, out var xform))
                {
                    Logger.Error($"Tried to draw decals on a non-existent grid. GridUid: {gridId}");
                    continue;
                }

                if (xform.MapID != args.MapId)
                    continue;

                var (_, worldRot, worldMatrix) = xform.GetWorldPositionRotationMatrix(xformQuery);

                handle.SetTransform(worldMatrix);

                foreach (var decals in zIndexDictionary.Values)
                {
                    foreach (var decal in decals.Values)
                    {
                        if (!_cachedTextures.TryGetValue(decal.Id, out var cache) && _prototypeManager.TryIndex<DecalPrototype>(decal.Id, out var decalProto))
                        {
                            var sprite = GetDecalSprite(decal.Id);
                            cache = (_sprites.Frame0(sprite), decalProto.SnapCardinals);
                            _cachedTextures[decal.Id] = cache;
                        }

                        var cardinal = Angle.Zero;

                        if (cache.SnapCardinals)
                        {
                            var worldAngle = eyeAngle + worldRot;
                            cardinal = worldAngle.GetCardinalDir().ToAngle();
                        }

                        var angle = decal.Angle - cardinal;

                        if (angle.Equals(Angle.Zero))
                            handle.DrawTexture(cache.Texture, decal.Coordinates, decal.Color);
                        else
                            handle.DrawTexture(cache.Texture, decal.Coordinates, angle, decal.Color);
                    }
                }
            }

            handle.SetTransform(Matrix3.Identity);
        }

        public SpriteSpecifier GetDecalSprite(string id)
        {
            if (_prototypeManager.TryIndex<DecalPrototype>(id, out var proto))
                return proto.Sprite;

            Logger.Error($"Unknown decal prototype: {id}");
            return new SpriteSpecifier.Texture(new ResourcePath("/Textures/noSprite.png"));
        }
    }
}
