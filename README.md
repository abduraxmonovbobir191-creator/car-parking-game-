# Ultra Car Simulator — Open World (Unity 6 HDRP)

GTA-scale open-world vehicle simulator. Modules 1–10 Part 1 complete.

**Engine:** Unity 6 + HDRP

---

## Modules

| # | Systems |
|---|--------|
| 1–9 | World, Vehicle, Multiplayer, Police, Economy, VR, Disasters, Trucking, OBD... |
| **10.1** | **Ragdoll Impact, Camera Inertia/G-Force, Crowd Reactions, Volumetric Headlights, Shatterable Glass & Debris** |

---

## MODULE 10 Part 1 Files

- `AI/RagdollAndImpactSystem.cs` + `VehicleImpactForwarder`
- `Camera/CinematicCameraInertia.cs`
- `AI/DynamicCrowdReactionSystem.cs`
- `World/VolumetricLightingAndNightAtmosphere.cs`
- `Vehicle/ShatterableGlassAndDebrisSystem.cs`

---

## Run

**Unity:** Tools → Ultra Car Simulator → 1. Build Complete Playable World & Vehicle Scene

**Server:** `cd server && pip install -r requirements.txt && python multiplayer_server.py`

**Status:** Modules 1–10.1 production-ready on GitHub.
