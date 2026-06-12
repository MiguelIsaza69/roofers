# Unity Editor Wiring Guide

**Project**: Roofing Simulator with Putty Physics
**Purpose**: Turn the code-complete project into a runnable build by authoring the
scenes, prefabs, and component wiring that scripts cannot create from text.

> **How to read this**: each section lists a GameObject **hierarchy** to build, then a
> **wiring table** mapping each serialized field (left) to the object you drag onto it
> (right). Field names match the `[SerializeField]` names in the scripts exactly.

> **Good news**: the single-player gameplay scene is almost self-building —
> `JobSceneController` creates a procedural roof, camera, input, and tool if you leave
> its fields empty. You can smoke-test gameplay with **one** GameObject. The menu/career
> scenes need real uGUI, and multiplayer needs prefab + scene authoring.

---

## 0. Prerequisites

1. **Unity**: 2022 LTS or newer (project assumes .NET Standard 2.1).
2. **Packages** (see `DEPENDENCIES_SETUP.md` for detail):
   - **Newtonsoft Json.NET** — required by the core game (save/load). Without it,
     `RoofingSimulator.asmdef` will not compile.
   - **Mirror** — required only by `RoofingSimulator.Multiplayer.asmdef`. If absent, the
     single-player game still compiles (the multiplayer assembly is isolated).
3. **First compile check**: open the project and confirm the Console is clean *before*
   wiring anything. If `UnityEngine.UI` fails to resolve in `RoofingSimulator.asmdef`,
   open the asmdef inspector and re-add the **UnityEngine.UI** assembly reference.

---

## 1. Project-Wide Setup

### 1.1 Tags & Layers
Already defined in `ProjectSettings/TagManager.asset`: tags **RoofingMaterial** and
**RoofSurface**, plus matching layers. Confirm under **Edit ▸ Project Settings ▸ Tags
and Layers** — the code assigns `gameObject.tag = "RoofingMaterial"` and will throw if
the tag is missing.

### 1.2 Build Settings scene order
**File ▸ Build Settings ▸ Add Open Scenes**, in this exact order (index matters — the
managers load scenes by name, but index 0 is the startup scene):

| Index | Scene |
|-------|-------|
| 0 | `Assets/Scenes/MainMenu.unity` |
| 1 | `Assets/Scenes/Career.unity` |
| 2 | `Assets/Scenes/RoofingJob.unity` |
| 3 | `Assets/Scenes/Multiplayer.unity` |

> Scene names are referenced as string constants in `GameManager`
> (`MainMenuScene`/`CareerScene`/`JobScene`) and must match the file names.

### 1.3 Input axes
`CameraController` reads the legacy `"Mouse X"`/`"Mouse Y"` axes (present by default).
`PlayerInput` reads mouse button 0 and an optional `"Fire1"` button (default present).
If you use the new Input System package exclusively, enable **Both** under
**Project Settings ▸ Player ▸ Active Input Handling**.

### 1.4 EventSystem
Every scene that has a `Canvas` needs an **EventSystem** (right-click Hierarchy ▸
UI ▸ Event System). Adding any UI element usually creates one automatically.

---

## 2. Scene: MainMenu

### Hierarchy
```
MainMenu (scene)
├── _Bootstrap                 (empty GameObject)
│   ├── GameManager            (GameManager.cs)
│   └── MultiplayerManager     (MultiplayerManager.cs)   ← only if using multiplayer
├── EventSystem
└── Canvas  (Screen Space - Overlay)
    └── MainMenuPanel
        ├── PlayerNameInput     (UI ▸ Input Field)
        ├── NewCareerButton     (UI ▸ Button, child Text "New Career")
        ├── LoadCareerButton    (UI ▸ Button, child Text "Load Career")
        ├── MessageLabel        (UI ▸ Text)
        └── SavedCareerList      (empty RectTransform, add Vertical Layout Group)
```

> `GameManager.Awake` creates `CareerManager` and `SaveManager` automatically and marks
> them `DontDestroyOnLoad`, so `_Bootstrap` only needs `GameManager` (+ `MultiplayerManager`).

### Component: add `MainMenuUI` to `MainMenuPanel`

| Field | Drag in |
|-------|---------|
| `playerNameInput` | `PlayerNameInput` |
| `newCareerButton` | `NewCareerButton` |
| `loadCareerButton` | `LoadCareerButton` |
| `savedCareerListContainer` | `SavedCareerList` (Transform) |
| `savedCareerButtonPrefab` | a **Button prefab** with a child `Text` (see below) |
| `messageLabel` | `MessageLabel` |

