# Roofing Simulator with Putty Physics

A first-person roofing simulator built in **Unity (C#)**: spread claymation-style putty
across roofs to meet coverage and quality targets, progress through a 15-job career of
escalating difficulty, and play solo or in 2–4-player co-op. Built spec-first with
[Spec Kit](https://github.com/github/spec-kit).

> **New here? This is the onboarding note — read it first, then follow the "Reading order"
> below.**

---

## Current status (read this honestly)

- **All feature code is written** for User Stories 1–4 (solo career, putty physics,
  difficulty progression, co-op multiplayer).
- **The C# is not compiler-verified.** It was authored without a Unity Editor in the
  loop. The first Editor open (with packages installed) is expected to surface fixes —
  most likely in the Mirror networking layer, the least-certain code.
- **The game is not yet runnable** without an Editor pass to author scenes, prefabs, and
  UI, and to wire components. That pass is fully documented (see `EDITOR_SETUP_GUIDE.md`).
- **Source of truth for progress**: `STATUS.md` (human-readable, kept current),
  `specs/001-roofing-simulator/tasks.md` (task-level checkboxes with ⚠️ notes on
  Editor/art tasks), and the **git log**.

---

## Reading order

Follow this path depending on what you're doing.

### To understand *what* and *why* (product/design)
1. **`specs/001-roofing-simulator/spec.md`** — the feature spec: user stories,
   requirements (FR-001…), success criteria. Non-technical, start here.
2. **`specs/001-roofing-simulator/research.md`** — key technical decisions and their
   rationale (Mirror, PhysX + custom deformation, JSON saves, raycast coverage).
3. **`specs/001-roofing-simulator/data-model.md`** — the entities (Career, RoofingJob,
   RoofingMaterial, RoofSurface, MultiplayerSession) and their relationships.

### To build / run it (engineering)
4. **`DEPENDENCIES_SETUP.md`** — install Mirror + Newtonsoft Json.NET.
5. **`EDITOR_SETUP_GUIDE.md`** — **the key doc**: exact scene hierarchies, component
   wiring tables (serialized field → object), NetworkManager + avatar prefab setup,
   build-settings scene order, and a smoke-test sequence. Field names are verified against
   the source.
6. **`specs/001-roofing-simulator/quickstart.md`** — 8 end-to-end validation scenarios to
   run once it's wired (these are the real acceptance tests).

### To extend it (deep dive)
7. **`specs/001-roofing-simulator/plan.md`** — architecture and source-tree layout.
8. **`specs/001-roofing-simulator/tasks.md`** — the 98-task breakdown by phase; current
   checkbox state shows what's code-done vs. Editor/art-pending.
9. **`specs/001-roofing-simulator/contracts/`** — the data/network contracts:
   `job-configuration-schema.json`, `career-save-schema.json`, `multiplayer-protocol.md`.

---

## Architecture at a glance

### Assemblies
| Assembly | Path | Depends on |
|----------|------|-----------|
| `RoofingSimulator` | `Assets/Scripts/` | UnityEngine.UI, Newtonsoft.Json |
| `RoofingSimulator.Multiplayer` | `Assets/Scripts/Multiplayer/` | RoofingSimulator, **Mirror**, UI |
| `RoofingSimulator.Tests` | `Tests/` | RoofingSimulator, NUnit |

> Multiplayer is **isolated** so the core single-player game compiles even if Mirror
> isn't installed.

### Script map (`Assets/Scripts/`)
| Folder | Key types | Role |
|--------|-----------|------|
| `Core/` | `GameManager`, `CareerManager`, `JobSceneController`, `Career` | App state, scene flow, career progression |
| `Gameplay/` | `RoofingJob`, `RoofingJobInstance`, `RoofingMaterial`, `MaterialPhysics`, `RoofSurface`, `RoofingMaterialTool`, `JobCatalog`, `JobConfiguration(+Loader)`, `MaterialBlobFactory` | The game itself: jobs, putty physics, coverage |
| `Input/` | `CameraController`, `PlayerInput` | First-person look + apply raycast |
| `UI/` | `MainMenuUI`, `CareerUI`, `JobBriefingUI`, `CompletionScreenUI`, `HUD` | uGUI controllers |
| `Persistence/` | `SaveManager` | JSON career save/load + backups |
| `Utils/` | `CoverageCalculator` | Raycast coverage sampling |
| `Multiplayer/` | `RoofingNetworkManager`, `MultiplayerManager`, `PlayerAvatarSync`, `NetworkRoofingMaterial`, `MultiplayerUI` | Mirror co-op layer |

### Core data flow
```
GameManager (state + scene transitions)
   └─ CareerManager ── JobCatalog ←── JobConfigurationLoader ←── Resources/Jobs/*.json
        └─ Career ──(SaveManager)──► {persistentDataPath}/saves/<name>_career.json
   When a job starts:
   JobSceneController → RoofingJobInstance(job) → RoofSurface (coverage) + RoofingMaterial[]
        ▲ PlayerInput (raycast) → RoofingMaterialTool → MaterialBlobFactory / MaterialPhysics (deform)
        └ HUD polls coverage/quality each frame
   Multiplayer overlay: PlayerAvatarSync (transform) + NetworkRoofingMaterial
        (server-authoritative apply events, deterministic replay)
```

---

## How it was built

This project was developed with the **Spec Kit** workflow, in order:
`/speckit-specify` → `/speckit-plan` → `/speckit-tasks` → `/speckit-implement`.
Each artifact feeds the next, so when changing behaviour, **update the spec/plan/tasks**
alongside the code to keep them in sync. The `.specify/` folder holds the templates and
the workflow extension config.

---

## Known gaps (planned, not done)

These are tracked in `EDITOR_SETUP_GUIDE.md` §7 and the ⚠️ task notes:
- **Editor authoring pending**: UI canvases (T030/T032/T044), player-avatar prefab
  (T054), multiplayer scene (T060), roof meshes (T047/T069).
- **Multiplayer job-selection isn't synced** — each client must load the same job index;
  a SyncVar for the chosen job is the clean fix.
- **Session codes are direct addresses** (LAN/direct works; internet needs a relay).
- **Phase 7 polish/validation** (`tasks.md` T074–T098) is unstarted and depends on the
  Editor pass to be meaningful.

---

## Quick start (TL;DR)

1. Open in Unity 2022 LTS+. Install **Mirror** and **Newtonsoft Json.NET**
   (`DEPENDENCIES_SETUP.md`).
2. Confirm a clean compile.
3. Follow **`EDITOR_SETUP_GUIDE.md`** to author the scenes/prefabs and wire components.
4. Run the smoke test in that guide (§8), then the full scenarios in `quickstart.md`.

---

*Repo layout: game code in `Assets/`, tests in `Tests/`, all design docs in
`specs/001-roofing-simulator/`, build/setup guides at the repo root.*
