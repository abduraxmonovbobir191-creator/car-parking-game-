# Ultra Car Simulator — Open World (Unity 6 HDRP)

GTA-scale open-world vehicle simulator. **Modules 1–10 complete.**

**Engine:** Unity 6 + HDRP  
**Repo:** car-parking-game-

---

## Full Module List

| Module | Content |
|--------|--------|
| 1 | Floating Origin, Procedural Terrain, Biomes, Deformation |
| 2 / 2.5 | Vehicle Physics, Drivetrain, Parking, Weather, Urban |
| 3 | Character, Tuning, GPU Instancing, Save |
| 4 | Network Sync, Avtobozor, Drag Race, Cockpit, Audio, Traffic |
| 5 | Damage, Winch, Economy, 8K HUD, One-Click Builder |
| 6 | Python Backend, Mobile/Gyro, Police, Dealership/Trailer |
| 7 | Voice Chat, Disasters, Trucking, Drone + Replay |
| 8 | Track Editor, VR, Radio, Photo Mode |
| 9 | Mechanical Failures, Pedestrians, Hydrodynamics, OBD/Bank/Insurance |
| **10.1** | Ragdoll, Camera Inertia, Crowd Reactions, Volumetric Lights, Shatter Glass |
| **10.2** | Audio Occlusion, Dirt Mask, Diverse NPCs, Easter Eggs, Adaptive 120FPS |

---

## Run

**Unity:** `Tools → Ultra Car Simulator → 1. Build Complete Playable World & Vehicle Scene`

**Server:**
```bash
cd server
pip install -r requirements.txt
python multiplayer_server.py
```

**Status:** Full production-ready framework on GitHub. Ready for art, netcode transport binding, and device testing.
