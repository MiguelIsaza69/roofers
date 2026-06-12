# Data Model: Roofing Simulator

**Date**: 2026-06-11  
**Feature**: [spec.md](spec.md)  
**Phase**: 1 - Design & Data Model

## Overview

This document defines the core data entities, their structure, relationships, and validation rules for the roofing simulator game. All entities are designed to support the game mechanics, progression system, and multiplayer synchronization.

---

## Core Entities

### 1. Career

Represents a player's progression through the roofing simulator career mode.

```csharp
Career {
  id: string (unique identifier)
  playerName: string (1-32 chars)
  createdDate: DateTime (UTC)
  lastModifiedDate: DateTime (UTC)
  totalJobsCompleted: int (0 to 15)
  currentJobIndex: int (0 to 14, index into job progression)
  unlockedJobIndex: int (highest accessible job index)
  totalPlayTime: TimeSpan (cumulative)
  performanceMetrics: PerformanceMetrics
  jobCompletions: JobCompletion[] (history of completed jobs)
}

PerformanceMetrics {
  totalCoverageArea: float (cumulative m²)
  averageCompletionTime: TimeSpan (avg time to complete jobs)
  totalMaterialUsed: float (kg, cumulative)
  bestCompletionTime: TimeSpan (fastest job completion)
  highestDifficultyCompleted: int (max job index completed)
}

JobCompletion {
  jobId: int (0-14)
  completedDate: DateTime
  timeToComplete: TimeSpan
  materialUsed: float
  finalCoveragePercent: float (0-100)
  attemptCount: int (how many tries)
  qualityRating: QualityRating (EXCELLENT, GOOD, ADEQUATE, POOR)
}

enum QualityRating {
  EXCELLENT = 4,    // >95% coverage, high quality
  GOOD = 3,         // 85-95% coverage
  ADEQUATE = 2,     // 70-85% coverage
  POOR = 1          // <70% coverage (technically passing but low quality)
}
```

