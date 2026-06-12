using UnityEngine;

namespace RoofingSimulator.Gameplay
{
    /// <summary>
    /// Builds roofing-material blob GameObjects at runtime. Used when no authored
    /// prefab (Assets/Prefabs/Materials/RoofingMaterial.prefab) is assigned, so the
    /// gameplay loop is fully functional without the Unity Editor. An authored prefab
    /// can replace this for nicer art, but the component layout matches what the rest
    /// of the systems expect: MeshFilter + MeshRenderer + convex MeshCollider +
    /// RoofingMaterial + MaterialPhysics, tagged "RoofingMaterial".
    /// </summary>
    public static class MaterialBlobFactory
    {
        private static Mesh sharedBlobMesh;
        private static Material sharedBlobMaterial;

        /// <summary>
        /// Spawn a new material blob at <paramref name="worldPoint"/> with the given
        /// starting mass and material properties.
        /// </summary>
        public static RoofingMaterial Spawn(Vector3 worldPoint, float mass, MaterialProperties props = null)
        {
            props ??= new MaterialProperties();

            var go = new GameObject("RoofingMaterialBlob");
            go.transform.position = worldPoint;

            // Mesh first so MaterialPhysics.Awake (added below) sees a valid sharedMesh.
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = GetBlobMesh();

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetBlobMaterial();

            var collider = go.AddComponent<MeshCollider>();
            collider.convex = true;

            // Adding RoofingMaterial pulls in MaterialPhysics via RequireComponent.
            var material = go.AddComponent<RoofingMaterial>();
            material.totalMass = mass;
            material.elasticity = props.baseElasticity;
            material.adhesion = props.baseAdhesion;
            material.density = props.baseDensity;

            return material;
        }

        /// <summary>
        /// A flattened low-poly sphere used as the base putty shape. Built once from a
        /// primitive sphere's mesh and reused (MaterialPhysics clones its own copy).
        /// </summary>
        private static Mesh GetBlobMesh()
        {
            if (sharedBlobMesh != null)
            {
                return sharedBlobMesh;
            }

            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh source = temp.GetComponent<MeshFilter>().sharedMesh;

            sharedBlobMesh = Object.Instantiate(source);
            sharedBlobMesh.name = "PuttyBlob";

            // Flatten and shrink into a small dome (~25cm wide, ~8cm tall).
            Vector3[] verts = sharedBlobMesh.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] = new Vector3(verts[i].x * 0.25f, verts[i].y * 0.08f, verts[i].z * 0.25f);
            }
            sharedBlobMesh.vertices = verts;
            sharedBlobMesh.RecalculateNormals();
            sharedBlobMesh.RecalculateBounds();

            // Destroy the temporary primitive (immediate so it never renders a frame).
            Object.DestroyImmediate(temp);

            return sharedBlobMesh;
        }

        private static Material GetBlobMaterial()
        {
            if (sharedBlobMaterial != null)
            {
                return sharedBlobMaterial;
            }

            // Use whichever default shader the active render pipeline provides.
            Shader shader = Shader.Find("Standard")
                            ?? Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Sprites/Default");

            sharedBlobMaterial = new Material(shader) { name = "PuttyMaterial" };
            sharedBlobMaterial.color = new Color(0.55f, 0.35f, 0.2f); // clay brown
            return sharedBlobMaterial;
        }
    }
}