### Saved-career button prefab
Create a simple **Button** with a child **Text**, drag it into `Assets/Prefabs/UI/`, then
delete it from the scene. Assign that prefab to `savedCareerButtonPrefab`. The controller
instantiates one per saved career and sets the label + click handler in code.

---

## 3. Scene: Career

### Hierarchy
```
Career (scene)
├── EventSystem
└── Canvas
    ├── CareerPanel
    │   ├── PlayerNameLabel       (UI ▸ Text)
    │   ├── JobsCompletedLabel    (UI ▸ Text)
    │   ├── BestTimeLabel         (UI ▸ Text)
    │   ├── BackToMenuButton      (UI ▸ Button)
    │   └── JobList               (RectTransform + Vertical Layout Group + Content Size Fitter)
    ├── JobBriefingPanel          (Panel, starts active — controller hides it in Awake)
    │   ├── TitleLabel            (Text)
    │   ├── DescriptionLabel      (Text)
    │   ├── CoverageLabel         (Text)
    │   ├── QualityLabel          (Text)
    │   ├── MaterialLabel         (Text)
    │   ├── TimeLabel             (Text)
    │   ├── StartButton           (Button)
    │   └── BackButton            (Button)
    └── CompletionPanel           (Panel, starts active — controller hides it in Awake)
        ├── HeaderLabel           (Text)
        ├── TimeLabel             (Text)
        ├── CoverageLabel         (Text)
        ├── QualityLabel          (Text)
        ├── MaterialLabel         (Text)
        └── ContinueButton        (Button)
```

### Component: `CareerUI` on `CareerPanel`

| Field | Drag in |
|-------|---------|
| `jobListContainer` | `JobList` (Transform) |
| `jobButtonPrefab` | a Button prefab w/ child Text (reuse the UI button prefab) |
| `playerNameLabel` | `PlayerNameLabel` |
| `jobsCompletedLabel` | `JobsCompletedLabel` |
| `bestTimeLabel` | `BestTimeLabel` |
| `backToMenuButton` | `BackToMenuButton` |
| `jobBriefingPanel` | `JobBriefingPanel` (the `JobBriefingUI` component) |

### Component: `JobBriefingUI` on `JobBriefingPanel`

| Field | Drag in |
|-------|---------|
| `panelRoot` | `JobBriefingPanel` (or leave empty → uses self) |
| `titleLabel`…`timeLabel` | the matching Text children |
| `startButton` | `StartButton` |
| `backButton` | `BackButton` |

### Component: `CompletionScreenUI` on `CompletionPanel`

| Field | Drag in |
|-------|---------|
| `panelRoot` | `CompletionPanel` (or leave empty → uses self) |
| `headerLabel`…`materialLabel` | matching Text children |
| `continueButton` | `ContinueButton` |
| `careerUI` | `CareerPanel` (the `CareerUI` component) |

> The completion panel shows itself automatically on `Start` if
> `GameManager.LastCompletion` is set (i.e., you just finished a job).

---

## 4. Scene: RoofingJob (single-player gameplay)

### Minimum viable (smoke test)
Create an empty GameObject **JobScene** and add `JobSceneController`. Press Play *from
the Career scene flow* (so a job is selected). The controller will procedurally build a
roof sized to the job, a camera, `PlayerInput`, and `RoofingMaterialTool`. You can paint
immediately. Controls: **Left Mouse** apply, **Return** finish, **Esc** abandon.

> To Play this scene directly for testing without going through the menus, the
> controller falls back to job index −1 → guard. Instead enter via the Career scene, or
> temporarily hardcode `SelectedJobIndex` (see Known Gaps §7).

### Authored version (recommended)
```
RoofingJob (scene)
├── JobSceneController          (JobSceneController.cs)
├── RoofingJobInstance          (RoofingJobInstance.cs)
├── Roof                        (your authored mesh; tag = RoofSurface)
│   ├── MeshFilter / MeshRenderer
│   ├── MeshCollider
│   └── RoofSurface             (RoofSurface.cs)
├── Player
│   ├── Camera (tag MainCamera) + PlayerInput + CameraController
│   └── ToolAnchor (empty) + RoofingMaterialTool
├── EventSystem
└── HUDCanvas
    └── HUDPanel                (HUD.cs)
        ├── CoverageLabel        (Text)
        ├── CoverageBar          (Image, Image Type = Filled, Fill Method = Horizontal)
        │   └── TargetMarker     (Image, thin vertical line)
        ├── MaterialLabel        (Text)
        ├── TimeLabel            (Text)
        └── QualityLabel         (Text)
```

### Component wiring

**`JobSceneController`** (any field left empty is auto-created):

