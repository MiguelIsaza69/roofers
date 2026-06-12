# Roofing Simulator Implementation Guide

**Date**: 2026-06-11  
**Status**: Phase 1 Setup - In Progress  
**Total Tasks**: 98  
**Completed**: 3  

---

## Quick Start

This guide provides a roadmap for implementing the roofing simulator feature following the task breakdown in `specs/001-roofing-simulator/tasks.md`.

### Prerequisites

- Unity 2022 LTS or later
- Mirror networking library (for multiplayer)
- Newtonsoft Json.NET (for save/load)
- C# development environment

### Project Structure

The project is organized in Assets/ by functional domain:

```
Assets/Scripts/
├── Core/              # GameManager, CareerManager, Career data model
├── Gameplay/          # RoofingJob, RoofingMaterial, RoofSurface, MaterialPhysics
├── Input/             # PlayerInput, CameraController
├── UI/                # CareerUI, JobUI, HUD, MainMenuUI
├── Multiplayer/       # MultiplayerManager, NetworkRoofingMaterial, PlayerAvatarSync
├── Persistence/       # SaveManager, CareerData serialization
└── Utils/             # CoverageCalculator, utility functions
```

---

## Implementation Phases

### Phase 1: Setup (3-4 hours) ✓ IN PROGRESS

**Objective**: Project initialization and dependencies

**Completed Tasks**:
- [x] T001 Create Unity project structure
- [ ] T002 Add Mirror networking library
- [ ] T003 Add Newtonsoft Json.NET
- [x] T004 Configure assembly definitions (RoofingSimulator.asmdef, RoofingSimulator.Tests.asmdef)
- [ ] T005 Create scene structure

**Next Steps**:
1. Import Mirror from Unity Package Manager or Assets Store
2. Add Newtonsoft Json.NET via NuGet or as a DLL
3. Create 4 empty scenes in Assets/Scenes/ (MainMenu, Career, RoofingJob, Multiplayer)

**Estimated Time**: 1-2 hours

---

### Phase 2: Foundational (8-10 hours) NEXT

**Objective**: Core infrastructure blocking all user stories

**Status**: Ready to begin

**Key Tasks**:
- [ ] T009-T013: Core data models (Career, RoofingJob, RoofingMaterial, RoofSurface)
- [ ] T014-T017: Persistence layer (SaveManager, CareerData)
- [ ] T018-T020: Input & camera system
- [ ] T021-T023: Physics foundation
- [ ] T024-T026: Coverage calculation

**Data Models Created**:
- ✓ Career.cs - Player career tracking with performance metrics
- ✓ RoofingJob.cs - Job definition with difficulty parameters
- ✓ RoofingMaterial.cs - Deformable material with physics

**Remaining Models**:
- [ ] RoofSurface.cs - Roof geometry and coverage tracking
- [ ] RoofingJobInstance.cs - Job state during gameplay
- [ ] SaveManager.cs - Persistence layer

**Critical**: This phase must complete before any user story implementation

**Estimated Time**: 8-10 hours

---

### Phase 3: User Story 1 - Solo Career Progression (P1)

**Objective**: Career creation, progression, persistence (MVP foundation)

**Status**: Pending Phase 2 completion

**Key Components**:
- GameManager - Central game orchestrator
- CareerManager - Career state and progression
- UI Systems - MainMenu, Career overview, Job briefing, Completion screen

**Independent Test** (from spec):
```
Given: Player creates new career with name "TestPlayer"
When: Player completes 3 sequential jobs
Then: Progress is saved and restored on game reload
```

**Estimated Time**: 6-8 hours

---

### Phase 4: User Story 2 - Physics-Based Roofing Mechanics (P1)

**Objective**: Material application, deformation, coverage tracking (MVP core gameplay)

**Status**: Pending Phase 2 completion

**Key Components**:
- RoofingMaterialTool - Player interaction
- MaterialPhysics - Deformation and spreading simulation
- RoofSurface - Coverage tracking via raycast sampling
- HUD - Real-time feedback (coverage %, material budget, time)

**Independent Test** (from spec):
```
Given: Player in active roofing job
When: Player applies material and moves it around
Then: Material deforms realistically, coverage updates in real-time
And: FPS stays above 60
```

**Key Physics Parameters**:
- Elasticity: 0.3 (bounciness)
- Adhesion: 0.7 (stickiness between blobs)
- Density: 1200 kg/m³
- Spreading rate: Tunable per job