**Validation Rules**:
- playerName must be non-empty and unique per save file
- currentJobIndex <= unlockedJobIndex (can't play locked jobs)
- jobCompletions must be in chronological order
- totalJobsCompleted must match length of jobCompletions where completion is successful
- timeToComplete must be positive
- finalCoveragePercent must be between job's minCoverage and 100

---

### 2. RoofingJob

Represents a single roofing task with defined geometry, constraints, and difficulty parameters.

```csharp
RoofingJob {
  id: int (0-14, progression order)
  name: string (e.g., "Smith Residence")
  description: string (brief job description)
  difficulty: int (1-15, relative difficulty)
  roofGeometry: RoofGeometry (3D geometry reference)
  
  // Completion criteria
  minCoveragePercent: float (60-95%, minimum surface coverage required)
  minQuality: QualityThreshold (STANDARD, HIGH, PRISTINE)
  surfaceArea: float (m², total roof area)
  
  // Constraints
  materialBudget: float (kg, total available material, null = unlimited)
  timeLimitSeconds: int (null = unlimited)
  
  // Progression
  unlocksNextJob: bool (whether completing this job unlocks next)
  difficulty_scalingFactors: DifficultyScaling
}

enum QualityThreshold {
  STANDARD = 1,  // min 5mm thickness
  HIGH = 2,      // min 10mm thickness
  PRISTINE = 3   // min 15mm thickness
}

RoofGeometry {
  id: string (e.g., "residential_small_v1")
  meshPath: string (path to 3D model asset)
  surfaceArea: float (pre-calculated m²)
  complexity: float (0.0-1.0, relative geometric complexity)
  description: string
}

DifficultyScaling {
  sizeMultiplier: float (1.0 = baseline; affects surface area)
  materialAvailability: float (0.5-1.0; 0.8 = 80% of theoretical minimum)
  requiredQuality: QualityThreshold (quality enforcement)
  timeLimit: int (seconds; null = unlimited)
  geometryComplexity: float (adds slopes, obstacles, etc.)
}
```

**Validation Rules**:
- id must be unique (0-14)
- difficulty must be 1-15
- minCoveragePercent must be 60-99%
- minQuality must be valid QualityThreshold
- surfaceArea must be positive
- materialBudget, if set, must be >= (surfaceArea / 10) (minimum viable material)
- timeLimitSeconds, if set, must be > 0
- sizeMultiplier must be > 0
- materialAvailability must be 0.5-1.0

**State During Gameplay**:
```csharp
RoofingJobInstance {
  job: RoofingJob (the job definition)
  currentState: JobState
  startTime: DateTime
  elapsedTime: TimeSpan (updated each frame)
  materialApplied: RoofingMaterial[] (all material applied this session)
  currentCoveragePercent: float (updated in real-time)
  currentAverageThickness: float (mm, updated in real-time)
}

enum JobState {
  BRIEFING,         // Player reviewing job objectives
  IN_PROGRESS,      // Active gameplay
  PAUSED,           // Job paused
  COMPLETED,        // Job completed, criteria met
  FAILED,           // Job failed (ran out of time/material, coverage insufficient)
  ABANDONED         // Player quit without completing
}
```

---

### 3. RoofingMaterial

Represents the deformable putty-like material that players apply to roofs.

```csharp
RoofingMaterial {
  id: GUID (unique identifier)
  position: Vector3 (world position of material center)
  deformationMesh: Mesh (dynamic mesh representing material shape)
  totalMass: float (kg, total mass of this blob)
  elasticity: float (0.0-1.0, bounciness)
  adhesion: float (0.0-1.0, stickiness to surfaces)
  density: float (kg/m³, used to calculate volume)
  
  // Physics simulation state
  velocity: Vector3 (current movement)
  angularVelocity: Vector3 (rotation)
  isSimulating: bool (whether physics are active)
  lastModified: DateTime
  
  // Coverage contribution
  coverageArea: float (m², surface area touching roof)
  avgThickness: float (mm, average thickness on roof)
}

MaterialProperties {
  // Material physics parameters (per job tuning)
  baseElasticity: float (0.3 default)
  baseAdhesion: float (0.7 default)
  baseDensity: float (1200 kg/m³, typical for rubber)
  spreadingRate: float (0.0-1.0, how easily it spreads)
  gravityInfluence: float (0.0-1.0, gravity multiplier)
}
```

**Validation Rules**:
- position must be within job's roof bounds
- totalMass must be positive
- elasticity, adhesion, spreadingRate, gravityInfluence must be 0.0-1.0
- density must be positive and realistic (400-2000 kg/m³)
- avgThickness must be 0-50mm (capped at material depth)
- coverageArea must be <= roofSurface.area

**Deformation Mechanics**:
```
// When player applies material:
1. Create new RoofingMaterial blob at raycast hit point
2. Add force/pressure based on player input intensity
3. Physics engine deforms mesh vertices over time
4. Adjacent material blobs merge if they touch (adhesion check)
5. Gravity pulls material downward on slopes
6. Coverage recalculated every frame

// Spreading behavior (pseudo-code):
OnPhysicsUpdate() {
  // Move vertices based on physics
  for each vertex:
    newPos = currentPos + velocity * deltaTime
    newPos += gravity * gravityInfluence * deltaTime
    
    // Constrain to roof surface (prevent floating)
    if newPos below roof: project to surface
    
  // Adhesion check: merge with nearby material if close
  for each nearby material blob:
    distance = Vector3.Distance(this.center, other.center)
    if distance < adhesionThreshold and adhesion > 0.5:
      merge into single blob
      
  // Spreadability: allow material to thin and spread
  avgThickness *= spreadingFactor
  coverageArea *= (1.0 + spreadingRate * deltaTime)
}
```

---

### 4. RoofSurface

Represents the roof geometry where material is applied and coverage is tracked.

```csharp
RoofSurface {
  id: string (e.g., "residential_small_v1_surface")
  mesh: Mesh (the 3D roof geometry)
  totalArea: float (m²)
  
  // Coverage tracking (dynamically updated)
  coverageSamples: CoverageSample[] (grid of sample points)
  totalCovered: float (m², sum of covered area)
  coveragePercent: float (0-100%, totalCovered / totalArea)
  
  // Material thickness tracking
  materialDepthAtPoint: Dictionary<Vector3, float> (sample point -> thickness mm)
  averageThickness: float (mm, across all covered points)
  
  // Quality tracking
  highQualityCoveragePercent: float (% with thickness > quality threshold)
}

CoverageSample {
  position: Vector3 (point on roof surface)
  isCovered: bool (is covered by material)
  currentThickness: float (mm, detected thickness above this sample)
  lastUpdated: DateTime
}
```

**Coverage Calculation Algorithm**:
```
UpdateCoverage():
  for each sample point on roof:
    // Raycast upward to find nearest material
    ray = Ray(samplePosition, Vector3.up)
    hits = Physics.RaycastAll(ray)
    
    // Find distance to roofing material (ignore other objects)
    materialDistance = float.MaxValue
    for each hit:
      if hit.collider.tag == "RoofingMaterial":
        materialDistance = min(materialDistance, hit.distance)
    
    // Check if covered (within threshold)
    if materialDistance < coverageThreshold (2cm):
      sample.isCovered = true
      sample.currentThickness = materialDistance * depthFactor
    else:
      sample.isCovered = false
      sample.currentThickness = 0
  
  // Aggregate statistics
  totalCovered = count of covered samples * (sampleSpacing²)
  coveragePercent = totalCovered / totalArea
  averageThickness = mean(materialDepthAtPoint)
```

**Validation Rules**:
- totalArea must be positive
- coveragePercent must be 0-100%
- averageThickness must be 0-50mm
- materialDepthAtPoint values must be 0-50mm
- highQualityCoveragePercent must be 0-100% and <= coveragePercent

---

### 5. MultiplayerSession

Represents an active multiplayer game where 2-4 players work together on the same job.

```csharp
MultiplayerSession {
  id: GUID (session identifier)
  hostPlayerId: string (creator/host)
  job: RoofingJob (the job being played)
  gameState: JobState (BRIEFING, IN_PROGRESS, COMPLETED, FAILED)
  
  // Players
  players: PlayerInSession[]
  maxPlayers: int (2-4)
  
  // Shared state
  sharedRoofSurface: RoofSurface (shared coverage tracking)
  sharedMaterials: RoofingMaterial[] (all material applied by all players)
  startTime: DateTime
  elapsedTime: TimeSpan
  
  // Network synchronization
  lastNetworkUpdate: DateTime
  networkTickRate: int (20 Hz, updates per second)
}

PlayerInSession {
  playerId: string
  playerName: string
  avatarPosition: Vector3
  isAlive: bool (has not disconnected)
  inputState: PlayerInputState
  lastUpdate: DateTime
  
  // Contribution tracking
  materialAppliedByPlayer: float (kg, this player's contribution)
  playerCoverageContribution: float (%, estimated)
}

enum PlayerInputState {
  IDLE,
  APPLYING_MATERIAL,
  TOOL_ACTIVE,
  NAVIGATING
}
```

**Synchronization Protocol**:
- Every 50ms (20 Hz tick): broadcast material state changes
- Use delta encoding: only send changed material vertices, not full mesh
- Compress material positions with quantization (reduce precision to 1cm)
- Use server-authoritative physics: server simulates, clients predict

**Validation Rules**:
- maxPlayers must be 2-4
- players array must have 1-maxPlayers entries
- All PlayerInSession entries must reference valid players
- sharedMaterials must be consistent across all clients (server-authoritative)
- elapsedTime must be monotonically increasing

---

## Data Relationships

```
Career
  ├─ contains many JobCompletion
  └─ references latest RoofingJobInstance

RoofingJobInstance
  ├─ references RoofingJob (definition)
  ├─ contains RoofSurface (state during gameplay)
  ├─ contains many RoofingMaterial
  └─ may be wrapped in MultiplayerSession (if multiplayer)

MultiplayerSession
  ├─ references RoofingJob
  ├─ contains many PlayerInSession
  └─ contains shared RoofSurface & RoofingMaterial[]

RoofingJob
  ├─ references RoofGeometry
  ├─ contains DifficultyScaling parameters
  └─ specifies MaterialProperties
```

---

## Persistence Schema

### Career Save File Format

```json
{
  "version": "1.0",
  "career": {
    "id": "player-abc123",
    "playerName": "John Roofer",
    "createdDate": "2026-06-11T10:30:00Z",
    "lastModifiedDate": "2026-06-11T12:45:00Z",
    "totalJobsCompleted": 3,
    "currentJobIndex": 3,
    "unlockedJobIndex": 4,
    "totalPlayTime": "PT2H30M",
    "performanceMetrics": {
      "totalCoverageArea": 450.5,
      "averageCompletionTime": "PT12M30S",
      "totalMaterialUsed": 2400.0,
      "bestCompletionTime": "PT8M45S",
      "highestDifficultyCompleted": 4
    },
    "jobCompletions": [
      {
        "jobId": 0,
        "completedDate": "2026-06-11T10:45:00Z",
        "timeToComplete": "PT10M30S",
        "materialUsed": 720.0,
        "finalCoveragePercent": 92.5,
        "attemptCount": 1,
        "qualityRating": "GOOD"
      }
      // ... more completions
    ]
  }
}
```

---

## Validation & Constraints

**Consistency Checks**:
- Career.currentJobIndex must be <= unlockedJobIndex
- RoofingJobInstance.currentCoveragePercent must match calculated value from RoofSurface
- MultiplayerSession.sharedMaterials state must be consistent across all clients
- JobCompletion entries must be ordered by completedDate

**Invariants**:
- A Career can only have one active RoofingJobInstance at a time
- A RoofingJobInstance is either Solo or part of one MultiplayerSession, never both
- All RoofingMaterial blobs must be within the RoofSurface bounds
- Material coverage sum cannot exceed roof surface area (geometric constraint)

---

## Next Steps

1. Define contract formats for job configuration files, save schemas, network messages
2. Create quickstart validation guide with data loading scenarios
3. Generate implementation tasks for data serialization, networking, physics