| Field | Drag in |
|-------|---------|
| `jobInstance` | `RoofingJobInstance` |
| `roofSurface` | `Roof` (the `RoofSurface` component) |
| `playerCamera` | `Player/Camera` |
| `playerInput` | `Player/Camera` (the `PlayerInput`) |
| `materialTool` | `Player/ToolAnchor` (the `RoofingMaterialTool`) |
| `hud` | `HUDPanel` (the `HUD`) |

**`RoofSurface`** on `Roof`: set `id`, `totalArea` (will be overwritten from the job),
ensure the `MeshCollider` is assigned. Put `Roof` on the **RoofSurface** layer.

**`PlayerInput`** on the camera:

| Field | Value |
|-------|-------|
| `aimCamera` | the camera (or leave empty → `Camera.main`) |
| `maxApplyDistance` | 8 |
| `applyMask` | include the **RoofSurface** and **RoofingMaterial** layers |

**`RoofingMaterialTool`** on `ToolAnchor`:

| Field | Value |
|-------|-------|
| `playerInput` | `Player/Camera` PlayerInput (or leave empty → searches parent) |
| `jobInstance` | `RoofingJobInstance` (or leave empty → `JobSceneController.Bind` sets it) |
| `massPerTick` / `applyInterval` / `mergeRadius` | 2 / 0.08 / 0.3 |

**`HUD`** on `HUDPanel`:

| Field | Drag in |
|-------|---------|
| `coverageLabel` | `CoverageLabel` |
| `coverageBarFill` | `CoverageBar` (the **Filled** Image) |
| `coverageTargetMarker` | `TargetMarker` |
| `materialLabel` / `timeLabel` / `qualityLabel` | matching Text |

> The HUD's `coverageTargetMarker` is repositioned in `Bind()` to the job's required
> coverage %, so anchor it to the left edge of the bar (anchorMin/Max X = 0).

---

## 5. Material Prefab (optional)

The tool spawns blobs via `MaterialBlobFactory` (procedural flattened sphere) when no
authored prefab exists, so this is **optional polish**. To use art instead:
1. Build a small dome mesh GameObject with `MeshFilter` + `MeshRenderer` +
   convex `MeshCollider` + `RoofingMaterial` (which auto-adds `MaterialPhysics`).
2. Tag it **RoofingMaterial**. Save to `Assets/Prefabs/Materials/RoofingMaterial.prefab`.
3. (Future) add a serialized prefab field to the tool/factory to use it instead of the
   procedural mesh — not currently exposed, so for now the procedural path is the path.

---

## 6. Scene: Multiplayer + Networking

> Requires Mirror installed. This is the most involved setup.

### 6.1 NetworkManager object
```
Multiplayer (scene)
├── NetworkManager
│   ├── RoofingNetworkManager   (NetworkSetup.cs)
│   ├── KcpTransport            (or any Mirror Transport component)
│   └── NetworkManagerHUD       (optional, Mirror's debug HUD)
```
On **RoofingNetworkManager**:

| Field | Value |
|-------|-------|
| `Transport` | the `KcpTransport` on the same object |
| `Player Prefab` | the **PlayerAvatar** prefab (§6.2) |
| `Auto Create Player` | **on** |
| `spawnPoints` (our field) | drag 2–4 empty spawn-point Transforms |
| `Network Address` | `localhost` for same-machine testing |

> `maxConnections` is forced to 4 in `Awake`. Add spawn-point empties around the roof and
> assign them to the `spawnPoints` array.

### 6.2 PlayerAvatar prefab (T054)
```
PlayerAvatar (prefab)
├── NetworkIdentity
├── PlayerAvatarSync            (PlayerAvatarSync.cs)
├── Body (capsule mesh)         (visible to everyone)
├── NameLabel (3D Text/TextMesh)
└── LocalRig                    (empty; the "local-only" container)
    ├── Camera                  (+ PlayerInput + CameraController)
    └── (no RoofingMaterialTool here — applies route through the network)
```
On **PlayerAvatarSync**:

| Field | Drag in |
|-------|---------|
| `localOnlyObjects` | array → `LocalRig` (and/or the Camera) — disabled on remote copies |
| `nameLabel` | `NameLabel` (TextMesh) |
| `sendInterval` / `interpolationSpeed` | 0.05 / 12 |
| `playerInput` | `LocalRig/Camera` PlayerInput (or leave empty → searches children) |

> **Register the prefab**: the PlayerAvatar must be the NetworkManager's `playerPrefab`.
> It does **not** need to be in the spawnable-prefabs list separately (the player prefab
> is registered automatically).
> Material blobs are **not** networked objects — they are reproduced deterministically on
> each client from `RpcApply`, so do **not** add a NetworkIdentity to blobs.

