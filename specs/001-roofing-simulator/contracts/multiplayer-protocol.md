# Multiplayer Network Protocol Contract

**Version**: 1.0  
**Date**: 2026-06-11  
**Scope**: Real-time cooperative multiplayer for roofing jobs

## Overview

This document defines the network protocol for synchronizing game state between clients in multiplayer roofing sessions. The protocol uses Mirror networking library with server-authoritative physics simulation and client-side prediction.

---

## Core Architecture

**Model**: Server-Authoritative with Client-Side Prediction

- **Server**: Owns all physics simulation, player state, material state
- **Clients**: Predict locally for responsive feedback; server reconciles periodically
- **Tick Rate**: 20 Hz (50ms per update)
- **Target Latency**: 100-200ms for material state consistency

---

## Message Types

### Session Management

#### CreateSessionRequest
```csharp
Message: CreateSessionRequest
From: Client (host)
To: Server
Frequency: Once per session creation

Fields:
- string sessionId (GUID)
- string hostPlayerId
- int jobId (0-14)
- int maxPlayers (2-4)
```

#### SessionCreatedResponse
```csharp
Message: SessionCreatedResponse
From: Server
To: Client
Frequency: Once per session

Fields:
- string sessionId (GUID)
- bool success
- string errorMessage (if success=false)
- int port (for joining)
```

#### JoinSessionRequest
```csharp
Message: JoinSessionRequest
From: Client (joining player)
To: Server
Frequency: Once per player join

Fields:
- string sessionId (GUID)
- string playerId
- string playerName (1-32 chars)
```

#### PlayerJoinedNotification
```csharp
Message: PlayerJoinedNotification
From: Server
To: All Clients
Frequency: Per player join

Fields:
- string playerId
- string playerName
- Vector3 spawnPosition
- int currentPlayerCount
```

### Player Input & State

#### PlayerInputMessage
```csharp
Message: PlayerInputMessage
From: Client
To: Server
Frequency: Every frame (60 Hz, so 3-4 per network tick)

Fields:
- string playerId
- Vector3 avatarPosition
- Vector3 avatarRotation (euler angles)
- bool isApplyingMaterial
- Vector3 toolPosition (raycasting endpoint)
- Vector3 toolForce (magnitude and direction)
- uint inputHash (for server-side verification)

Sent via: Delta compression (only changed fields)
Size: ~32 bytes typical
```

#### PlayerStateUpdate
```csharp
Message: PlayerStateUpdate
From: Server
To: All Clients
Frequency: 20 Hz (50ms)

Fields:
- string playerId
- Vector3 avatarPosition (quantized to 1cm)
- Vector3 avatarRotation (quantized to 1 degree)
- int playerAnimationState (enum)

Sent via: Broadcast to all players in session
```

### Material Synchronization

#### MaterialAppliedMessage
```csharp
Message: MaterialAppliedMessage
From: Client
To: Server
Frequency: Per material application action

Fields:
- string playerId
- Vector3 applicationPoint (world position)
- float materialMass (kg)
- Vector3 applicationForce (direction and magnitude)
- uint timestamp (client frame number for ordering)

Server Processing:
1. Validate against available material budget
2. Apply material in physics simulation
3. Broadcast MaterialStateUpdate to all clients
```

#### MaterialStateUpdate (CRITICAL - Real-time)
```csharp
Message: MaterialStateUpdate
From: Server
To: All Clients
Frequency: 20 Hz (50ms)

Fields:
- uint tickNumber (monotonic counter)
- MaterialBlobUpdate[] blobs (array of changes)

MaterialBlobUpdate:
- string materialBlobId
- Vector3 centroid (quantized to 1cm)
- float totalMass (quantized to 0.1kg)
- MeshDelta meshVertices (only changed vertices)
- bool isNewBlob
- bool isMergedAwayFlag

Compression:
- Quantize positions to 1cm (save 2/3 bandwidth vs float32)
- Send only vertices that changed this tick
- Use run-length encoding for vertex deltas
- Typical size: 256-512 bytes per tick with 2-4 concurrent applicators

Delivery:
- Unreliable UDP (not guaranteed delivery, faster)
- Server sends latest state, clients apply deltas
- If packet lost: next state update reconciles
```

#### CoverageUpdateMessage
```csharp
Message: CoverageUpdateMessage
From: Server
To: All Clients
Frequency: 1 Hz (every 1 second)

Fields:
- uint tickNumber
- float totalCoveragePercent
- float averageThickness (mm)
- float highQualityCoveragePercent
- bool jobCompletedStatus
```

### Session State

#### SessionStateMessage
```csharp
Message: SessionStateMessage
From: Server
To: All Clients
Frequency: 2 Hz (every 500ms)

Fields:
- enum jobState (BRIEFING, IN_PROGRESS, PAUSED, COMPLETED, FAILED)
- TimeSpan elapsedTime
- float? timeRemainingSeconds (null if no limit)
- float materialBudgetRemaining (kg)
- int currentPlayerCount
```

#### SessionAbortMessage
```csharp
Message: SessionAbortMessage
From: Server
To: All Clients
Frequency: Per disconnection/abort

Fields:
- string reason (PLAYER_DISCONNECT, JOB_FAILED, HOST_QUIT, TIMEOUT)
- string? affectedPlayerId (if applicable)
```

