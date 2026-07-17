using UnityEngine;

namespace RoofingSimulator.World
{
    /// <summary>
    /// The material staging tarp (Etapa 7). Drop a load (G) while standing on the tarp and it
    /// snaps to the nearest free slot, stacking NEATLY on whatever is already there instead of
    /// tumbling — bundles, planks and rolls line up like a real acopio. Picking things back up
    /// works as always (E).
    /// </summary>
    public class StagingArea : MonoBehaviour
    {
        public static StagingArea Instance { get; private set; }

        private const float SlotSpacingX = 1.35f;
        private const float SlotSpacingZ = 1.35f;
        private const int SlotsX = 2, SlotsZ = 3;

        private Vector3[] slots;
        private Vector2 halfExtents;

        public static StagingArea Build(Vector3 center)
        {
            var root = new GameObject("StagingArea");
            root.transform.position = center;
            var area = root.AddComponent<StagingArea>();
            Instance = area;

            float w = SlotsX * SlotSpacingX + 0.5f;
            float d = SlotsZ * SlotSpacingZ + 0.5f;
            area.halfExtents = new Vector2(w * 0.5f, d * 0.5f);

            // The tarp itself plus a bright corner marker so the zone reads at a glance.
            Tarp(root.transform, new Vector3(0f, 0.012f, 0f), new Vector3(w, 0.02f, d),
                new Color(0.16f, 0.26f, 0.42f)); // work-tarp blue
            Color mark = new Color(0.92f, 0.78f, 0.18f);
            Tarp(root.transform, new Vector3(w * 0.5f - 0.1f, 0.03f, d * 0.5f - 0.1f), new Vector3(0.2f, 0.02f, 0.2f), mark);
            Tarp(root.transform, new Vector3(-w * 0.5f + 0.1f, 0.03f, d * 0.5f - 0.1f), new Vector3(0.2f, 0.02f, 0.2f), mark);
            Tarp(root.transform, new Vector3(w * 0.5f - 0.1f, 0.03f, -d * 0.5f + 0.1f), new Vector3(0.2f, 0.02f, 0.2f), mark);
            Tarp(root.transform, new Vector3(-w * 0.5f + 0.1f, 0.03f, -d * 0.5f + 0.1f), new Vector3(0.2f, 0.02f, 0.2f), mark);

            // Slot grid the drops snap to.
            area.slots = new Vector3[SlotsX * SlotsZ];
            int i = 0;
            for (int x = 0; x < SlotsX; x++)
                for (int z = 0; z < SlotsZ; z++)
                    area.slots[i++] = center + new Vector3(
                        (x - (SlotsX - 1) * 0.5f) * SlotSpacingX, 0f,
                        (z - (SlotsZ - 1) * 0.5f) * SlotSpacingZ);
            return area;
        }

        private static void Tarp(Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Tarp";
            var c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (r != null)
                r.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"))
                { color = color };
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// If <paramref name="item"/> was dropped over the tarp, snap it onto the nearest slot,
        /// resting on top of whatever is already stacked there. Returns true when snapped
        /// (the caller should then freeze its rigidbody).
        /// </summary>
        public static bool TrySnap(Transform item, Vector3 size)
        {
            StagingArea area = Instance;
            if (area == null || item == null) return false;

            Vector3 local = item.position - area.transform.position;
            if (Mathf.Abs(local.x) > area.halfExtents.x || Mathf.Abs(local.z) > area.halfExtents.y)
                return false;

            // Nearest slot to where it was dropped.
            Vector3 slot = area.slots[0];
            float best = float.MaxValue;
            foreach (Vector3 s in area.slots)
            {
                float d = (s - item.position).sqrMagnitude;
                if (d < best) { best = d; slot = s; }
            }

            // Rest on top of the stack already in the slot (ignore the item itself).
            float topY = area.transform.position.y + 0.03f;
            foreach (RaycastHit hit in Physics.RaycastAll(
                slot + Vector3.up * 4f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == item || hit.transform.IsChildOf(item)) continue;
                if (hit.point.y > topY) topY = hit.point.y;
            }

            item.SetPositionAndRotation(
                new Vector3(slot.x, topY + 0.01f, slot.z), Quaternion.identity);
            return true;
        }
    }
}
