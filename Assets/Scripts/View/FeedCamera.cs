using UnityEngine;

namespace Sussy
{
    /// Snaps the camera to whichever feed is active. Switching is instant and free —
    /// the cost of looking is what you stop seeing, not a delay.
    [RequireComponent(typeof(Camera))]
    public sealed class FeedCamera : MonoBehaviour
    {
        Camera _cam;
        CameraFeed _shown;

        void Awake() => _cam = GetComponent<Camera>();

        void LateUpdate()
        {
            var d = NightDirector.Instance;
            if (d == null || d.ActiveFeed == null || d.ActiveFeed == _shown) return;

            _shown = d.ActiveFeed;
            var r = _shown.View;

            transform.position = new Vector3(r.center.x, r.center.y, -10f);

            // Fit the feed rect regardless of window aspect.
            float halfH = r.height * 0.5f;
            float halfW = r.width * 0.5f / Mathf.Max(0.01f, _cam.aspect);
            _cam.orthographicSize = Mathf.Max(halfH, halfW);
        }
    }
}
