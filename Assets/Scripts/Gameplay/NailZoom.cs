using UnityEngine;
using RoofingSimulator.Input;

namespace RoofingSimulator.Gameplay
{
    /// <summary>
    /// Nail-gun precision zoom (Etapa 8). With the nail gun out, HOLD right-click: the camera
    /// eases in (narrow FOV, slower mouse, gun raised to centre — see
    /// <see cref="CameraController.SetAimZoom"/>), a focus frame appears, and the HUD shows the
    /// LIVE precision score at the crosshair so you can centre the nail before firing. Release
    /// to snap back. Not a separate scene — just a temporary close-up.
    /// </summary>
    public class NailZoom : MonoBehaviour
    {
        private CameraController camCtrl;
        private RoofingToolbelt belt;
        private PlayerInput input;
        private RoofGrid grid;
        private bool zooming;

        public void Bind(Camera cam, RoofingToolbelt toolbelt, PlayerInput playerInput, RoofGrid roofGrid)
        {
            camCtrl = cam != null ? cam.GetComponent<CameraController>() : null;
            belt = toolbelt;
            input = playerInput;
            grid = roofGrid;
        }

        private void Update()
        {
            bool want = belt != null && belt.Current == RoofingTool.NailGun
                && UnityEngine.Input.GetMouseButton(1);
            if (want == zooming) return;
            zooming = want;
            camCtrl?.SetAimZoom(zooming);
        }

        private void OnDisable()
        {
            if (zooming) camCtrl?.SetAimZoom(false);
            zooming = false;
        }

        private void OnGUI()
        {
            float blend = camCtrl != null ? camCtrl.ZoomBlend : (zooming ? 1f : 0f);
            if (blend < 0.05f) return;

            // Focus frame: four corner brackets closing in around the crosshair.
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            float r = Mathf.Lerp(180f, 78f, blend);
            const float len = 22f, th = 3f;
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.85f * blend);
            foreach (Vector2 s in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            {
                GUI.DrawTexture(new Rect(cx + s.x * r - (s.x > 0 ? len : 0), cy + s.y * r, len, th), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + s.x * r, cy + s.y * r - (s.y > 0 ? len : 0), th, len), Texture2D.whiteTexture);
            }
            GUI.color = prev;

            // Live precision readout for the shingle under the crosshair.
            var style = new GUIStyle(GUI.skin.box) { fontSize = 15, fontStyle = FontStyle.Bold };
            string txt = "Apunta a una teja colocada (sin clavar)";
            if (input != null && input.HasValidTarget && grid != null)
            {
                RoofCell cell = input.CurrentHit.collider != null
                    ? input.CurrentHit.collider.GetComponentInParent<RoofCell>()
                    : null;
                float q = (RoofGrid.GridOf(cell) ?? grid).PreviewNailQuality(cell, input.CurrentHit.point);
                if (q >= 0f) txt = $"Precisión: {q:P0} — clava en el centro de la teja";
            }
            GUI.Box(new Rect(cx - 190f, cy + r + 16f, 380f, 30f), txt, style);
        }
    }
}
