# Ultra Car Simulator — Open World Driving & Parking (Unity 6 HDRP)

AAA-style open-world vehicle simulator with continental-scale procedural terrain, soft-body damage, deformable mud/sand, full drivetrain, multiplayer backend + voice chat, police pursuit, mobile 120FPS controls, dealership/livery/trailer, trucking business, extreme weather disasters, and cinematic drone/replay.

**Engine:** Unity 6 + HDRP  
**Target:** 4K/8K + Mobile 120FPS

---

## Modules 1–7 Complete

| Module | Systems |
|--------|--------|
| 1 | Floating Origin, Procedural Terrain, Biomes, Deformation Compute |
| 2 / 2.5 | Vehicle Physics, Drivetrain, Parking, Environment Bridge, Urban, Water/Weather |
| 3 | Character, Doors, Tuning, GPU Instancing, Save |
| 4 | Network Sync, Avtobozor, Drag Race, Cockpit, Audio, AI Traffic |
| 5 | Damage, Winch, Economy/Missions, 8K HUD, One-Click Builder |
| 6 | Python Backend, NetworkBackendClient, Mobile Touch/Gyro, Police, Dealership/Trailer |
| 7 | Voice Chat Walkie-Talkie, Tornado/Flood/Earthquake, Trucking Logistics, Cinematic Drone + Replay |

---

## Controls (extra)

| Action | Key |
|--------|-----|
| Push-to-Talk Voice | V |
| Drone FPV Toggle | F7 |
| Replay Last 30s | F8 |
| Winch / Diff-Lock / etc. | See full list in previous README sections |

---

## Run

**Unity:** Tools → Ultra Car Simulator → 1. Build Complete Playable World & Vehicle Scene → Play

**Server:**
```bash
cd server
pip install -r requirements.txt
python multiplayer_server.py
```

---

## Status

**Modules 1–7 production-ready and pushed.**  
Ready for Unity testing, art, mobile builds, and live multiplayer + voice sessions.
