using System;
using UnityEngine;
using UtsavNights.Core.Home;
using UtsavNights.Core.Runtime;

namespace UtsavNights.Gameplay
{
    /// <summary>Decoration mode opened (analytics in Phase 7 step 7).</summary>
    public readonly struct DecorModeOpenedEvent
    {
        public DecorModeOpenedEvent(int decorations) => Decorations = decorations;

        public int Decorations { get; }
    }

    /// <summary>Decoration mode closed: how many edits, and whether the house was saved.</summary>
    public readonly struct DecorModeClosedEvent
    {
        public DecorModeClosedEvent(int edits, bool saved, int decorations)
        {
            Edits = edits;
            Saved = saved;
            Decorations = decorations;
        }

        public int Edits { get; }

        public bool Saved { get; }

        public int Decorations { get; }
    }

    /// <summary>A decoration was placed (catalogue id and category only).</summary>
    public readonly struct DecorationPlacedEvent
    {
        public DecorationPlacedEvent(string item, DecorCategory category)
        {
            Item = item;
            Category = category;
        }

        public string Item { get; }

        public DecorCategory Category { get; }
    }

    /// <summary>A facade module or the paint changed (catalogue ids only).</summary>
    public readonly struct HouseModuleChangedEvent
    {
        public HouseModuleChangedEvent(string point, string module)
        {
            Point = point;
            Module = module;
        }

        public string Point { get; }

        public string Module { get; }
    }

    /// <summary>A layout slot was saved or loaded (the slot number only, never its name).</summary>
    public readonly struct HouseLayoutEvent
    {
        public HouseLayoutEvent(int slot, bool saved)
        {
            Slot = slot;
            Saved = saved;
        }

        public int Slot { get; }

        public bool Saved { get; }
    }

    /// <summary>
    /// The player's house for this session (Phase 7 step 5): house.json and its journal, the editor (an interrupted
    /// session is replayed when the scene starts), the house and decoration views, placement checks for the current
    /// quality tier, and colliders for the house's solid parts so crackers cannot be placed into walls.
    /// </summary>
    public sealed class HomeService
    {
        private readonly Func<int> _tier;
        private readonly Transform _colliders;
        private PlacementContext _context;
        private string _contextKey;

        public HomeService(HouseStore store, HouseView house, DecorView decor, Func<int> tier, Transform colliders = null)
        {
            Store = store;
            House = house;
            Decor = decor;
            _tier = tier ?? (() => 1);
            _colliders = colliders;
            Editor = store.Open();
            Recovered = store.Recovered;
            Editor.Changed += Refresh;
            Refresh();
        }

        public event Action Changed;

        public HouseStore Store { get; }

        public HouseEditor Editor { get; }

        public HouseView House { get; }

        public DecorView Decor { get; }

        public HouseDesign Design => Editor.Design;

        /// <summary>A friend's house shown during a visit (Phase 10 step 4), or null. The player's own design is untouched.</summary>
        public HouseDesign Visit { get; private set; }

        public bool Visiting => Visit != null;

        /// <summary>What the scene shows: the visited house, else the player's own.</summary>
        public HouseDesign Shown => Visit ?? Design;

        /// <summary>Journal lines replayed at start: above zero means an interrupted edit was restored.</summary>
        public int Recovered { get; }

        public int Tier => _tier();

        /// <summary>Placement rules for the design as it is now (rebuilt when modules or the tier change).</summary>
        public PlacementContext Context
        {
            get
            {
                string key = Tier + "|" + ModulesKey(Design);
                if (_context == null || key != _contextKey || !ReferenceEquals(_context.Design, Design))
                {
                    _context = DecorPlacement.For(Design, Tier);
                    _contextKey = key;
                }

                return _context;
            }
        }

        public bool Do(IHouseCommand command)
        {
            if (Visiting || !Editor.Do(command))
            {
                return false;
            }

            if (command is PlaceDecor place)
            {
                PlacedDecor placed = Design.Find(place.Uid);
                DecorInfo info = HomeCatalog.Decoration(placed?.item);
                if (info != null)
                {
                    Game.Events.Publish(new DecorationPlacedEvent(info.Id, info.Category));
                }
            }

            return true;
        }

        public bool Save() => Store.Save(Editor);

        /// <summary>Shows a friend's house in place of the player's own, view only, until <see cref="EndVisit"/>.</summary>
        public void ShowVisit(HouseDesign design)
        {
            Visit = design;
            Refresh();
        }

        public void EndVisit()
        {
            if (Visit != null)
            {
                Visit = null;
                Refresh();
            }
        }

        public void Refresh()
        {
            if (House != null)
            {
                House.Show(Shown);
            }

            if (Decor != null)
            {
                Decor.Show(Shown.decorations);
            }

            RebuildColliders();
            Changed?.Invoke();
        }

        private string _collidersKey;

        private void RebuildColliders()
        {
            if (_colliders == null)
            {
                return;
            }

            string key = ModulesKey(Shown);
            if (key == _collidersKey)
            {
                return;
            }

            _collidersKey = key;
            for (int i = _colliders.childCount - 1; i >= 0; i--)
            {
                HouseMeshes.Destroy(_colliders.GetChild(i).gameObject);
            }

            foreach (Box3 b in DecorPlacement.For(Shown, Tier).Solids)
            {
                Collider("Solid", b, Content.PlacementSurface.None);
            }

            // Phase 8: the roof deck and the balcony floor are celebration spots, ground for crackers (2 cm proud of the
            // solids below, so a placement ray finds the marked surface first).
            System.Collections.Generic.HashSet<string> tags = Core.Neighbourhood.WalkNavigator.HomeTags(Shown);
            if (tags.Contains(Core.Neighbourhood.SpotTags.HasRooftop))
            {
                const float w = HouseGeometry.HalfWidth - 0.15f;
                Collider("RoofDeck", new Box3(-w, HouseGeometry.Eaves, -HouseGeometry.Depth + 0.15f, w, HouseGeometry.Eaves + 0.02f, -0.15f),
                    Content.PlacementSurface.Ground | Content.PlacementSurface.Rooftop);
            }

            if (tags.Contains(Core.Neighbourhood.SpotTags.HasBalcony))
            {
                Collider("BalconyFloor", new Box3(-0.95f, HouseGeometry.UpperFloor, 0f, 0.95f, HouseGeometry.UpperFloor + 0.02f, 0.75f), Content.PlacementSurface.Ground);
            }
        }

        private void Collider(string name, Box3 b, Content.PlacementSurface surface)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_colliders, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3((b.Min.X + b.Max.X) / 2f, (b.Min.Y + b.Max.Y) / 2f, (b.Min.Z + b.Max.Z) / 2f);
            box.size = new Vector3(b.Max.X - b.Min.X, b.Max.Y - b.Min.Y, b.Max.Z - b.Min.Z);
            if (surface != Content.PlacementSurface.None)
            {
                go.AddComponent<PlacementSurfaceMarker>().Configure(surface);
            }
        }

        private static string ModulesKey(HouseDesign d)
        {
            var sb = new System.Text.StringBuilder();
            foreach (ModuleChoice m in d.modules)
            {
                sb.Append(m.module).Append(',');
            }

            return sb.ToString();
        }
    }
}