**Estimated Time**: 10-12 hours

---

### Phase 5: User Story 3 - Cooperative Multiplayer (P2)

**Objective**: 2-4 players working together with synchronized material state

**Status**: After Phase 4 completion

**Key Components**:
- MultiplayerManager - Session creation/joining
- Mirror networking setup - Connection and RPC handlers
- NetworkRoofingMaterial - Material state synchronization
- PlayerAvatarSync - Avatar position/rotation sync

**Network Architecture**:
- Server-authoritative physics simulation
- 20 Hz network tick rate
- Delta-compressed material mesh updates
- 100-200ms target latency tolerance

**Independent Test** (from spec):
```
Given: Two players in same job session
When: Player A applies material, Player B observes
Then: Material appears on B's screen within 200ms
And: Both players see identical material positions
```

**Estimated Time**: 8-10 hours

---

### Phase 6: User Story 4 - Difficulty Progression (P2)

**Objective**: 10-15 designed jobs with progressive difficulty

**Status**: After Phase 4 completion

**Key Components**:
- JobConfiguration - Data-driven job definitions
- Difficulty parameters - 10-15 job configuration files
- Constraint enforcement - Time limits, material budgets, quality thresholds
- Job progression curve - Smooth ramp from tutorial to challenging

**Job Progression**:
- Jobs 1-3: Learning phase (5-10 min, 85% coverage, STANDARD quality)
- Jobs 4-7: Intermediate (10-15 min, 88-90% coverage, HIGH quality)
- Jobs 8-15: Advanced (20-30 min, 92%+ coverage, PRISTINE quality)

**Estimated Time**: 6-8 hours

---

### Phase 7: Polish & Validation (4-6 hours)

**Objective**: Testing, optimization, documentation

**Key Tasks**:
- [ ] T074-T081: Run all 8 quickstart validation scenarios
- [ ] T082: Regression test checklist
- [ ] T083-T087: Performance optimization
- [ ] T088-T093: Documentation and polish

**Validation Checkpoints**:
- Game launches without crash
- Career creation and save/load works
- Material deformation achieves 60+ FPS
- Multiplayer sessions sync within 200ms
- All 8 difficulty job types complete successfully

**Estimated Time**: 4-6 hours

---

## Development Timeline

### MVP (User Stories 1 + 2 Only)

**Recommended for initial release**:

```
Week 1:
  Day 1-2: Phase 1 Setup + early Phase 2
  Day 3-5: Phase 2 Foundational completion

Week 2:
  Day 1-2: Phase 3 (Career progression)
  Day 3-4: Phase 4 (Physics mechanics)
  Day 5: Testing and validation

Total: ~40-50 hours for 1-2 developers
```

### Full Feature (All 4 User Stories)

```
Week 1-2: Setup + Foundational (as above)
Week 3:   User Stories 1 + 2 (as above)
Week 4:   User Story 3 (Multiplayer) - 8-10 hours
Week 5:   User Story 4 (Difficulty) - 6-8 hours
          Polish & Testing - 4-6 hours

Total: ~60-70 hours for 1-2 developers
With 3-4 developers: ~15-20 hours per person (parallel execution)
```

---

## Key Technical Decisions

Reference `specs/001-roofing-simulator/research.md` for detailed rationale on:

1. **Networking**: Mirror for real-time multiplayer
2. **Physics**: Unity PhysX + custom deformation layer
3. **Persistence**: Local JSON file storage
4. **Coverage**: Raycast-based grid sampling
5. **Difficulty**: Data-driven configuration system

---

## File Organization & Conventions

### Naming Conventions

- **Classes**: PascalCase (e.g., `CareerManager`, `RoofingJob`)
- **Methods**: PascalCase (e.g., `UpdateCoverage()`)
- **Fields**: camelCase (e.g., `totalMass`, `elasticity`)
- **Constants**: UPPER_CASE (e.g., `MIN_COVERAGE_PERCENT = 60`)

### Script Organization

Each script should have:

```csharp
using statements
namespace RoofingSimulator.Domain

[Serializable] attributes where needed
[System.NonSerialized] for transient data

Class definition with:
  - XML documentation comments (///)
  - Public properties/methods first
  - Private implementation details last
```

### Prefab Naming

- Player prefab: `Assets/Prefabs/PlayerAvatar.prefab`
- Material prefab: `Assets/Prefabs/Materials/RoofingMaterial.prefab`
- UI prefabs: `Assets/Prefabs/UI/{ComponentName}.prefab`
- Job prefabs: `Assets/Prefabs/RoofingJobs/{JobName}.prefab`

