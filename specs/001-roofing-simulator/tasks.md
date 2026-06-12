# Tasks: Roofing Simulator with Putty Physics

**Input**: Design documents from `/specs/001-roofing-simulator/`

**Prerequisites**: plan.md, spec.md, data-model.md, research.md, quickstart.md, contracts/

**Organization**: Tasks grouped by user story to enable independent implementation and testing

**Tests**: NOT included in this task list (no tests explicitly requested in specification)

---

## Phase 1: Setup (Project Initialization)

**Purpose**: Project structure and foundational setup

### Unity Project & Dependencies

- [x] T001 Create Unity project structure per plan.md with Assets/, Tests/, and ProjectSettings/
- [ ] T002 [P] Add Mirror networking library (version 2024.1+) to Assets/Plugins/ or via Unity Package Manager
- [ ] T003 [P] Add Newtonsoft Json.NET (NuGet package) for career save/load serialization
- [ ] T004 [P] Configure assembly definitions: RoofingSimulator.asmdef (main game), RoofingSimulator.Tests.asmdef
- [ ] T005 Create scene structure: Assets/Scenes/MainMenu.unity, Career.unity, RoofingJob.unity, Multiplayer.unity

### Project Configuration

- [ ] T006 Configure input system: Assets/Input/ with mouse/keyboard and gamepad bindings for tool application, camera control
- [ ] T007 Set up game folder structure per plan.md in Assets/Scripts/ (Core/, Gameplay/, Input/, UI/, Multiplayer/, Persistence/, Utils/)
- [ ] T008 Create prefab directories: Assets/Prefabs/ with subdirectories for RoofingJobs/, RoofGeometries/, Materials/, UI/, VFX/

**Checkpoint**: Project structure complete, dependencies installed, ready for foundational phase

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that ALL user stories depend on

⚠️ **CRITICAL**: No user story work can begin until this phase is 100% complete

### Core Data Models

- [x] T009 [P] Implement Career data model in Assets/Scripts/Core/Career.cs (ID, playerName, currentJobIndex, unlockedJobIndex, jobCompletions array, performanceMetrics)
- [x] T010 [P] Implement RoofingJob data model in Assets/Scripts/Gameplay/RoofingJob.cs (id, name, difficulty, roofGeometryId, minCoveragePercent, minQuality, materialBudget, timeLimit, difficultyScaling)
- [x] T011 [P] Implement RoofingMaterial data model in Assets/Scripts/Gameplay/RoofingMaterial.cs (position, deformationMesh, mass, elasticity, adhesion, coverage properties)
- [x] T012 [P] Implement RoofSurface data model in Assets/Scripts/Gameplay/RoofSurface.cs (mesh, totalArea, coverageSamples array, coverage tracking)
- [x] T013 [P] Implement RoofingJobInstance state model in Assets/Scripts/Gameplay/RoofingJobInstance.cs (job definition, currentState, elapsed time, material array, coverage %)

### Persistence Layer

- [x] T014 Implement SaveManager in Assets/Scripts/Persistence/SaveManager.cs to load/save career JSON to {GameDataPath}/saves/{playerName}_career.json
- [x] T015 Implement CareerData serialization (Newtonsoft.Json serialization of Career handled in SaveManager per career-save-schema.json contract)
- [x] T016 Add career save validation in Assets/Scripts/Persistence/SaveManager.cs (try/catch, backup-on-save and RestoreFromBackup for corrupted files)
- [x] T017 Implement career load and restore in Assets/Scripts/Core/CareerManager.cs (load career, restore current job state, replay completed jobs)

### Input & Camera System

- [x] T018 [P] Implement first-person camera controller in Assets/Scripts/Input/CameraController.cs (look around with mouse, maintain hand/tool position relative to camera)
- [x] T019 [P] Implement PlayerInput system in Assets/Scripts/Input/PlayerInput.cs (detect tool activation from mouse/gamepad, provide raycast hit points for material application)
- [x] T020 Implement raycast detection in Assets/Scripts/Input/PlayerInput.cs (raycast from camera center to roof surface, track hit point and hit normal for material application)

### Physics Foundation

