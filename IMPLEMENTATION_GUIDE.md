# Roofing Simulator Developer Guide

Durable reference for working in this codebase: prerequisites, structure, conventions,
implementation patterns, testing, performance, and troubleshooting.

> For **current progress** see `STATUS.md`. For the **task breakdown** see
> `specs/001-roofing-simulator/tasks.md`. For **Editor wiring** see `EDITOR_SETUP_GUIDE.md`.

---

## Prerequisites

- Unity 2022 LTS or later (.NET Standard 2.1)
- Mirror networking library (multiplayer assembly only)
- Newtonsoft Json.NET (save/load)
- C# development environment

## Project structure

Game code is organized under `Assets/Scripts/` by functional domain:

```
Assets/Scripts/
├── Core/          # GameManager, CareerManager, JobSceneController, Career
├── Gameplay/      # RoofingJob(+Instance), RoofingMaterial, MaterialPhysics, RoofSurface,
│                  #   RoofingMaterialTool, JobCatalog, JobConfiguration(+Loader), MaterialBlobFactory
├── Input/         # PlayerInput, CameraController
├── UI/            # MainMenuUI, CareerUI, JobBriefingUI, CompletionScreenUI, HUD
├── Multiplayer/   # RoofingNetworkManager, MultiplayerManager, PlayerAvatarSync,
│                  #   NetworkRoofingMaterial, MultiplayerUI   (separate asmdef → Mirror)
├── Persistence/   # SaveManager (JSON serialization of Career)
└── Utils/         # CoverageCalculator
```

Job definitions live as JSON in `Assets/Resources/Jobs/` (`JobConfigurationLoader` reads
them; `JobCatalog` falls back to a built-in progression if none are present).

---

## Key technical decisions

See `specs/001-roofing-simulator/research.md` for full rationale:

1. **Networking**: Mirror for real-time multiplayer (isolated assembly)
2. **Physics**: Unity PhysX + a custom per-vertex deformation layer (`MaterialPhysics`)
3. **Persistence**: local JSON files with backup-on-save
4. **Coverage**: raycast-based grid sampling
5. **Difficulty**: data-driven JSON job configuration

---

## Conventions

### Naming
- **Classes / methods**: PascalCase (`CareerManager`, `UpdateCoverage()`)
- **Fields**: camelCase (`totalMass`, `elasticity`)
- **Constants**: UPPER_CASE

### Script layout
```csharp
using statements
namespace RoofingSimulator.<Domain>

// [Serializable] for saved data; [SerializeField] for inspector-wired refs
// XML doc comments (///) on public types/members
// public surface first, private implementation last
```

### Prefab naming
- Player avatar: `Assets/Prefabs/PlayerAvatar.prefab`
- Material: `Assets/Prefabs/Materials/RoofingMaterial.prefab` (optional — a procedural
  fallback exists in `MaterialBlobFactory`)
- UI: `Assets/Prefabs/UI/{Component}.prefab`

---

## Implementation patterns

### Career save/load (`SaveManager`)
```csharp
// Save (with automatic backup of the previous file)
string json = JsonConvert.SerializeObject(career, Formatting.Indented);
File.WriteAllText(Path.Combine(savePath, $"{career.playerName}_career.json"), json);

// Load
Career career = JsonConvert.DeserializeObject<Career>(File.ReadAllText(saveFile));
```

### Per-job update loop
`MaterialPhysics` drives its own deformation/settling in `FixedUpdate`. The job instance
samples coverage each frame; the HUD polls it.
```csharp
// RoofingJobInstance.Update()
roofSurface.UpdateCoverage(appliedMaterials);   // raycast sampling
if (CheckCompletion()) CompleteJob();            // completion before failure checks
// HUD.Update() reads job.CurrentCoveragePercent / CurrentAverageThickness
```

### Multiplayer material sync (`NetworkRoofingMaterial`, Mirror)
Server-authoritative, deterministic event replication (not raw vertex streaming):
```csharp
[Command(requiresAuthority = false)]            // shared scene object, any client may call
public void CmdApply(Vector3 point)
{
    if (!jobInstance.HasMaterialAvailable) return;
    int blobId = ResolveBlobId(point);          // reuse nearby blob or assign a new id
    ApplyToBlob(blobId, point);                  // authoritative apply on server
    jobInstance.ConsumeMaterial(massPerApply);
    RpcApply(Quantize(point), blobId);           // 1cm-quantized broadcast
}

[ClientRpc]
void RpcApply(Vector3 point, int blobId)
{
    if (isServer) return;                        // host already applied in the Command
    ApplyToBlob(blobId, point);                  // deterministic Deform → converges
}
```

---

## Testing strategy

- **EditMode** (`Tests/EditMode/`): career progression, completion criteria, coverage math.
- **PlayMode** (`Tests/PlayMode/`): job init, material physics under gravity, save/load
  round-trip.
- **Validation**: the 8 end-to-end scenarios in `specs/001-roofing-simulator/quickstart.md`.

---

## Performance targets (from spec)

| Metric | Target | How to measure |
|--------|--------|----------------|
| FPS | 60+ | Profiler |
| Input latency | <100ms | click → visible deformation |
| Multiplayer sync | 100–200ms | network round-trip + state update |
| Coverage update | real-time | HUD updates each frame |
| Job load time | <1s | menu → gameplay |
| Save/load time | <500ms | file I/O |

**Optimization levers**: object-pool material blobs; batch coverage raycasts; LOD the
material mesh when blob count is high; delta/quantize network messages.

---

## Troubleshooting

**Material falls through / floats**: ensure the roof is on the **RoofSurface** layer and
`MaterialPhysics.roofMask` includes it; the convex `MeshCollider` must refresh after mesh
edits.

**Multiplayer desync**: confirm the apply path goes through `CmdApply` (server-
authoritative); blobs must **not** have their own `NetworkIdentity` (they are reproduced
deterministically). Watch `NetworkTime.rtt`.

**Save file issues**: `SaveManager` backs up on save and exposes `RestoreFromBackup`;
loads are wrapped in try/catch and degrade gracefully on malformed JSON.

**Coverage inaccurate**: reduce sample spacing in `RoofSurface.InitializeCoverageSampling`,
verify raycasts hit the material layer, and check the thickness offset.

---

## References

- Specification: `specs/001-roofing-simulator/spec.md`
- Implementation plan: `specs/001-roofing-simulator/plan.md`
- Data model: `specs/001-roofing-simulator/data-model.md`
- Technical research: `specs/001-roofing-simulator/research.md`
- Validation guide: `specs/001-roofing-simulator/quickstart.md`
- Network protocol: `specs/001-roofing-simulator/contracts/multiplayer-protocol.md`
- Editor wiring: `EDITOR_SETUP_GUIDE.md`
