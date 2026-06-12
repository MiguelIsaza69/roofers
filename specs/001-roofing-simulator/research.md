# Research & Technical Decisions: Roofing Simulator

**Date**: 2026-06-11  
**Feature**: [spec.md](spec.md)  
**Phase**: 0 - Research & Technical Foundation

## Overview

This document captures key technical research, decisions, and rationale for the roofing simulator architecture. All [NEEDS CLARIFICATION] markers from the plan have been resolved through analysis of industry standards and project requirements.

---

## Decision 1: Multiplayer Architecture & Networking

**Decision**: Use **Mirror** for real-time multiplayer synchronization

**Rationale**: 
- Mirror is the industry-standard open-source networking library for Unity multiplayer
- Supports both client-server and peer-to-peer modes
- Achieves 100-200ms latency targets on standard internet connections
- Built-in room/lobby system for session management
- Well-documented and actively maintained with strong community support
- Lower learning curve than Netcode for GameObjects (newer alternative)

**Alternatives Considered**:
- **Netcode for GameObjects**: Newer offering from Unity, more modern architecture but steeper learning curve and less mature ecosystem
- **Photon PUN 2**: Commercial offering with cloud hosting; adds unnecessary complexity and cost for this scope
- **Custom networking**: Reinventing the wheel; increases development risk and complexity

**Synchronization Strategy**:
- Mirror's `NetworkBehaviour` for player avatars and core game state
- Custom `NetworkRoofingMaterial` component using Mirror's `NetworkTransform` and state synchronization
- Authoritative server physics simulation to prevent cheating and ensure consistency
- Client-side prediction for responsive material application feedback (100ms latency tolerance)

---

## Decision 2: Physics Simulation Approach

**Decision**: Use **Unity's built-in PhysX with custom deformation layer** for roofing material

**Rationale**:
- PhysX (NVIDIA's physics engine, integrated into Unity) provides robust collision and rigidbody simulation
- Custom deformation system built on top of PhysX using mesh deformation techniques
- Arcade-style material behavior (putty-like) is achievable without engineering-level accuracy
- Allows easy parameterization of material properties (elasticity, adhesion, density) per job
- Avoids overhead of specialized cloth/fluid simulations (Obi Cloth, etc.)

**Material Deformation Architecture**:
- Roofing material represented as dynamic mesh with vertex deformation
- On impact/application: deform mesh vertices based on tool contact and pressure
- Gravity and physics forces applied to material mesh
- Coverage calculated via raycast and mesh overlap detection
- Material adhesion: stickiness parameter prevents unrealistic sliding

**Alternatives Considered**:
- **Obi Cloth**: More accurate cloth simulation; overkill for arcade putty mechanics and adds dependency licensing
- **Nvidia Flex**: Advanced particle system; overly complex for this scope
- **Procedural surface sculpting**: Custom but requires significant research and optimization

---

## Decision 3: Career Progress Persistence

**Decision**: Use **local JSON file storage** with automatic serialization

**Rationale**:
- Simplest approach for local game with no cloud requirements (per assumptions)
- JSON human-readable, easy to debug and support player troubleshooting
- No database setup overhead
- Standard approach for indie games
- Newtonsoft Json.NET provides robust serialization in C#
- Can be easily extended to cloud save later if needed

**Data Structure**:
- Career file: `{PlayerName}_career.json`
- Contents: completed job IDs, current job, difficulty level, performance metrics
- Backup on save: automatic versioning to prevent corruption

**Alternatives Considered**:
- **SQLite**: Overkill for single-player game data; adds complexity without benefit
- **PlayerPrefs**: Unity's built-in storage; limited to small data and not suitable for complex career structures
- **Cloud save**: Out of scope per assumptions; can be added post-MVP

---

## Decision 4: Material Coverage Detection

**Decision**: Use **raycast-based coverage calculation** with mesh sampling

**Rationale**:
- Efficient: raycast from coverage sample points on roof surface
- Reliable: direct distance check between roof surface and applied material
- Parameterizable: adjustable sample density for quality vs performance tradeoff
- Works with any roof geometry without pre-processing

**Algorithm**:
1. Define coverage grid on roof surface (configurable density, e.g., 10cm spacing)
2. Raycast upward from each grid point
3. Check distance to nearest roofing material
4. Points within threshold (e.g., 2cm) count as "covered"
5. Coverage % = covered points / total points
6. Quality check: enforce minimum material thickness (prevents thin skims)

**Alternatives Considered**:
- **Shader-based readback**: GPU-driven; adds complexity and potential performance issues
- **Volume-based calculation**: Requires bounding volume trees; too complex for this scope
- **Mesh intersection tests**: Too expensive for real-time feedback

---

## Decision 5: Job Difficulty Parameterization

**Decision**: Define difficulty through **data-driven job configurations** with scaling parameters

**Rationale**:
- Easy iteration: designers can adjust difficulty without code changes
- Flexible: same roofing job (geometry) can have multiple difficulty variants
- Scalable: new jobs added by creating configuration, not writing new code
- Measurable progression: explicit difficulty tiers (1-15) with clear jumps

**Difficulty Dimensions**:
- **Roof size**: Surface area (small → large)
- **Geometry complexity**: Angles, slopes, irregularities (simple → complex)
- **Coverage requirement**: % of surface that must be covered (60% → 95%)
- **Quality threshold**: Minimum material thickness to be counted (5mm → 20mm)
- **Time limit**: Optional constraint (unlimited → 10 minutes)
- **Material scarcity**: Available material pool (unlimited → 80% of needed)

**Implementation**:
- JobConfiguration scriptable object with all parameters
- Career system loads configurations in sequence
- Difficulty curve defined in progression table

---

## Decision 6: First-Person Camera & Interaction

**Decision**: Use **raycasting for tool interaction** with visual preview feedback

**Rationale**:
- Precise feedback on where material will be applied
- Works with arbitrary roof geometries
- Low performance overhead
- Clear visual communication to player

**Implementation**:
- First-person camera mounted at player "hand" position
- Raycast from camera center each frame
- Show preview mesh at raycast hit point
- On input: apply material at hit location with accumulated pressure

---

## Decision 7: Multiplayer Game Loop

**Decision**: **Server-authoritative physics** with client-side prediction

**Rationale**:
- Server simulates all physics to prevent cheating/exploits
- Clients predict locally for responsive feel (100ms tolerance)
- Server state reconciles predictions periodically
- Ensures all players see consistent material state

**Synchronization Details**:
- Client: Apply material input locally, show feedback immediately
- Server: Receives input, updates physics, broadcasts material state
- Network tick rate: 20 Hz (50ms) for state updates, sufficient for 100-200ms target latency
- Material mesh compression: Send delta updates, not full mesh each tick

---

## Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Physics deformation performance at scale | FPS drops with many clients applying material | Implement LOD for material mesh, batch updates, optimize vertex shader |
| Network bandwidth for material sync | Lag with poor connections | State compression, delta updates, predictive reconciliation |
| Save file corruption | Player progress loss | Backup on save, validation on load, graceful degradation |
| Difficulty balance | Players frustrated or bored | Playtesting gates, telemetry on job completion rates, iterative tuning |

---

## Next Steps

1. **Phase 1**: Generate data model for job configurations, career state, material properties
2. **Phase 1**: Define contracts for job configuration format, save file schema, multiplayer message protocol
3. **Phase 1**: Create quickstart validation guide with test scenarios
4. **Phase 2**: Generate implementation tasks based on data model and architecture
