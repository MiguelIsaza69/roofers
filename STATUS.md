# Build Status

**Single source of truth for human-readable progress.** For task-level detail see
`specs/001-roofing-simulator/tasks.md`; for history see `git log`. This file replaces the
former `PHASE_1_STATUS.md`, `PHASE_2_STATUS.md`, and `PROGRESS_SUMMARY.md` (consolidated).

**Last updated**: after the Phase 5 (multiplayer) + documentation commits.

---

## Headline

- **All gameplay code for User Stories 1–4 is written** (solo career, putty physics,
  difficulty progression, co-op multiplayer).
- **64 / 98 tasks complete.** The remaining 34 are almost entirely **Unity Editor / art /
  validation** tasks that cannot be done from source alone.
- **Not compiler-verified.** No Editor was in the loop; expect first-open fixes,
  most likely in the Mirror networking layer.
- **Not yet runnable** until the Editor wiring pass (`EDITOR_SETUP_GUIDE.md`) is done.

---

## Phase status

| Phase | Scope | Code | Remaining |
|-------|-------|------|-----------|
| 1 — Setup | Project structure, asmdefs, scenes, folders | ✅ | T002/T003 (install Mirror, Json.NET), T006 (input asset) |
| 2 — Foundational | Data models, persistence, input, physics, coverage | ✅ | — |
| 3 — Career (US1, P1) | GameManager, CareerManager, catalog, menu/career/briefing/completion UI | ✅ code | T030/T032 (author UI canvases) |
| 4 — Physics (US2, P1) | Material tool, blob factory, MaterialPhysics, HUD, job scene harness | ✅ code | T044 (scene), T047 (roof meshes) |
| 5 — Multiplayer (US3, P2) | Mirror layer: manager, avatar sync, networked material, lobby UI | ✅ code | T054 (avatar prefab), T060 (MP scene) |
| 6 — Difficulty (US4, P2) | JSON job configs + loader, 15 jobs, constraint enforcement | ✅ code+data | T069 (roof mesh variants) |
| 7 — Polish | Validation, profiling, docs, builds | ◑ started | Coverage hot-path optimized (T086) + EditMode unit tests added; validation/profiling/builds (T074–T085, T087–T098) need the Editor |

> Data note: the 15 job configs (Phase 6) were machine-validated — valid JSON, unique
> ids 0–14, monotonic difficulty 1→15. That part *is* verified.

---

## The 35 open tasks, by category

| Category | Tasks | Nature |
|----------|-------|--------|
| Package installs | T002, T003 | Editor — add Mirror + Json.NET |
| Input asset | T006 | Editor — code uses legacy axes; action asset optional |
| Scene / prefab / UI authoring | T030, T032, T044, T054, T060 | Editor — guided by `EDITOR_SETUP_GUIDE.md` |
| Art (roof meshes) | T047, T069 | DCC/Editor — procedural roof stands in |
| Validation scenarios | T074–T082 | Editor — run `quickstart.md` (needs the above) |
| Performance profiling | T083–T087 | Editor — Profiler passes |
| Docs / polish | T088–T093 | Mixed — some codeable, some need the running game |
| Standalone builds | T094–T098 | Editor — build + playtest per platform |

The critical path is the **Editor wiring pass**: installs → author scenes/prefabs/UI →
then validation, profiling, and builds unblock.

---

## What's verified vs. not

| Aspect | State |
|--------|-------|
| Job config JSON (15 files) | ✅ Parsed & validated (ids, monotonic difficulty) |
| C# compilation | ❌ Not verified (no Editor/packages) |
| Gameplay behaviour | ❌ Not run |
| Multiplayer (Mirror API) | ❌ Not run — least-certain code |
| Save/load round-trip | ❌ Not run (logic complete) |

---

## Commit history

```
README onboarding note
Unity Editor wiring guide
Phase 5: Cooperative multiplayer (Mirror)
Phase 6: Difficulty progression
Phase 4: Physics-based roofing mechanics
Phase 3: Solo career progression
Phase 2 (b): Input/camera + physics foundation + career manager
Phase 2 (a): Data models, persistence, utilities
Phase 1: Project setup
```

---

## Next action

Open in Unity 2022 LTS+, install the two packages, confirm a clean compile, then follow
`EDITOR_SETUP_GUIDE.md`. The first Editor open is the highest-value step remaining: it
converts "written" into "verified" and will flush out real compiler/Mirror-API errors.