---

## Common Implementation Patterns

### Data Serialization (Career Save/Load)

```csharp
// Saving
Career career = GetCurrentCareer();
string json = JsonConvert.SerializeObject(career);
File.WriteAllText($"{savePath}/{career.playerName}_career.json", json);

// Loading
string json = File.ReadAllText(saveFile);
Career career = JsonConvert.DeserializeObject<Career>(json);
```

### Physics Update Loop

```csharp
void Update()
{
    material.UpdatePhysics(Time.deltaTime);
    roofSurface.UpdateCoverage();
    HUD.UpdateCoverageDisplay(roofSurface.coveragePercent);
}
```

### Multiplayer State Sync (Mirror)

```csharp
[Command]
void CmdApplyMaterial(Vector3 position, Vector3 force, float mass)
{
    // Server validates and applies
    serverMaterial.ApplyMaterial(position, force, mass);
    RpcApplyMaterial(position, force, mass);
}

[ClientRpc]
void RpcApplyMaterial(Vector3 position, Vector3 force, float mass)
{
    // All clients apply the material
    clientMaterial.ApplyMaterial(position, force, mass);
}
```

---

## Testing Strategy

### Unit Tests

Create tests in `Tests/EditMode/` for:
- Career progression logic
- Job completion criteria
- Coverage calculation accuracy

### Integration Tests

Create tests in `Tests/PlayMode/` for:
- Job loading and initialization
- Material physics with gravity
- Career save/load round-trip

### Validation Tests

Use `quickstart.md` scenarios for:
- End-to-end gameplay validation
- Multiplayer synchronization testing
- Difficulty progression validation

---

## Performance Targets

**Benchmark targets from spec**:

| Metric | Target | How to Measure |
|--------|--------|----------------|
| FPS | 60+ | Profiler > Memory |
| Input Latency | <100ms | Time from click to visible deformation |
| Multiplayer Sync | 100-200ms | Network latency + state update time |
| Coverage Update | Real-time | HUD updates each frame |
| Job Load Time | <1s | Time from menu to gameplay |
| Save/Load Time | <500ms | Profiler > Memory |

**Optimization Tips**:
- Use object pooling for material blobs
- Batch raycast calls for coverage calculation
- Use LOD for material mesh when many blobs exist
- Compress network messages with delta encoding

---

## Troubleshooting

### Common Issues

**Physics mesh not updating**:
- Ensure MeshCollider has `convex = true`
- Call `meshCollider.convex = true` after mesh changes
- Check that mesh deformation stays within bounds

**Multiplayer desync**:
- Verify network tick rate is 20 Hz
- Check that clients reconcile local predictions with server
- Monitor latency with `Debug.Log(NetworkTime.rtt)`

**Save file corruption**:
- Implement backup on save
- Validate JSON against schema on load
- Handle missing fields gracefully with defaults

**Coverage calculation inaccurate**:
- Increase sample point density (reduce spacing)
- Verify raycasts hit correct layers
- Check thickness calculation uses correct offset

---

## Next Steps

1. **Phase 1 Completion** (Today):
   - Import Mirror and Json.NET
   - Create scene files
   - Commit to git: `git add -A && git commit -m "Phase 1: Project setup complete"`

2. **Phase 2 Start** (Tomorrow):
   - Create remaining data models (RoofSurface, JobInstance)
   - Implement SaveManager persistence layer
   - Create input system placeholder

3. **Phase 3 Start** (After Phase 2):
   - Create GameManager and CareerManager
   - Build UI screens (MainMenu, Career, JobBriefing)
   - Implement career progression logic

---

## References

- **Specification**: `specs/001-roofing-simulator/spec.md`
- **Implementation Plan**: `specs/001-roofing-simulator/plan.md`
- **Data Model**: `specs/001-roofing-simulator/data-model.md`
- **Technical Research**: `specs/001-roofting-simulator/research.md`
- **Validation Guide**: `specs/001-roofting-simulator/quickstart.md`
- **Network Protocol**: `specs/001-roofing-simulator/contracts/multiplayer-protocol.md`

---

## Questions or Issues?

Refer to:
1. Specification for "what" to build
2. Plan for "how" to architect it
3. Data model for "what" data to manage
4. Research for technical decision rationale
5. Quickstart for validation scenarios

Good luck with implementation! 🎮