- [x] T021 Create physics material asset: Assets/Physics Materials/RoofingPutty.physicMaterial with base properties (elasticity 0.3, friction 0.7, density 1200 kg/m³)
- [x] T022 Implement MaterialPhysics base system in Assets/Scripts/Gameplay/MaterialPhysics.cs (gravity simulation, vertex deformation, mesh update per frame)
- [x] T023 Implement mesh deformation pipeline in Assets/Scripts/Gameplay/MaterialPhysics.cs (vertex position updates, mesh.RecalculateNormals, bounds recalculation)

### Coverage & Metrics

- [x] T024 Implement CoverageCalculator in Assets/Scripts/Utils/CoverageCalculator.cs to perform raycast-based coverage sampling on RoofSurface
- [x] T025 Implement coverage grid generation in Assets/Scripts/Utils/CoverageCalculator.cs (distribute sample points across roof, configurable density)
- [x] T026 Implement quality threshold checking in Assets/Scripts/Utils/CoverageCalculator.cs (detect minimum thickness requirements per QualityThreshold enum)

**Checkpoint**: Foundation complete - all data models, persistence, input, physics base, and coverage systems ready. **User story implementation can now begin in parallel**

---

## Phase 3: User Story 1 - Solo Career Progression (Priority: P1) 🎯 MVP

**Goal**: Players can create careers, complete sequential jobs, and have their progress persisted across sessions

**Independent Test** (from spec): Player creates career, completes 3 sequential jobs with increasing difficulty, verifies progress saved and restored

### Implementation for User Story 1

- [x] T027 [P] Implement GameManager in Assets/Scripts/Core/GameManager.cs (manage game state, scene transitions, career initialization)
- [x] T028 [P] Implement CareerManager progression in Assets/Scripts/Core/CareerManager.cs (track current career, manage job progression, detect job unlocks) + JobCatalog.cs
- [x] T029 Implement job completion detection in Assets/Scripts/Core/CareerManager.cs (TryCompleteJob checks coverage % and quality thresholds, records + unlocks next job)
- [ ] T030 [P] Create MainMenu scene UI in Assets/Scenes/MainMenu.unity with "New Career" and "Load Career" buttons — ⚠️ EDITOR TASK: requires hand-authoring uGUI hierarchy; MainMenuUI controller ready to attach
- [x] T031 Implement MainMenuUI controller in Assets/Scripts/UI/MainMenuUI.cs (create new career, prompt for player name, load existing careers)
- [ ] T032 [P] Create Career overview scene in Assets/Scenes/Career.unity showing job list, completion status, progression metrics — ⚠️ EDITOR TASK: requires hand-authoring uGUI hierarchy; CareerUI/CompletionScreenUI controllers ready to attach
- [x] T033 Implement CareerUI in Assets/Scripts/UI/CareerUI.cs (display jobs, show completion status, handle job selection/start)
- [x] T034 Implement JobBriefing UI in Assets/Scripts/UI/JobBriefingUI.cs (display job requirements: coverage %, quality, material budget, time limit before job starts)
- [x] T035 Implement CompletionScreen UI in Assets/Scripts/UI/CompletionScreenUI.cs (display job stats: time, coverage %, quality rating, material used)
- [x] T036 Integrate job loading into CareerManager in Assets/Scripts/Core/CareerManager.cs (InitializeJobInstance loads job config from catalog into RoofingJobInstance)

**Checkpoint**: User Story 1 fully functional - career creation, job progression, persistence, UI complete. Player can: create career → start job → complete job → next job unlocks → progress saved.

---

## Phase 4: User Story 2 - Physics-Based Roofing Mechanics (Priority: P1)

**Goal**: Players apply and shape roofing material with realistic physics deformation and coverage tracking

**Independent Test** (from spec): Player applies material to roof surface, observes realistic deformation/spreading, coverage updates objectively

### Implementation for User Story 2

