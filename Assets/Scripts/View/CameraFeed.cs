using UnityEngine;

namespace Sussy
{
    /// One CCTV feed covering a rectangle of the level. The player sees exactly one at a time;
    /// everything off-feed is still fully simulated.
    public sealed class CameraFeed : MonoBehaviour
    {
        public string FeedName = "CAM";
        public int    RoomId = -1;
        public Rect   View = new Rect(0, 0, 10, 8);

        public bool Contains(Vector3 worldPos) => View.Contains(worldPos);

        public Vector3 Center => new Vector3(View.center.x, View.center.y, -10f);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(new Vector3(View.center.x, View.center.y, 0f),
                                new Vector3(View.width, View.height, 0.1f));
        }
    }
}
