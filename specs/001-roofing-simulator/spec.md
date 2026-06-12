# Feature Specification: Roofing Simulator with Putty Physics

**Feature Branch**: `001-roofing-simulator`

**Created**: 2026-06-11

**Status**: Draft

**Input**: User description: "First-person roofing simulator game in Unity with claymation putty physics, solo and cooperative multiplayer career mode, and progressive difficulty like REPO."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Solo Career Progression (Priority: P1)

A player progresses through a career of roofing jobs, starting with simple residential roofs and advancing to complex commercial installations. Each completed job unlocks progressively harder jobs with tighter constraints, more complex geometries, and higher quality requirements.

**Why this priority**: Core gameplay loop and primary engagement driver. Players need a progression system that provides clear goals and meaningful advancement.

**Independent Test**: A player can start a career, complete at least 3 sequential jobs of increasing difficulty, and see their progress saved. Job completion is based on roofing material coverage meeting quality thresholds.

**Acceptance Scenarios**:

1. **Given** a player has not started a career, **When** they select "New Career", **Then** they see the first job (residential roof, small surface area)
2. **Given** a player completes a job meeting quality requirements, **When** they finish, **Then** the next job unlocks and difficulty increases
3. **Given** a player has completed jobs, **When** they load their career, **Then** their progress and completed jobs are restored

---

### User Story 2 - Physics-Based Roofing Mechanics (Priority: P1)

Players use physics-based tools to apply and shape roofing material (claymation putty-like substance) onto roof surfaces. The material behaves naturally—deforming, spreading, and conforming to roof geometry based on physics simulation.

**Why this priority**: Defines the core gameplay mechanic. Without responsive, intuitive physics, the game cannot deliver the intended experience.

**Independent Test**: A player can pick up roofing material, apply it to a roof surface, and observe realistic deformation and spreading behavior. Coverage can be measured objectively.

**Acceptance Scenarios**:

1. **Given** a player is in a job with roofing material available, **When** they apply material to a roof, **Then** it deforms naturally and conforms to the surface
2. **Given** material is applied to a roof, **When** they apply more material nearby, **Then** it spreads realistically and bonds with existing material
3. **Given** a player applies excess material, **When** gravity/physics simulation runs, **Then** material can slide or redistribute based on roof angle and physics parameters

---

### User Story 3 - Cooperative Multiplayer Roofing (Priority: P2)

Two or more players work together on the same roofing job in real-time. Players can see each other's avatars and synchronized material placement, enabling collaborative problem-solving on complex jobs.

**Why this priority**: Extends core experience to multiplayer. Enables emergent gameplay and increases replayability, but solo mode is the primary experience.

**Independent Test**: Two players can join the same job, see each other's avatars, apply material simultaneously, and complete the job with combined effort and synchronized physics state.

**Acceptance Scenarios**:

1. **Given** multiplayer is available, **When** player A creates a multiplayer session, **Then** player B can join the same job
2. **Given** both players are in the same job, **When** player A applies material, **Then** player B sees it applied in real-time
3. **Given** players are working together, **When** they both apply material, **Then** the physics simulation accounts for all applied material and produces consistent results

---

### User Story 4 - Difficulty Progression System (Priority: P2)

Difficulty progresses through multiple dimensions: larger roof areas, complex geometries, stricter quality requirements, time limits, and reduced material availability. Progression follows a roguelike-inspired curve where early jobs teach mechanics and later jobs require mastery and adaptation.

**Why this priority**: Creates long-term engagement and sense of progression. Early levels teach, late levels challenge.

**Independent Test**: A player experiences clear difficulty scaling from job 1 to job 8+. Early jobs are completable with basic technique; later jobs require precision and planning.

**Acceptance Scenarios**:

1. **Given** a player completes an early job, **When** they attempt the next job, **Then** the difficulty increase is noticeable but not overwhelming
2. **Given** a player reaches advanced jobs, **When** they attempt them, **Then** they require significantly more skill and planning than early jobs
3. **Given** a job has a time limit or material constraint, **When** players see the job briefing, **Then** the constraint is clearly communicated

---

### Edge Cases

