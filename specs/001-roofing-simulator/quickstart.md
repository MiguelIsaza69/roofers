# Quickstart Validation Guide: Roofing Simulator

**Date**: 2026-06-11  
**Feature**: Roofing Simulator with Putty Physics  
**Purpose**: End-to-end validation scenarios proving core gameplay works

---

## Validation Overview

This guide provides step-by-step scenarios to validate that the roofing simulator meets its core requirements. Each scenario focuses on an independent feature slice that can be tested in isolation.

**Prerequisites**:
- Unity 2022 LTS or later
- Build artifact or running game instance
- Test data (job configurations, roof geometries loaded)

---

## Scenario 1: Solo Career Initialization & Job Loading

**Objective**: Verify a player can create a career and access the first job.

**Acceptance Criteria**:
- New career created with valid player name
- Career file saved with correct structure per [career-save-schema.json](contracts/career-save-schema.json)
- First job loads with correct parameters
- Job briefing displays accurately

**Steps**:

1. **Start Game**: Launch the game, arrive at main menu
2. **Create Career**: Click "New Career", enter player name (e.g., "TestPlayer")
3. **Verify Save**: 
   - Confirm career file created: `{GameDataPath}/saves/TestPlayer_career.json`
   - Load and parse JSON (validate against schema)
   - Check: `currentJobIndex == 0`, `unlockedJobIndex == 0`
4. **Enter Career Mode**: Select career to continue
5. **Verify Job Display**:
   - Job name displays: "Smith Residence"
   - Job description visible
   - Briefing screen shows:
     - Coverage requirement: "85% coverage minimum"
     - Surface area: "45 m²"
     - Material budget: "600 kg"
     - No time limit
   - Click "Start Job" → enter gameplay

**Pass Criteria**: Career saves correctly, first job loads with accurate parameters

---

## Scenario 2: Physics-Based Material Application & Coverage

**Objective**: Verify material deformation and coverage detection work intuitively.

**Acceptance Criteria**:
- Material applies at raycast hit point
- Material deforms realistically under physics
- Coverage tracking updates in real-time
- Coverage visualization matches detected coverage

**Steps**:

1. **Start Job**: Load Job 0 (Smith Residence) in gameplay
2. **Apply Material** (Iteration 1):
   - Look at roof surface
   - Click/hold tool button to apply material
   - Observe material appears at cursor point
   - Material should deform, spread, conform to roof shape
   - Verify: Material NOT floating above/below roof (check thickness)
3. **Apply Material** (Iteration 2):
   - Click nearby to apply more material
   - Observe new material merges with existing material
   - Material should bond together (adhesion effect)
   - Spread naturally under gravity on sloped surfaces
4. **Monitor Coverage**:
   - HUD displays "Coverage: XX%"
   - Coverage increases as material is applied
   - First material blob: ~5-10% coverage
   - Continue applying until ~30% (several blobs)
5. **Verify Physics Response**:
   - Apply material on a slope: should slide slightly
   - Apply material from above: should compress and spread
   - Frame rate remains 60+ FPS during application
   - Latency from input to visible deformation: < 100ms

**Pass Criteria**: Material applies, deforms realistically, coverage updates accurately, FPS stable

---

## Scenario 3: Job Completion & Career Progress

**Objective**: Verify job completion criteria work and career progresses.

**Acceptance Criteria**:
- Job completes when coverage meets minimum threshold
- Career saves completion data
- Next job unlocks and becomes accessible
- Difficulty increase is noticeable

**Steps**:

1. **Complete Job 0**:
   - Continue applying material until coverage reaches 85% (job requirement)
   - Observe HUD: "Coverage: 85%" 
   - System should detect completion
   - Completion screen appears with stats:
     - Time: "XX minutes"
     - Coverage: "85%"
     - Quality: "ADEQUATE" (based on thickness)
     - Material used: "XXX kg"
   - Click "Continue"
2. **Verify Career Update**:
   - Career screen shows Job 0 marked "Completed"
   - Job 1 now unlocked (button enabled, not grayed out)
   - Verify save file updated:
     - `totalJobsCompleted == 1`
     - `currentJobIndex == 1`
     - `unlockedJobIndex == 1`
     - `jobCompletions` array has one entry
3. **Load Job 1** ("Small Commercial"):
   - Verify difficulty increase:
     - Surface area larger (55 m² vs 45 m²)
     - Coverage requirement higher (90% vs 85%)
     - Quality stricter (HIGH vs STANDARD)
     - Material constraint tighter (550 kg vs 600 kg)
   - Job briefing reflects increased difficulty

**Pass Criteria**: Job completes correctly, career progresses, next job unlocks with increased difficulty

---

## Scenario 4: Multiplayer Session Initialization

