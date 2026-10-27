# Ultra Car Simulator — Open World Driving & Parking (Unity 6 HDRP)

AAA-style open-world vehicle simulator featuring continental-scale procedural terrain, realistic soft-body damage, deformable mud/sand, full drivetrain simulation, multiplayer backend, police pursuit, mobile 120FPS controls, dealership/livery/trailer systems, and precision parking missions.

**Repository:** `car-parking-game-`  
**Engine:** Unity 6 + High Definition Render Pipeline (HDRP)  
**Target:** 4K / 8K photorealistic fidelity + Mobile 120FPS

---

## Architecture Overview (Modules 1–6)

### MODULE 1 — World Foundation
- FloatingOriginSystem, ProceduralTerrainChunkStreamer, TerrainChunk, BiomeType
- TerrainDeformationManager + TerrainDeformation.compute

### MODULE 2 / 2.5 — Vehicle & Environment
- RealisticVehicleController, DrivetrainSystem, PrecisionParkingSystem
- VehicleEnvironmentBridge, UrbanAndBridgeGenerator, WaterAndWeatherSystem

### MODULE 3 — Character, Tuning & Performance
- DriverCharacterController, VehicleDoorInteraction, VehicleTuningSystem
- GPUInstancedFoliageAndLOD, GarageAndWorldSaveManager

### MODULE 4 — Multiplayer Client, Audio, AI Traffic
- NetworkWorldSyncManager, CarMarketAndTradeSystem, StreetRaceAndTelemetrySystem
- CockpitAndDashboardSystem, VehicleAudioSynthesizer, SmartTrafficSystem

### MODULE 5 — Damage, Winch, Economy, HUD, Scene Builder
- VehicleDamageAndDeformation, OffRoadWinchAndRecoverySystem
- WorldServicesAndEconomySystem, Graphics8KAndHUDController
- OneClickWorldSceneBuilder

### MODULE 6 — Backend, Mobile, Police, Dealership
- **server/multiplayer_server.py** + **server/requirements.txt** — Async Python WebSocket server (matchmaking, 64-bit sync, Avtobozor, races, cash/ownership)
- **NetworkBackendClient.cs** — Unity client bridge to the Python server
- **MobileTouchAndGyroController.cs** — 120FPS touch steering wheel, pedals, gyro
- **PolicePursuitAndRadarSystem.cs** — Speed cameras, fines, 1–5 star pursuit AI
- **DealershipLiveryAndTrailerSystem.cs** — Avtosalon, livery/bodykit, car hauler trailer

---

## Controls

| Action                    | Key / Input              |
|---------------------------|--------------------------|
| Drive                     | WASD / Arrow Keys / Touch|
| Handbrake                 | Space / Mobile button    |
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
| Tuning Panel              | F1                       |
| Winch Panel               | F2                       |
| Toggle Ray Tracing        | F3                       |
| Resolution Scale Cycle    | F4                       |
| Quick Save                | F5                       |
| Quick Load                | F9                       |

---

## How to Run

### Unity Client
1. Open project in **Unity 6** (HDRP).
2. Menu: **Tools → Ultra Car Simulator → 1. Build Complete Playable World & Vehicle Scene**
3. Press Play.

### Multiplayer Server
```bash
cd server
pip install -r requirements.txt
python multiplayer_server.py
```
Server listens on `ws://0.0.0.0:8765`.

Set `NetworkBackendClient.serverUrl` in Unity to your server address.

---

## Project Status

**Modules 1 – 6 complete.**  
Ready for Unity Editor testing, art integration, mobile builds, and live multiplayer sessions.
