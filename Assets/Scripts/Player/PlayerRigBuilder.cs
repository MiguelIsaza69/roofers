using UnityEngine;
using RoofingSimulator.Input;
using RoofingSimulator.Gameplay;

namespace RoofingSimulator.Player
{
    /// <summary>References to the freshly built player so the scene can wire gameplay to it.</summary>
    public struct PlayerRig
    {
        public GameObject root;
        public Camera camera;
        public PlayerInput input;
        public RoofingToolbelt toolbelt;
        public PlayerLocomotion locomotion;
        public PlayerMaterials materials;
    }

    /// <summary>
    /// Assembles a complete first-person player from code so the gameplay scene is fully
    /// playable without hand-authoring a prefab. Hierarchy:
    ///
    ///   Player (CharacterController + locomotion/stamina/harness/ladder/ledge + body)
    ///   └── CameraPivot (head-bob)
    ///       └── PlayerCamera (Camera + CameraController + PlayerInput)
    ///           └── ToolAnchor (RoofingToolbelt + held-tool placeholder)
    /// </summary>
    public static class PlayerRigBuilder
    {
        public static PlayerRig Build(Vector3 position, Quaternion rotation)
        {
            var root = new GameObject("Player");
            root.transform.SetPositionAndRotation(position, rotation);

            var cc = root.AddComponent<CharacterController>();
            cc.radius = 0.35f;
            cc.height = 1.78f;                          // real ~1.75 m person
            cc.center = new Vector3(0f, 0.89f, 0f);
            cc.slopeLimit = 55f;
            cc.stepOffset = 0.35f;

            root.AddComponent<PlayerStamina>();

            // Camera rig.
            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.65f, 0f); // eye height of a ~1.75 m person

            var camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(pivot.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, 0.2f); // forward so looking down doesn't show your own body
            camGo.tag = "MainCamera";
            var camera = camGo.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            // The player's camera is the audio listener (the scene controller disables legacy ones).
            camGo.AddComponent<AudioListener>();

            // Held tools render through a second depth-only camera so they never clip walls
            // (standard FPS view-model setup). The main camera stops drawing that layer.
            camera.cullingMask &= ~(1 << HeldToolView.ViewModelLayer);
            var vmGo = new GameObject("ViewModelCamera");
            vmGo.transform.SetParent(camGo.transform, false);
            var vmCam = vmGo.AddComponent<Camera>();
            vmCam.clearFlags = CameraClearFlags.Depth;       // draw on top of the world
            vmCam.cullingMask = 1 << HeldToolView.ViewModelLayer;
            vmCam.depth = camera.depth + 1;
            vmCam.nearClipPlane = 0.02f;
            vmCam.farClipPlane = 5f;
            vmCam.fieldOfView = camera.fieldOfView;

            var toolAnchor = new GameObject("ToolAnchor");
            toolAnchor.transform.SetParent(camGo.transform, false);
            toolAnchor.transform.localPosition = new Vector3(0.3f, -0.3f, 0.6f);
            var toolbelt = toolAnchor.AddComponent<RoofingToolbelt>();
            toolAnchor.AddComponent<HeldToolView>();

            var camCtrl = camGo.AddComponent<CameraController>();
            var input = camGo.AddComponent<PlayerInput>();
            input.SetAimCamera(camera);

            // Player systems on the body (added after the camera so their Awake auto-find works).
            root.AddComponent<SafetyHarness>();
            var locomotion = root.AddComponent<PlayerLocomotion>();
            root.AddComponent<LadderClimber>();
            root.AddComponent<LedgeGrabAndFall>();
            var materials = root.AddComponent<PlayerMaterials>();
            root.AddComponent<PlayerInteractor>();

            CreateBodyVisual(root.transform);

            camCtrl.Configure(root.transform, toolAnchor.transform);

            return new PlayerRig
            {
                root = root,
                camera = camera,
                input = input,
                toolbelt = toolbelt,
                locomotion = locomotion,
                materials = materials
            };
        }

        private static void CreateBodyVisual(Transform parent)
        {
            // Kept below eye height so it doesn't fill the view, visible when looking down.
            var vis = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            vis.name = "BodyVisual";
            vis.transform.SetParent(parent, false);
            vis.transform.localPosition = new Vector3(0f, 0.7f, -0.12f);
            vis.transform.localScale = new Vector3(0.6f, 0.7f, 0.6f);
            var col = vis.GetComponent<Collider>();
            if (col != null) Object.Destroy(col); // the CharacterController is the collider
            var r = vis.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"))
            { color = new Color(0.2f, 0.45f, 0.8f) };
        }
    }
}