**Objective**: Verify two players can join same job and see each other.

**Acceptance Criteria**:
- Host creates multiplayer session
- Second player successfully joins
- Both players' avatars visible to each other
- Game state synchronized within acceptable latency

**Setup**:
- Two game instances running (same machine or networked)
- Mirror networking configured and operational
- Test with localhost first, then network test

**Steps**:

1. **Host Creates Session**:
   - Player A: Open Career, select Job 0
   - Click "Play Multiplayer"
   - Click "Create Session"
   - Observe: Session created, session code displayed (e.g., "ABC123")
   - Network status shows "Hosting" or "Server Started"

2. **Client Joins Session**:
   - Player B: Open Game, Career
   - Click "Play Multiplayer"
   - Click "Join Session"
   - Enter session code "ABC123"
   - Click "Join"
   - Network status transitions to "Connected"

3. **Verify Avatar Synchronization**:
   - Player A sees Player B's avatar in the game world
   - Player A sees Player B's name label above avatar
   - Player B sees Player A's avatar
   - Player B sees Player A's name label
   - Move around the map:
     - Player A moves → visible on Player B's screen within 200ms
     - Player B moves → visible on Player A's screen within 200ms

4. **Monitor Synchronization Quality**:
   - Network latency display (if visible): 100-200ms target
   - Avatar movement smooth (no teleporting)
   - No one-way disconnection or ghost players

**Pass Criteria**: Session creation succeeds, joining works, avatars visible and synchronized

---

## Scenario 5: Cooperative Material Application

**Objective**: Verify two players can apply material simultaneously with consistent physics state.

**Acceptance Criteria**:
- Both players' material applications visible to each other
- Physics state converges (all players see same material result)
- Coverage updates reflect combined effort
- No conflicting material states

**Prerequisites**: Scenario 4 complete (two players in session)

**Steps**:

1. **Synchronized Application**:
   - Player A applies material at location X (roof left side)
   - Simultaneously, Player B applies material at location Y (roof right side)
   - Observe both locations:
     - Player A sees their material appear immediately (client prediction)
     - Player A sees Player B's material appear within 200ms (server update)
     - Player B sees their material appear immediately
     - Player B sees Player A's material appear within 200ms

2. **Verify Physics Consistency**:
   - Both players see material at same positions (within 2cm quantization)
   - Material spreading/deformation is identical for both players
   - Coverage % matches for both players (within 1%)
   - No material appears in different places for different players