### 6.3 Shared material field
```
Multiplayer (scene)
└── SharedMaterialField
    ├── NetworkIdentity         (scene object — Mirror spawns it on host start)
    └── NetworkRoofingMaterial  (NetworkRoofingMaterial.cs)
```
On **NetworkRoofingMaterial**:

| Field | Value |
|-------|-------|
| `jobInstance` | the shared `RoofingJobInstance` in this scene |
| `hud` | (optional) the in-scene `HUD` to bind when the job starts |
| `massPerApply` / `mergeRadius` / `quantizeStep` | 2 / 0.3 / 0.01 |
| `coverageSyncInterval` | 1 |

> **`NetworkRoofingMaterial` owns the job lifecycle in multiplayer** — it picks the job on
> the server (host's selection → career current job → job 0), syncs it via the
> `activeJobIndex` SyncVar, and every client loads and `StartJob()`s the **same** config.
> So the multiplayer scene needs a `RoofingJobInstance` + `RoofSurface` (as in §4) wired to
> the `jobInstance` field, but **does NOT use `JobSceneController`** (that's the
> single-player driver — adding it here would double-initialize the job).

### 6.4 Multiplayer UI
Build a Canvas mirroring `MultiplayerUI`'s fields:

| Field | Element |
|-------|---------|
| `playerNameInput`, `joinCodeInput` | Input Fields |
| `hostButton`, `joinButton`, `leaveButton` | Buttons |
| `statusPanel`, `disconnectPanel` | Panels (start inactive; controller toggles) |
| `sessionCodeLabel`, `playerCountLabel`, `disconnectMessage`, `errorLabel` | Texts |
| `continueButton`, `abandonButton` | Buttons |

---

## 7. Known Gaps & Honest Notes

These are real limitations to plan around — not yet handled in code:

1. **Multiplayer co-op completion isn't shared.** Job *selection* (`activeJobIndex`
   SyncVar), coverage, and the material-budget HUD value (`syncedMaterialRemaining` →
   `HUD.SetMaterialRemaining`) are all synced now. What remains is the **job outcome**:
   each peer evaluates completion/failure on its own `RoofingJobInstance`, and there's no
   co-op completion→career-record flow (whose career advances is undefined per the spec).
   So a client can reach the win state slightly before/after the host, and progression
   isn't recorded for a co-op session. Broadcasting an authoritative outcome and deciding
   co-op progression are the remaining MP items.
2. **Session codes are direct addresses.** `JoinSession(code)` treats the code as an
   IP/hostname. LAN/same-machine works; internet play needs a relay/matchmaker
   (`ResolveAddress` is the seam to add it).
3. **Procedural camera is overhead-angled, not true first-person on the roof.** The
   single-player harness places the camera above/behind so you can paint. A real
   first-person walking controller is a refinement.
4. **No authored roof meshes.** `roofGeometryId` flows through the job configs but isn't
   used to load a mesh yet; the procedural quad stands in. Add a lookup from
   `roofGeometryId` → prefab/mesh when art exists.
5. **TimeSpan JSON format** in saves is Newtonsoft's `"HH:MM:SS"`, not the ISO-8601
   `PT…` form in `career-save-schema.json`. Round-trips fine; only matters if an external
   tool consumes the save.

---

## 8. Smoke-Test Sequence

Once wired, validate in this order (maps to `quickstart.md` scenarios):

1. **Boot** → MainMenu loads, no Console errors.
2. **New career** → enter a name, click New Career → Career scene shows 15 jobs, job 1
   Available, rest Locked. (Confirms save created under
   `%userprofile%/AppData/LocalLow/<company>/<product>/saves/`.)
3. **Briefing** → click job 1 → briefing shows 85% / Standard / 600 kg / no limit.
4. **Play** → Start → paint the roof; coverage % climbs, HUD bar fills, putty deforms
   and settles without floating.
5. **Finish** → reach 85% → press Return → Career scene shows the completion panel; job 2
   unlocks.
6. **Persistence** → quit Play, re-enter, Load Career → progress restored.
7. **Multiplayer** (if Mirror wired) → one instance Host, another Join `localhost` → both
   see each other's avatars; painting by one appears on the other within ~200 ms.

If step 4 shows material falling through or floating, check the `Roof` is on the
**RoofSurface** layer and `MaterialPhysics`'s `roofMask` includes it.

---

*Generated to accompany the implemented scripts. Field names are authoritative as of the
current `Assets/Scripts/` source; if you rename a `[SerializeField]`, update this guide.*
