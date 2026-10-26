# Next-Gen Car Simulator & Live Auction MMO

AAA-grade open-world vehicle simulator featuring custom suspension/tire physics, subsurface-scattering photorealistic character/vehicle LOD rendering, and a server-authoritative real-time auction house.

## Architecture
- `client/` — Unity 6 (C#) Game Client (Physics, Photorealistic Graphics & Networking)
- `server/` — Node.js + WebSocket Authoritative Backend (Live Auction, VIN History, Anti-Cheat Economy)
