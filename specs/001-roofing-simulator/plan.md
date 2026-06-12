# Implementation Plan: Roofing Simulator with Putty Physics

**Branch**: `001-roofing-simulator` | **Date**: 2026-06-11 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-roofing-simulator/spec.md`

**Note**: This plan is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Build a first-person roofing simulator game in Unity featuring physics-based putty material deformation, a solo career progression system with 10-15 designed levels of escalating difficulty, and cooperative multiplayer support for 2-4 players with real-time material synchronization (100-200ms latency tolerance). Core gameplay involves applying and shaping roofing material to meet coverage and quality requirements on progressively complex roof geometries.

## Technical Context

**Language/Version**: C# with Unity 2022 LTS or later

**Primary Dependencies**: 
- Unity Engine (physics, rendering, UI)
- Mirror or Netcode for GameObjects (multiplayer synchronization)
- Newtonsoft Json.NET (career save/load)

**Storage**: Player career data persisted as JSON files locally (career progress, completed jobs, current state)

**Testing**: Unity Test Framework (UTF) for unit and integration tests

**Target Platform**: PC (Windows, macOS, Linux) with keyboard + mouse or gamepad input

**Project Type**: Standalone game application (desktop game)

**Performance Goals**: 
- 60+ FPS during gameplay
- Material deformation response within 100ms user input latency
- Multiplayer state synchronization within 100-200ms (real-time cooperation)

**Constraints**: 
- Multiplayer synchronization latency: 100-200ms maximum for responsive cooperation
- Physics simulation must handle 2-4 concurrent players applying material simultaneously
- Career save/load must be 100% reliable (no progress loss)
- Level design must scale difficulty smoothly from tutorial (5-10 min) to challenging (20-30 min)

**Scale/Scope**: 
- 10-15 designed roofing jobs in progression system
- 2-4 concurrent players in multiplayer sessions
- Player career tracking across sessions
- First-person perspective with roof surface interaction

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

No constitution file with active constraints. This feature operates under standard game development best practices.

## Project Structure

### Documentation (this feature)

```text
specs/001-roofing-simulator/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command)
```

### Source Code (Unity Project)

```text
Assets/
├── Scripts/
│   ├── Core/                    # Core game loop and initialization
│   │   ├── GameManager.cs
│   │   └── CareerManager.cs
│   ├── Gameplay/
│   │   ├── RoofingJob.cs        # Job definition and state
│   │   ├── RoofSurface.cs       # Roof geometry and coverage tracking
│   │   ├── RoofingMaterial.cs   # Physics-based material system
│   │   └── MaterialPhysics.cs   # Deformation and spreading simulation
│   ├── Input/
│   │   └── PlayerInput.cs       # Input handling (mouse/keyboard/gamepad)
│   ├── UI/
│   │   ├── CareerUI.cs          # Career progression UI
│   │   ├── JobUI.cs             # Job objectives and feedback
│   │   └── HUD.cs               # In-game HUD (coverage, constraints)
│   ├── Multiplayer/
│   │   ├── MultiplayerManager.cs
│   │   ├── NetworkRoofingMaterial.cs  # Networked material sync
│   │   └── PlayerAvatarSync.cs        # Avatar synchronization
│   ├── Persistence/
│   │   ├── CareerData.cs        # Career save/load structure
│   │   └── SaveManager.cs       # Persistence layer
│   └── Utils/
│       └── CoverageCalculator.cs  # Coverage detection and metrics

├── Scenes/
│   ├── MainMenu.unity           # Main menu scene
│   ├── Career.unity             # Career overview scene
│   ├── RoofingJob.unity         # Primary gameplay scene (parameterized)
│   └── Multiplayer.unity        # Multiplayer session scene

├── Prefabs/
│   ├── RoofingJobs/             # Job configuration prefabs
│   ├── RoofGeometries/          # Roof mesh variations
│   ├── Materials/               # Roofing material prefabs
│   ├── UI/                      # UI prefab library
│   └── VFX/                     # Visual effects

├── Physics Materials/
│   └── RoofingPutty.physicMaterial  # Putty material properties

├── Models/                      # 3D models
├── Animations/                  # Character/tool animations
├── Audio/                       # Sound effects and music
└── Resources/                   # Runtime-loaded resources

Tests/
├── PlayMode/
│   ├── GameplayTests.cs         # Physics and coverage tests
│   ├── MultiplayerTests.cs      # Multiplayer synchronization tests
│   └── PersistenceTests.cs      # Save/load tests
└── EditMode/
    └── DataModelTests.cs        # Data structure validation

ProjectSettings/
└── [Unity project configuration]
```

**Structure Decision**: Unity standard project layout with feature-based organization. `Scripts/` organized by functional domain (Gameplay, Multiplayer, UI, Persistence) to support parallel development. Physics and deformation logic centralized in `MaterialPhysics.cs`. Multiplayer synchronization handled through Mirror with networked material state in `NetworkRoofingMaterial.cs`.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
