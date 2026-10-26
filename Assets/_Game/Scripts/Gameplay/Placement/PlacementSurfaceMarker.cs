using UnityEngine;
using UtsavNights.Content;

namespace UtsavNights.Gameplay
{
    /// <summary>Marks a collider as somewhere crackers may stand (ground, rooftop, launch pad).</summary>
    public sealed class PlacementSurfaceMarker : MonoBehaviour
    {
        [SerializeField] private PlacementSurface kind = PlacementSurface.Ground;

        public PlacementSurface Kind => kind;

        public void Configure(PlacementSurface surface) => kind = surface;
    }
}