- What happens when a player runs out of roofing material before completing a job?
- How does the system handle a player disconnecting during multiplayer gameplay?
- What occurs if a player applies material in ways that violate roof geometry (e.g., material floating in air)?
- Can players undo/redo material placement, or is placement permanent within a job?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a first-person camera perspective where players see their roofing tools and the roof surface they're working on
- **FR-002**: System MUST simulate physics-based roofing material (putty-like behavior) that deforms, spreads, and conforms to roof geometry
- **FR-003**: System MUST track material coverage across roof surfaces and provide objective completion criteria (e.g., % surface covered, minimum thickness)
- **FR-004**: System MUST implement a solo career mode with sequential jobs that unlock progressively with increasing difficulty
- **FR-005**: System MUST provide cooperative multiplayer where 2+ players can join the same job and work simultaneously with synchronized physics
- **FR-006**: System MUST synchronize roofing material state between multiplayer clients within 100-200ms latency tolerance to maintain real-time responsive cooperation
- **FR-007**: System MUST implement progressive difficulty through parameterized job design (roof size, geometry complexity, time limits, material constraints)
- **FR-008**: System MUST persist player career progress including completed jobs, current job state, and unlocked jobs
- **FR-009**: Players MUST be able to restart a job if they fail to meet completion criteria
- **FR-010**: System MUST clearly communicate job objectives, quality requirements, and constraints before job starts
- **FR-011**: System MUST provide visual feedback during roofing material application (e.g., material preview, coverage indicator, quality feedback)
- **FR-012**: System MUST detect job completion based on meeting coverage and quality thresholds within any time/material constraints

### Key Entities

- **RoofingJob**: A single roofing task with defined geometry, difficulty, constraints (time, material, quality requirements), and completion criteria. Has a progression tier (1-N).
- **RoofSurface**: The 3D geometry of a roof where material must be applied. Defines playable area and geometry that affects material physics.
- **RoofingMaterial**: A deformable, physics-simulated substance that players apply. Has mass, elasticity, adhesion properties, and responds to gravity and player input.
- **Career**: Represents a player's progression through jobs, tracking completed jobs, current job, difficulty level, and performance metrics.
- **MultiplayerSession**: Represents an active cooperative game where 2+ players share the same job instance, physics state, and completion criteria.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Players can complete a solo career with at least 10 progressively difficult jobs without significant friction
- **SC-002**: Physics simulation runs at 60+ FPS with responsive material deformation (player action triggers visible response within 100ms)
- **SC-003**: Multiplayer sessions maintain synchronized state within 500ms latency tolerance (material appears in same location for all players)
- **SC-004**: Players can complete cooperative jobs together at the same success rate as solo jobs (multiplayer enables progress, not hinders it)
- **SC-005**: Difficulty progression feels balanced with early jobs completable in 5-10 minutes and later jobs requiring 20-30 minutes
- **SC-006**: 90% of players successfully complete the first 3 career jobs on first or second attempt (early jobs teach mechanics)
- **SC-007**: Player progression data is persisted across sessions with 100% reliability (no lost progress)
- **SC-008**: Roofing material coverage calculation is consistent and objectively verifiable (same material placement achieves same coverage %)

## Assumptions

- **Game Scope**: This specification covers core roofing gameplay (solo career + cooperative multiplayer). Cosmetics, progression rewards, and extensive job variety are out of scope for the initial release.
- **Player Count**: Cooperative multiplayer supports 2-4 players simultaneously. Larger groups are out of scope.
- **Difficulty Progression**: "Like REPO" (roguelike) implies escalating difficulty with clear milestones, not procedural generation. Job variety comes from designed levels, not procedural generation.
- **Physics Accuracy**: Physics simulation prioritizes intuitive gameplay over real-world accuracy. Material behavior is arcade-style, not engineering-accurate.
- **Platform**: Game targets PC (Windows/Mac/Linux) with keyboard + mouse or gamepad input. Mobile platforms are out of scope.
- **Material Application**: Players apply material primarily through direct placement/spreading. Complex roofing techniques (nailing, fastening, etc.) are out of scope; focus is putty-based coverage.
- **Roofing Surfaces**: Roofs are static, pre-defined geometries. Dynamic roof structures or environmental hazards are out of scope for v1.
- **Career Length**: MVP targets 10-15 designed jobs in the progression system. Endless procedural jobs are out of scope.
- **Multiplayer Architecture**: Multiplayer uses client-server synchronization or peer-to-peer replication. Asynchronous/turn-based multiplayer is out of scope.
- **Save/Load Scope**: Career progress is persisted locally (client-side save). Cloud save and cross-device progress sync are out of scope.
