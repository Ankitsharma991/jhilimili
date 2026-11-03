using UnityEngine;
using UtsavNights.Core.Home;

namespace UtsavNights.Gameplay
{
    /// <summary>Tags a room collider with what it is for placement: the floor, a wall (with its inward normal) or a furniture top.</summary>
    public sealed class RoomSurfaceMarker : MonoBehaviour
    {
        public Surface Surface { get; private set; }

        public Vector3 Normal { get; private set; }

        /// <summary>For furniture: the height of its top in room space.</summary>
        public float Top { get; private set; }

        public void Configure(Surface surface, Vector3 normal, float top = 0f)
        {
            Surface = surface;
            Normal = normal;
            Top = top;
        }
    }
}
