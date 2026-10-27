# Ultra Car Simulator — Open World (Unity 6 HDRP)

GTA-scale + futuristic systems. **Modules 1–11 complete.**

---

## Modules 1–11

| # | Content |
|---|--------|
| 1–10 | World, Vehicle, Multiplayer, Police, Economy, VR, Disasters, Ragdoll, Dirt, 120FPS... |
| **11** | **Tesla FSD Autopilot, Flying Car / Rocket Booster, Companion Web Dashboard, Python Neural Bot Racers** |

---

## MODULE 11

| File | Role |
|------|------|
| `AI/AIAutopilotSystem.cs` | FSD toggle (F12), waypoints, obstacles |
| `Vehicle/FlyingCarAndRocketBooster.cs` | Alt+B fly mode, boost |
| `UI/CompanionAppBridge.cs` | http://127.0.0.1:8080 telemetry dashboard |
| `server/neural_bot_racers.py` | AI racers vs players |

---

## Run

**Unity:** Tools → Ultra Car Simulator → 1. Build Complete Playable World & Vehicle Scene

**Multiplayer server:**
```bash
cd server && pip install -r requirements.txt && python multiplayer_server.py
```

**Neural bots (optional):**
```bash
python neural_bot_racers.py
```

**Companion dashboard:** open `http://127.0.0.1:8080` while game is running.

**Status:** Modules 1–11 on GitHub.
