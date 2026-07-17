using UnityEngine;

namespace RoofingSimulator.World
{
    /// <summary>Keeps a label facing the player camera so the tool-rack names stay readable.</summary>
    public class Billboard : MonoBehaviour
    {
        private Camera cam;

        private void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            // Match the camera's orientation — the canonical non-mirrored TextMesh billboard.
            transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
        }
    }
}