- [x] T037 [P] Implement material application tool in Assets/Scripts/Gameplay/RoofingMaterialTool.cs (respond to player input, create RoofingMaterial blob at raycast hit)
- [x] T038 [P] Implement RoofingMaterial blob in Assets/Scripts/Gameplay/MaterialBlobFactory.cs (procedural runtime blob w/ MeshCollider + dynamic mesh; authored .prefab optional Editor task)
- [x] T039 Implement material spawning in RoofingMaterialTool (spawn blob at raycast point, set initial mass/properties based on application rate)
- [x] T040 [P] Implement MaterialPhysics deformation in Assets/Scripts/Gameplay/MaterialPhysics.cs (apply input force to vertices, simulate gravity, update mesh each frame)
- [x] T041 Implement adhesion/merging logic (RoofingMaterialTool.ResolveTarget continues nearest blob within mergeRadius; RoofingMaterial.MergeWith/CanMergeWith for blob fusion)
- [x] T042 [P] Implement spreading/thinning behavior in Assets/Scripts/Gameplay/MaterialPhysics.cs (Deform spreads vertices inward via spreadingRate parameter)
- [x] T043 Implement geometry constraint in Assets/Scripts/Gameplay/MaterialPhysics.cs (ProjectVertexToSurface clamps vertices to roof, prevents floating)
- [ ] T044 [P] Create RoofingJob scene template in Assets/Scenes/RoofingJob.unity — ⚠️ EDITOR TASK: JobSceneController.cs builds a procedural roof + rig harness so the scene runs; authored .unity scene still TODO
- [x] T045 [P] Implement JobUI HUD in Assets/Scripts/UI/HUD.cs (display coverage % real-time, material budget remaining, time remaining if constrained)
- [x] T046 Implement material preview in Assets/Scripts/Gameplay/RoofingMaterialTool.cs (show material placement preview at raycast hit before application)
- [ ] T047 [P] Create roof geometry assets (simple models) in Assets/Models/*.fbx — ⚠️ ART TASK: binary meshes need the Editor/DCC tool; JobSceneController generates a procedural roof as a stand-in
- [x] T048 Implement coverage feedback UI in Assets/Scripts/UI/HUD.cs (coverage bar fill, coverage %, target marker, quality status color)
- [x] T049 Implement real-time coverage calculation (RoofingJobInstance.Update runs RoofSurface coverage sampling each frame; HUD polls + OnJobCompleted/OnJobFailed events)

**Checkpoint**: User Story 2 fully functional - material application, physics deformation, adhesion, spreading, coverage tracking, HUD feedback complete. Player can: apply material → see deformation → coverage updates → visual feedback.

---

## Phase 5: User Story 3 - Cooperative Multiplayer Roofing (Priority: P2)

**Goal**: 2-4 players work together on same job with real-time avatar and material synchronization

**Independent Test** (from spec): Two players join same job, see each other's avatars, apply material simultaneously, see synchronized material placement within 200ms

### Implementation for User Story 3

- [ ] T050 [P] Implement MultiplayerManager in Assets/Scripts/Multiplayer/MultiplayerManager.cs (manage session creation, player joining, network setup)
- [ ] T051 [P] Configure Mirror networking setup in Assets/Scripts/Multiplayer/NetworkSetup.cs (NetworkManager, transport settings, player prefab registration)
- [ ] T052 Implement session creation in MultiplayerManager (host creates session, spawns server, returns session code to player)
- [ ] T053 Implement session joining in MultiplayerManager (client joins via session code, connects to server)
- [ ] T054 [P] Create player avatar prefab in Assets/Prefabs/PlayerAvatar.prefab with NetworkIdentity and first-person controller
- [ ] T055 [P] Implement PlayerAvatarSync in Assets/Scripts/Multiplayer/PlayerAvatarSync.cs (synchronize position, rotation, animation state via Mirror NetworkBehaviour)
- [ ] T056 Implement avatar spawning in MultiplayerManager (spawn local avatar for player, remote avatars for other players)
- [ ] T057 [P] Implement NetworkRoofingMaterial in Assets/Scripts/Multiplayer/NetworkRoofingMaterial.cs (extend RoofingMaterial with Mirror NetworkBehaviour for state sync)
- [ ] T058 Implement material state synchronization in NetworkRoofingMaterial (serialize position, mesh deltas, ownership, broadcast to all clients at 20 Hz)
- [ ] T059 Implement delta compression for material mesh in NetworkRoofingMaterial (serialize only changed vertices per update, quantize positions to 1cm)
- [ ] T060 [P] Create Multiplayer scene in Assets/Scenes/Multiplayer.unity (extend RoofingJob scene with multiplayer setup, player spawn points)
- [ ] T061 Implement shared RoofSurface state in MultiplayerManager (all players see same roof surface, shared material list, synchronized coverage %)
- [ ] T062 Implement player disconnect handling in MultiplayerManager (detect player timeout/leave, remove avatar, preserve their material contributions)
- [ ] T063 Implement session abort UI in Assets/Scripts/UI/MultiplayerUI.cs (display "Player X disconnected", offer continue/abandon options)

**Checkpoint**: User Story 3 fully functional - multiplayer sessions, avatars, material sync, disconnect handling. Two players can: join same job → see each other → apply material together → synchronized state.

---

## Phase 6: User Story 4 - Difficulty Progression System (Priority: P2)

**Goal**: 10-15 designed jobs with progressive difficulty via parameterized configuration

**Independent Test** (from spec): Player experiences difficulty scaling from early jobs (5-10 min, 85% coverage) to late jobs (20-30 min, 92%+ coverage, pristine quality)

### Implementation for User Story 4

- [x] T064 [P] Create job configuration system in Assets/Scripts/Gameplay/JobConfigurationLoader.cs (load job configurations from JSON files per job-configuration-schema.json)
- [x] T065 Create job configuration files in Assets/Resources/Jobs/ (job_001.json through job_015.json with progressive difficulty; validated: ids 0-14 unique, difficulty monotonic 1-15)
- [x] T066 [P] Implement JobConfiguration data in Assets/Scripts/Gameplay/JobConfiguration.cs (id, name, difficulty, geometry reference, coverage requirement, quality, constraints + IsValid)
- [x] T067 Implement job difficulty parameterization in Assets/Scripts/Gameplay/JobConfiguration.cs (DifficultyScalingConfig: sizeMultiplier, materialAvailability, requiredQuality, geometryComplexity)
- [x] T068 Integrate JobConfiguration into JobCatalog/CareerManager (JobCatalog loads JSON configs on startup, falls back to built-in progression)
- [ ] T069 [P] Create roof geometry variants in Assets/Models/*.fbx — ⚠️ ART TASK: binary meshes need Editor/DCC; roofGeometryId wired through configs, procedural roof stands in per job area/complexity
- [x] T070 Implement difficulty progression curve (JobConfigurationLoader sorts by id + ValidateDifficultyCurve warns on dips; 15-job curve authored and verified monotonic)
- [x] T071 Implement time limit constraints in RoofingJobInstance (counts down while IN_PROGRESS, fails with TIME_EXCEEDED at limit; completion checked first)
- [x] T072 Implement material budget constraints in RoofingJobInstance (tool blocks apply when budget exhausted; instance fails OUT_OF_MATERIAL when depleted and objective unmet)
- [x] T073 Implement quality requirement enforcement in RoofingJobInstance (completion requires coverage % AND min thickness for quality; cannot complete below threshold)

**Checkpoint**: User Story 4 fully functional - 10-15 designed jobs, parameterized difficulty, constraints, progression curve. Player can: unlock sequence of jobs with escalating difficulty.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Optimization, testing validation, and final integration

### Testing & Validation

- [ ] T074 [P] Run quickstart.md Scenario 1 validation (career creation, first job load)
- [ ] T075 [P] Run quickstart.md Scenario 2 validation (material application, deformation, coverage)
- [ ] T076 [P] Run quickstart.md Scenario 3 validation (job completion, career progression)
- [ ] T077 [P] Run quickstart.md Scenario 4 validation (multiplayer session creation and joining)
- [ ] T078 [P] Run quickstart.md Scenario 5 validation (cooperative material application, sync)
- [ ] T079 [P] Run quickstart.md Scenario 6 validation (multiplayer disconnect handling)
- [ ] T080 [P] Run quickstart.md Scenario 7 validation (career persistence across sessions)
- [ ] T081 [P] Run quickstart.md Scenario 8 validation (difficulty progression from early to late jobs)
- [ ] T082 Run regression test checklist from quickstart.md (game launch, menus, material application, UI, multiplayer, FPS, save/load)

### Performance & Optimization

- [ ] T083 Profile physics simulation in Assets/Scripts/Gameplay/MaterialPhysics.cs (target 60+ FPS, optimize vertex updates, reduce calculations)
- [ ] T084 Optimize mesh deformation updates in MaterialPhysics (batch updates, use GPU compute if necessary for large material blobs)
- [ ] T085 Optimize network serialization in NetworkRoofingMaterial (ensure < 10 KB/sec per client, delta compression working)
- [ ] T086 Profile coverage calculation in CoverageCalculator (ensure real-time updates don't drop FPS below 60)
- [ ] T087 Optimize scene loading times (target < 1s for career/job scenes)

### Quality & Documentation

- [ ] T088 [P] Add XML documentation comments to all public classes in Assets/Scripts/
- [ ] T089 Create gameplay documentation in docs/ explaining core mechanics, physics model, job design
- [ ] T090 Create multiplayer networking guide in docs/ explaining Mirror setup, state synchronization, bandwidth targets
- [ ] T091 Create level design guide in docs/ with difficulty progression parameters for designers
- [ ] T092 Test audio placeholder system (if needed for job sounds, material application sounds)
- [ ] T093 Add player feedback improvements (cursor feedback, tooltips, hints for early jobs)

### Final Integration

- [ ] T094 [P] Build standalone Windows executable and test (Assets/Scenes included in build settings)
- [ ] T095 [P] Build standalone macOS executable and test
- [ ] T096 [P] Build standalone Linux executable and test
- [ ] T097 Verify all save files compatible across platforms
- [ ] T098 Final full playthrough: create career → complete 5 jobs solo → verify progression → test multiplayer → verify save/load

**Checkpoint**: All validation scenarios pass, performance targets met, game ready for release

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup) → MUST complete before anything else
    ↓
Phase 2 (Foundational) → BLOCKS all user stories
    ↓
Phase 3 (US1: Career) ─┐
Phase 4 (US2: Physics)─├─ Can start immediately after Phase 2
Phase 5 (US3: Multiplayer)─┤ Can progress in parallel if staffed
Phase 6 (US4: Difficulty)─┘ Or sequentially in priority order
    ↓
Phase 7 (Polish) → After desired stories complete
```

### User Story Dependencies

- **User Story 1 (Career P1)**: Depends on Phase 2 only. Can start immediately.
- **User Story 2 (Physics P1)**: Depends on Phase 2 only. Can start immediately, in parallel with US1.
- **User Story 3 (Multiplayer P2)**: Depends on Phase 2 + US1 + US2 (needs core gameplay working). Start after US1+US2.
- **User Story 4 (Difficulty P2)**: Depends on Phase 2 + US1 + US2. Start after US1+US2, can overlap with US3.

### Within Each User Story

**Optimal Sequence**:
1. Data models/prefabs (marked [P], parallel)
2. Core system implementation (sequential, may depend on models)
3. UI/feedback (can start after core system)
4. Integration tests (final validation)

---

## Parallel Opportunities

### All Setup Tasks (Phase 1)
All T002-T008 marked [P] can run in parallel:
```
T002: Mirror setup
T003: JSON library setup  
T004: Assembly definitions
T006: Input system
T007: Folder structure
T008: Prefab directories
```
Run together: 1 dev builds them in parallel

### All Foundational Tasks (Phase 2)
Groups of [P] marked tasks can run in parallel:

**Data Models Group** (T009-T013): All marked [P]
```
T009: Career model
T010: RoofingJob model
T011: RoofingMaterial model
T012: RoofSurface model
T013: JobInstance model
```
Run together: 2 devs complete in parallel

**Input/Camera Group** (T018-T019): Both marked [P]
```
T018: Camera controller
T019: PlayerInput system
```
Run together: 2 devs complete in parallel

**After models complete, persistence/physics can start independently**:
```
T014-T017: Persistence (depends on T009 Career model)
T021-T023: Physics foundation (independent)
T024-T026: Coverage (depends on T012 RoofSurface model)
```

### User Story 1 (Career) Parallel Tasks
Models and UI can run in parallel:
```
[P] T027: GameManager
[P] T028: CareerManager  
[P] T030: MainMenu scene
[P] T032: Career scene
```
Run first, then sequential UI/integration

### User Story 2 (Physics) Parallel Tasks
Material systems can run in parallel:
```
[P] T037: Material tool
[P] T038: Material prefab
[P] T040: Physics deformation
[P] T044: RoofingJob scene template
[P] T047: Roof geometry assets
```

### User Story 3 (Multiplayer) Parallel Tasks
Network components can run in parallel:
```
[P] T050: MultiplayerManager
[P] T051: NetworkSetup
[P] T054: Avatar prefab
[P] T055: AvatarSync
[P] T060: Multiplayer scene
```

### Suggested Parallel Team Distribution

**4-person team**:
- Developer 1: Phase 1 Setup + Phase 2 Data Models (T001-T013)
- Developer 2: Phase 2 Persistence + Physics (T014-T026) in parallel with Dev 1
- Developer 3: Phase 3 User Story 1 (Career) (T027-T036) - starts after Phase 2
- Developer 4: Phase 4 User Story 2 (Physics) (T037-T049) - starts after Phase 2, runs parallel with Dev 3

Then Phase 5/6 with rotations as team capacity allows.

---

## Implementation Strategy

### MVP First (Minimum Viable Product)

**Scope**: User Story 1 + User Story 2 only (solo career + physics)

1. ✅ Phase 1: Setup (3-4 hours) - Project structure, dependencies
2. ✅ Phase 2: Foundational (8-10 hours) - Core systems blocking everything
3. ✅ Phase 3: User Story 1 (6-8 hours) - Career progression, UI, persistence
4. ✅ Phase 4: User Story 2 (10-12 hours) - Material physics, deformation, coverage
5. ✅ Phase 7: Testing & Validation (4-6 hours) - Quickstart scenarios
6. **STOP AND DEPLOY**: MVP ready - solo career with physics working

**Total MVP Timeline**: ~31-40 hours for 1-2 developers

### Incremental Delivery (Add Stories Progressively)

After MVP (US1+US2):

1. Add User Story 3 (Multiplayer) - 8-10 hours
2. Add User Story 4 (Difficulty Progression) - 6-8 hours  
3. Final Polish - 4-6 hours

**Total Full Feature**: ~50-60 hours for 1-2 developers

### Parallel Team Strategy (Multiple Developers)

With 3-4 developers:

```
Hours 0-15:   All devs complete Phase 1 + Phase 2 (setup + foundation)
Hours 15-35:  Dev 1+2: User Story 1 | Dev 3: User Story 2 (parallel)
Hours 35-50:  Dev 4: User Story 3 | Dev 3: User Story 4 (while Dev 1+2 test)
Hours 50-60:  All devs: Polish, testing, optimization
```

**Total with 3-4 devs**: ~15-20 hours per person for full feature

---

## Validation Checkpoints

Stop at each checkpoint to validate independently before proceeding:

- ✅ **After Phase 2**: Run simple test - can create career, save/load works
- ✅ **After Phase 3 (US1)**: Run Scenario 1 & 3 from quickstart.md - career progression works
- ✅ **After Phase 4 (US2)**: Run Scenario 2 from quickstart.md - material physics works
- ✅ **After Phase 5 (US3)**: Run Scenario 4 & 5 from quickstart.md - multiplayer works
- ✅ **After Phase 6 (US4)**: Run Scenario 8 from quickstart.md - difficulty progression works
- ✅ **After Phase 7**: Run all 8 quickstart scenarios + regression checklist

---

## Notes

- **[P] marked tasks** = different files, no inter-task dependencies, can run in parallel
- **[Story] labels** = maps task to specific user story for traceability (US1, US2, US3, US4)
- Each user story is independently testable and deployable
- Avoid: Combining stories before each is validated
- Commit after each major task or logical group (every 2-3 tasks)
- Use feature branches per user story for easier integration
- Stop at checkpoints to validate before proceeding