---

## Synchronization Strategy

### Client-Side Prediction

**Goal**: Provide responsive feedback without waiting for server round-trip.

```
Player applies material:
1. Client: Apply material locally immediately
   - Show visual feedback at cursor
   - Update local coverage estimate
   - Send PlayerInputMessage to server

2. Server: Receives input after ~50-100ms
   - Validate (material budget, geometry bounds)
   - Apply in authoritative physics
   - Calculate actual material state

3. Server: Broadcasts MaterialStateUpdate
   - All clients receive (20 Hz)
   - Compare client-predicted vs server-actual
   - If difference > threshold: smoothly reconcile

Threshold: 
- Position: 2cm (quantization granularity)
- Thickness: 1mm
- If exceeded: interpolate over next 200ms to match server
```

### Consistency Guarantees

**Strong Consistency**: All players see same material state within 200ms
- Server is source of truth
- Clients predict and reconcile
- Material mesh is deterministic given input sequence

**Ordering**: Material applications are ordered by server-side timestamp
- No race conditions on overlapping material
- Merge logic is deterministic

---

## Bandwidth & Performance

### Estimated Bandwidth per Player

**Uplink (Client → Server)**:
- PlayerInputMessage: 32 bytes @ 20 Hz = 640 bytes/sec
- **Total: ~650 bytes/sec per player**

**Downlink (Server → Client)**:
- PlayerStateUpdate (per other player): 48 bytes × (N-1 players) @ 20 Hz = 960 bytes/sec (N=3)
- MaterialStateUpdate: 256-512 bytes @ 20 Hz = 5.12-10.24 KB/sec
- CoverageUpdateMessage: 32 bytes @ 1 Hz = 32 bytes/sec
- **Total: ~6 KB/sec per client (with 3 players)**

**At 4 players**: ~8 KB/sec per client ✓ (well within typical broadband)

### Server Load

**4 concurrent sessions** (16 players total):
- Physics simulation: 4 × (1 fixed timestep @ 60 Hz) = 240 simulations/sec
- Network serialization: ~80 KB/sec outbound (4 sessions × 8 KB uplink + 6 KB downlink avg)
- CPU: Moderate (2-4 cores sufficient)

---

## Error Handling & Edge Cases

### Player Disconnect
- Server detects timeout after 3 seconds without ping
- Broadcasts PlayerDisconnectedNotification
- Material contributions remain in world (frozen state)
- Remaining players can continue or abandon job

### Network Packet Loss
- MaterialStateUpdate sent unreliably (UDP)
- Loss tolerance: 1 lost packet = ~50ms stale state
- Next packet contains full mesh state → automatic reconciliation
- Coverage updates sent reliably for accuracy

### Bandwidth Throttling
- If client falls behind (buffering messages): skip intermediate MaterialStateUpdates
- Keep only latest state, discard queued older updates
- Prevents cascading latency

---

## Validation & Security

### Input Validation (Server-Side)

All inputs from clients are validated:

```
PlayerInputMessage:
✓ playerId matches authenticated session
✓ toolPosition within job bounds
✓ toolForce magnitude reasonable (0-1000 N max)
✓ avatarPosition within reasonable distance of previous frame

MaterialAppliedMessage:
✓ Player has material budget remaining
✓ Application point within roof surface bounds
✓ Mass value reasonable (0.1-50 kg max)
✓ Timestamp is monotonically increasing
✓ Hash signature matches player's move set
```

### Cheating Prevention

- Server owns physics simulation (prevent speed hack)
- Material budget tracked server-side (prevent infinite material)
- Impossible positions rejected (teleport hack detection)
- Timestamp ordering prevents replay attacks

---

## Testing Contract

### Synchronization Tests

1. **Single Player Test**: Material applies and covers correctly
   - Precondition: Solo job in progress
   - Action: Apply material at specific point
   - Verify: Material appears, coverage updates, physics responds
   - Acceptance: All within 100ms

2. **Two-Player Sync Test**: Both players' material visible to each other
   - Precondition: Two players in same session
   - Action: Player A applies material, Player B observes
   - Verify: Material appears on B's screen within 200ms
   - Acceptance: Positions within 2cm quantization tolerance

3. **Bandwidth Test**: Network usage stays within limits
   - Precondition: 4 players in session, heavy material application
   - Measure: Uplink/downlink bytes per second
   - Verify: < 10 KB/sec per client
   - Acceptance: No bandwidth spikes, consistent rate

4. **Convergence Test**: Client predictions reconcile with server
   - Precondition: Client predicts material placement
   - Verify: If prediction differs from server, smoothly converges
   - Acceptance: Reconciliation complete within 200ms

5. **Disconnect Handling**: Graceful handling of player disconnect
   - Precondition: Player A in active session
   - Action: Player A's network connection fails
   - Verify: Server detects, broadcasts notification within 3s
   - Verify: Remaining players notified, can continue
   - Acceptance: No crash, clean state cleanup

---

## Future Extensibility

- **Replays**: Capture and replay message streams for post-game analysis
- **Spectating**: Add spectator mode (receive-only clients)
- **LAN Mode**: Support local network play without internet
- **Cloud Save**: Extend to persist session state server-side for resumption