3. **Adjacent Material Merging**:
   - Player A applies material at point X
   - Player B applies material at point X+0.5m (close to A's material)
   - Observe merging behavior:
     - Material blobs bond together
     - Both players see merged result within 200ms
     - No conflicting states where one sees merged, other sees separate

4. **Coverage Convergence**:
   - Both players see identical coverage % on their HUD
   - Continue applying until job completable (90% coverage for Job 1)
   - Both players' HUD show "90% Coverage"
   - One player completes the job:
     - Both players see completion screen
     - Both players see same stats (coverage, time, quality)

**Pass Criteria**: Material synchronized between players, physics consistent, coverage matches

---

## Scenario 6: Multiplayer Disconnect Handling

**Objective**: Verify graceful handling when a player disconnects.

**Acceptance Criteria**:
- Disconnecting player's material remains in world
- Remaining player can continue or abandon job
- No crash or undefined state
- Clear UI feedback

**Prerequisites**: Scenario 4 complete (two players in session)

**Steps**:

1. **Establish Session**:
   - Two players in active job (e.g., Job 1, 30% coverage)
   - Both players have applied some material

2. **Player Disconnect** (Simulate):
   - Player B: Force disconnect (kill network, close app, etc.)
   - Observe Player A's client:
     - Player B's avatar remains visible (frozen state) or disappears gracefully
     - UI displays notification: "Player B disconnected"
     - Game state does NOT crash or freeze
     - Coverage from both players' contributions still reflected

3. **Continue or Abort**:
   - Player A chooses: Continue job or Abandon
   - If Continue: Can complete job solo with all prior material
   - If Abandon: Returns to career screen with no progress saved
   - No errors, clean exit

4. **Host Disconnect**:
   - Player A (host) disconnects
   - Observe Player B's client:
     - Gets notification: "Host disconnected"
     - Game ends, returns to main menu
     - Session terminates cleanly

**Pass Criteria**: Disconnect handled gracefully, no crashes, clear feedback

---

## Scenario 7: Career Persistence Across Sessions

**Objective**: Verify player progress is saved and restored correctly.

**Acceptance Criteria**:
- Career data persists to disk
- Closing and reopening game restores career
- All completion data intact
- No progress loss

**Steps**:

1. **Build Career Progress**:
   - Complete Job 0 (coverage 85%, time 10 min, quality GOOD)
   - Complete Job 1 (coverage 90%, time 12 min, quality ADEQUATE)
   - Do NOT complete Job 2 (leave at 50% coverage)
   - Exit game to main menu

2. **Verify Save File**:
   - Check filesystem: `{GameDataPath}/saves/TestPlayer_career.json` exists
   - Load JSON, validate against [career-save-schema.json](contracts/career-save-schema.json)
   - Verify contents:
     - `totalJobsCompleted == 2`
     - `currentJobIndex == 2`
     - `unlockedJobIndex == 2`
     - `jobCompletions` has 2 entries for Jobs 0 and 1
     - Completion timestamps and stats match

3. **Close Game**:
   - Fully close game application
   - Remove game from memory (restart OS if testing robustness)

4. **Reopen Game**:
   - Launch game
   - Go to Career selection
   - Load "TestPlayer" career
   - Verify Career screen shows:
     - Jobs 0-1 marked "Completed"
     - Job 2 available but not completed
     - Current job: Job 2
     - All stats and metrics from step 2 restored

5. **Resume Job 2**:
   - Open Job 2
   - Resume previous attempt (if supported)
   - Verify material from previous session is present
   - Coverage still at ~50%

**Pass Criteria**: All progress saved and restored, no data loss

---

## Scenario 8: Difficulty Progression Validation

**Objective**: Verify difficulty scaling is noticeable and balanced across progression.

**Acceptance Criteria**:
- Early jobs (1-3) completable in 5-10 minutes
- Mid jobs (5-8) require 15-20 minutes
- Late jobs (10+) require 25-30 minutes
- Difficulty increase smooth, not abrupt

**Steps**:

1. **Job 0 Baseline**:
   - Complete with normal play
   - Measure time: target 5-10 minutes for experienced player
   - Coverage required: 85%

2. **Job 3 Measurement**:
   - Load Job 3 (after completing 0-2)
   - Note requirements:
     - Surface area: larger than Job 0
     - Coverage: 88% or higher
     - Quality: HIGH (min 10mm thickness)
     - Material: constrained (80% of theoretical minimum)
   - Attempt to complete
   - Measure time: target 10-15 minutes
   - Difficulty should feel noticeably harder than Job 0, but not overwhelming

3. **Job 8 Measurement**:
   - (Assume player has completed 0-7)
   - Load Job 8
   - Note requirements:
     - Large complex geometry
     - Coverage: 92%+
     - Quality: PRISTINE (min 15mm thickness)
     - Material: tight constraint
   - Attempt to complete (may fail first attempt)
   - Measure time on successful attempt: 20-30 minutes
   - Difficulty should require mastery: precision, planning, careful material management

4. **Progression Feel**:
   - Compare Jobs 0, 4, 8
   - Verify smooth ramp, not cliff/drop in difficulty
   - Each job incrementally harder than previous
   - No job feels arbitrary or disconnected

**Pass Criteria**: Difficulty scales reasonably, progression feels balanced

---

## Regression Test Checklist

Run these quick checks after each build/deployment:

- [ ] Game launches without crash
- [ ] Main menu displays, buttons clickable
- [ ] New career creates successfully
- [ ] First job loads, briefing shows correct requirements
- [ ] Material applies and deforms on keypress
- [ ] HUD coverage % updates when material applied
- [ ] Completion detected when 85%+ coverage achieved
- [ ] Next job unlocks after completion
- [ ] Career file saves and loads without corruption
- [ ] Multiplayer session creation works (if networking enabled)
- [ ] Multiplayer join works and avatars visible
- [ ] Disconnect handled gracefully (no crash)
- [ ] FPS remains 60+ during heavy material application

---

## Performance Baselines

Reference targets from [plan.md](plan.md) success criteria:

| Metric | Target | Validation |
|--------|--------|-----------|
| FPS during gameplay | 60+ | Monitor with profiler during material application |
| Input latency | <100ms | Measure time from mouse click to visible deformation |
| Multiplayer sync latency | 100-200ms | Monitor network tick, measure state update arrival |
| Coverage update frequency | Real-time | HUD updates each frame without lag |
| Material merge detection | < 500ms | Time from placement to merge with adjacent material |
| Job completion detection | Immediate | Completion screen appears within 1 frame of 85% coverage |
| Career save time | < 500ms | Measure file write time after job completion |
| Career load time | < 1s | Measure from career selection to gameplay start |

---

## Known Issues & Workarounds

(To be filled post-implementation with any edge cases discovered)

---

## Next Steps

1. Run all scenarios end-to-end
2. Document any failures with reproduction steps
3. Update data models or implementation based on findings
4. Re-run until all scenarios pass
5. Proceed to full testing and optimization phase
