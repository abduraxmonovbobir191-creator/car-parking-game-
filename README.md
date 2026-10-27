# Ultra Car Simulator — Open World (Unity 6 HDRP)

**Modules 1–12 complete.** GTA-scale + futuristic + economy ledger systems.

---

## MODULE 12

| System | File |
|--------|------|
| Procedural Megacity | `World/ProceduralMegacityGenerator.cs` |
| Live Weather / Satellite | `World/RealWorldWeatherAndSatelliteSync.cs` (Open-Meteo) |
| Accident / Insurance AI | `Core/InsuranceAndAccidentDeepLearningSystem.cs` |
| Car Token Ledger | `server/car_token_ledger.py` + `Multiplayer/CarTokenLedgerClient.cs` |

---

## Quick Start

```text
Unity: Tools → Ultra Car Simulator → 1. Build Complete Playable World & Vehicle Scene
Server: cd server && pip install -r requirements.txt && python multiplayer_server.py
Ledger: python car_token_ledger.py
Bots:   python neural_bot_racers.py
App:    http://127.0.0.1:8080
```

**Repo:** https://github.com/abduraxmonovbobir191-creator/car-parking-game-
