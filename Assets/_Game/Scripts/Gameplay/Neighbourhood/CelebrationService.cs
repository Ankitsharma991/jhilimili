using System;
using UnityEngine;
using UtsavNights.Core.Avatar;
using UtsavNights.Core.Home;
using UtsavNights.Core.Neighbourhood;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>The celebration moved to a spot (analytics in step 7: the spot's kind, never a position).</summary>
    public readonly struct SpotChosenEvent
    {
        public SpotChosenEvent(string spot, string kind, string environment)
        {
            Spot = spot;
            Kind = kind;
            Environment = environment;
        }

        public string Spot { get; }

        public string Kind { get; }

        public string Environment { get; }
    }

    /// <summary>
    /// Where the player celebrates (Phase 8 step 4, PRD 10, F20): the chosen spot moves the fireworks camera and the
    /// placement area there, with the spot's aerials rule, launch pads and a clear-sky check that sees the player's own
    /// strung lights. A spot the house no longer allows (a roof changed to tiles) falls back to the courtyard. Every
    /// session starts at home, in the courtyard, where the starting crackers stand.
    /// </summary>
    public sealed class CelebrationService
    {
        public const string Courtyard = "courtyard";

        private readonly SettlementView _settlement;
        private readonly HomeService _home;

        public CelebrationService(SettlementView settlement, HomeService home)
        {
            _settlement = settlement;
            _home = home;
            _settlement.Loaded += Reapply;
            if (_home != null)
            {
                _home.Changed += OnHouseChanged;
            }

            Choose(Courtyard, clearCrackers: false);
        }

        public event Action Changed;

        public SpotDef Current { get; private set; }

        public SettlementLayout Layout => _settlement.Layout;

        public SpotAvailability Availability(string spotId) =>
            CelebrationRules.Availability(_settlement.Layout, _settlement.Layout.Spot(spotId), _home?.Design);

        /// <summary>Moves the celebration to a spot. False (and nothing changes) when the spot is not open; see <see cref="Availability"/>.</summary>
        public bool Choose(string spotId, bool clearCrackers = true)
        {
            SpotDef spot = _settlement.Layout.Spot(spotId);
            if (spot == null || !Availability(spotId).Open)
            {
                return false;
            }

            bool moved = Current == null || Current.id != spot.id;
            Current = spot;
            Apply(spot);
            if (moved && clearCrackers && Game.Services.TryGet(out CrackerSpawner spawner))
            {
                // Crackers left at the old spot would be out of sight and out of reach.
                spawner.ClearAll();
            }

            if (moved)
            {
                Game.Events.Publish(new SpotChosenEvent(spot.id, spot.kind, _settlement.Layout.environment));
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>The spot whose play area contains a world point, or null (the walk's "Celebrate here").</summary>
        public SpotDef SpotAt(Vector3 world)
        {
            var p = new V3(world.x, world.y, world.z);
            foreach (SpotDef s in _settlement.Layout.spots)
            {
                if (CelebrationRules.InArea(s, p))
                {
                    return s;
                }
            }

            return null;
        }

        private void Apply(SpotDef spot)
        {
            CameraRig rig = UnityEngine.Object.FindFirstObjectByType<CameraRig>();
            if (rig != null)
            {
                rig.SetGroundShot(new Vector3(spot.camX, spot.camY, spot.camZ), Quaternion.Euler(spot.camPitch, spot.camYaw, 0f));
            }

            if (Game.Services.TryGet(out PlacementController placement))
            {
                placement.Validator.Spot = spot;
                placement.Validator.Bounded = spot.kind != SpotKinds.Courtyard;
                placement.Validator.StrandAbove = StrandAbove;
            }
        }

        private bool StrandAbove(Vector3 world)
        {
            HouseDesign design = _home?.Design;
            return design != null && CelebrationRules.StrandOverhead(design, new V3(world.x, world.y, world.z - HouseGeometry.WorldFrontZ));
        }

        private void Reapply()
        {
            // A new environment: the same spot ids, new places. Keep the spot if it is open there, else the courtyard.
            string id = Current?.id ?? Courtyard;
            Current = null;
            if (!Choose(id, clearCrackers: true))
            {
                Choose(Courtyard, clearCrackers: true);
            }
        }

        private void OnHouseChanged()
        {
            if (Current != null && !Availability(Current.id).Open)
            {
                Choose(Courtyard);
            }
        }
    }
}
