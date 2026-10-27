# Ultra Car Simulator — Open World Driving & Parking (Unity 6 HDRP)

AAA-style open-world vehicle simulator featuring continental-scale procedural terrain, realistic soft-body damage, deformable mud/sand, full drivetrain simulation, multiplayer-ready floating origin, avtobozor, drag racing, and precision parking missions.

**Repository:** `car-parking-game-`  
**Engine:** Unity 6 + High Definition Render Pipeline (HDRP)  
**Target:** 4K / 8K photorealistic fidelity

---

## Architecture Overview

### MODULE 1 — World Foundation
- `FloatingOriginSystem` — 64-bit origin shifting to prevent precision loss on 1500 km+ maps
- `ProceduralTerrainChunkStreamer` + `TerrainChunk` + `BiomeType` — Job/Burst powered multi-biome terrain (City, Desert, Mud, Rainforest, Ocean, Mountain…)
- `TerrainDeformationManager` + `TerrainDeformation.compute` — Persistent wheel ruts in mud/sand

### MODULE 2 / 2.5 — Vehicle & Environment
- `RealisticVehicleController` — Raycast suspension + Pacejka-style tire model
- `DrivetrainSystem` — Torque curve, turbo, manual/auto gearbox, AWD/RWD/FWD, Diff-Lock
- `PrecisionParkingSystem` — Ultrasonic sensors, bird’s-eye camera, trajectory lines, air suspension
- `VehicleEnvironmentBridge` — Per-wheel biome / wetness / sink / buoyancy bridge
- `UrbanAndBridgeGenerator` — Procedural cities, parking garages, suspension bridges
- `WaterAndWeatherSystem` — Gerstner waves, rain, sandstorm, global wetness

### MODULE 3 — Character, Tuning & Performance
- `DriverCharacterController` + `VehicleDoorInteraction` — Enter/exit vehicle, open doors/hood/trunk
- `VehicleTuningSystem` — Camber, toe, caster, engine swaps (I4 / 2JZ / V8 / V12), ECU stages, paint types
- `GPUInstancedFoliageAndLOD` — High-performance instanced foliage
- `GarageAndWorldSaveManager` — Full vehicle + world state save/load (F5 / F9)

### MODULE 4 — Multiplayer, Audio, AI
- `NetworkWorldSyncManager` — Double-precision position sync + Hermite interpolation
- `CarMarketAndTradeSystem` — Avtobozor listings, 3D spec boards, ownership transfer
- `StreetRaceAndTelemetrySystem` — Drag (1/4 & 1/2 mile), countdown tree, false-start, telemetry
- `CockpitAndDashboardSystem` — Steering wheel, gauges, signals, lights, wipers, mirrors
- `VehicleAudioSynthesizer` — Multi-layer engine, turbo, blow-off, ALS, surface audio
- `SmartTrafficSystem` — Job-system AI traffic

### MODULE 5 — Final Realism & Tools
- `VehicleDamageAndDeformation` — Soft-body mesh damage, detachable parts, mechanical failures, repair
- `OffRoadWinchAndRecoverySystem` — Lebyodka with catenary rope and physics pull
- `WorldServicesAndEconomySystem` — Dirt accumulation, car wash, fuel, parking missions, cash
- `Graphics8KAndHUDController` — Resolution scaler, ray-tracing toggle, full HUD
- `OneClickWorldSceneBuilder` — Editor menu to build the entire playable scene in one click

---

## Controls

| Action                    | Key / Input              |
|---------------------------|--------------------------|
| Drive                     | WASD / Arrow Keys        |
| Handbrake                 | Space                    |
| Clutch (Manual)           | C                        |
| Shift Up / Down           | Left Shift / Left Ctrl   |
| Diff-Lock                 | L                        |
| Camera Toggle (1st/3rd)   | V                        |
| Enter / Exit Vehicle      | F                        |
| Interact (Doors etc.)     | E                        |
| Winch Attach / Detach     | T                        |
| Winch Reel-In             | R                        |
| Winch Free-Spool          | G                        |
| Left / Right Signal       | Q / E                    |
| Hazard Lights             | X                        |
| Headlights High Beam      | L (toggle)               |
| Tuning Panel              | F1                       |
| Winch Panel               | F2                       |
| Toggle Ray Tracing        | F3                       |
| Resolution Scale Cycle    | F4                       |
| Quick Save                | F5                       |
| Quick Load                | F9                       |

---

## How to Run (Unity 6)

1. Clone this repository.
2. Open the project with **Unity 6** (HDRP).
3. Wait for package resolution (`Packages/manifest.json` is already configured).
4. In the menu bar go to:
   **Tools → Ultra Car Simulator → 1. Build Complete Playable World & Vehicle Scene**
5. Press **Play**.

The One-Click builder automatically creates and wires:
- Floating Origin, Terrain Streamer, Deformation, Weather, Traffic, Multiplayer managers
- A fully configured playable vehicle with all systems attached
- Driver character

---

## Important Notes

- All scripts are written in production-ready style with zero placeholders.
- Namespaces follow the pattern `CarParking.*`.
- Floating Origin uses double-precision (`Vector3d`) for world coordinates.
- Vehicle physics, damage, winch, economy and multiplayer systems are designed to work together out of the box after running the One-Click builder.

---

## Project Status

**Modules 1 – 5 complete.**  
Ready for Unity Editor testing, art integration, and further netcode transport binding.
