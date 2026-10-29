using UnityEngine;
using UtsavNights.Content;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Placement;

namespace UtsavNights.Gameplay
{
    /// <summary>Result of checking one spot.</summary>
    public struct PlacementCheck
    {
        public bool Valid => Block == PlacementBlock.None;

        public PlacementBlock Block;
        public Vector3 Point;
        public Vector3 Normal;
    }

    /// <summary>
    /// Gathers placement facts with physics and lets <see cref="PlacementRules"/> decide (PRD 6.3, 9.1, 10).
    /// Footprint and effect-volume overlaps ignore the surface itself; spacing uses every registered fuse.
    /// Allocation-free (shared overlap buffer).
    /// </summary>
    public sealed class PlacementValidator
    {
        public const float MaxRayDistance = 30f;
        public const float FootprintHeight = 0.25f;

        private readonly Collider[] _overlaps = new Collider[16];

        public PlacementValidator(float maxReach = 7f, float minimumSpacing = 0.45f)
        {
            MaxReach = maxReach;
            MinimumSpacing = minimumSpacing;
        }

        /// <summary>Furthest a cracker may be placed from the camera, in metres.</summary>
        public float MaxReach { get; set; }

        public float MinimumSpacing { get; set; }

        /// <summary>
        /// The celebration spot (Phase 8): its play area bounds placement unless <see cref="Bounded"/> is false (the
        /// courtyard, bounded by its walls), and its aerials rule and pads apply. Null behaves as the open courtyard.
        /// </summary>
        public SpotDef Spot { get; set; }

        public bool Bounded { get; set; }

        /// <summary>True when the player's strung lights or garlands pass above a world point (aerials would hit them).</summary>
        public System.Func<Vector3, bool> StrandAbove { get; set; }

        /// <summary>How high the sky above an aerial must be clear, in metres.</summary>
        public const float SkyHeight = 40f;

        public static bool IsAerial(CrackerDefinition d) =>
            d.Category == CrackerCategory.Rocket || d.Category == CrackerCategory.Skyshot || d.Category == CrackerCategory.MultiShotCake;

        public PlacementCheck Check(CrackerDefinition definition, Ray ray, int itemsPlaced, int itemLimit)
        {
            var check = new PlacementCheck();
            var facts = new PlacementFacts
            {
                AllowedSurfaces = (int)definition.Surfaces,
                ItemsPlaced = itemsPlaced,
                ItemLimit = itemLimit,
                MinimumSpacing = MinimumSpacing,
                NearestItemDistance = float.MaxValue,
                FootprintClear = true,
                EffectVolumeClear = true,
            };

            if (Physics.Raycast(ray, out RaycastHit hit, MaxRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                facts.HitSurface = true;
                check.Point = hit.point;
                check.Normal = hit.normal;
                PlacementSurfaceMarker marker = hit.collider.GetComponentInParent<PlacementSurfaceMarker>();
                facts.SurfaceFlags = marker != null ? (int)marker.Kind : 0;
                facts.SlopeDegrees = Vector3.Angle(hit.normal, Vector3.up);
                facts.InReach = hit.distance <= MaxReach;
                facts.FootprintClear = IsClear(hit.point + Vector3.up * (FootprintHeight * 0.5f + 0.02f),
                    new Vector3(definition.Footprint.x * 0.5f, FootprintHeight * 0.5f, definition.Footprint.y * 0.5f), hit.collider);
                Vector3 volume = definition.EffectVolume;
                facts.EffectVolumeClear = IsClear(hit.point + Vector3.up * (FootprintHeight + volume.y * 0.5f),
                    new Vector3(volume.x * 0.5f, volume.y * 0.5f, volume.z * 0.5f), hit.collider);
                facts.NearestItemDistance = NearestFuseDistance(hit.point);
                var p = new V3(hit.point.x, hit.point.y, hit.point.z);
                facts.OutsideArea = Spot != null && Bounded && !CelebrationRules.InArea(Spot, p);
                facts.IsAerial = IsAerial(definition);
                facts.AerialRule = CelebrationRules.RuleOf(Spot);
                facts.OnPad = Spot != null && CelebrationRules.OnPad(Spot, p.X, p.Z);
                if (facts.IsAerial)
                {
                    bool roof = Physics.Raycast(hit.point + Vector3.up * 0.3f, Vector3.up, SkyHeight, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    facts.SkyClear = !roof && (StrandAbove == null || !StrandAbove(hit.point));
                }
            }

            check.Block = PlacementRules.Evaluate(facts);
            return check;
        }

        private bool IsClear(Vector3 center, Vector3 halfExtents, Collider surface)
        {
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlaps, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (_overlaps[i] != surface)
                {
                    return false;
                }
            }

            return true;
        }

        private static float NearestFuseDistance(Vector3 point)
        {
            float best = float.MaxValue;
            var all = IgnitionTargets.Shared.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is CrackerRuntime runtime && !runtime.isActiveAndEnabled)
                {
                    continue;
                }

                Vector3 d = all[i].FuseWorldPosition - point;
                d.y = 0f;
                float distance = d.magnitude;
                if (distance < best)
                {
                    best = distance;
                }
            }

            return best;
        }
    }
}
