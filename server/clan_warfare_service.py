#!/usr/bin/env python3
"""
MODULE 49 — Clan / Club HQ, Shared Garage & Crew Wars
Create clubs, shared garage, seasonal championships.
"""
import time
import uuid
from typing import Dict, List, Set

class Clan:
    def __init__(self, name: str, founder: str):
        self.id = str(uuid.uuid4())
        self.name = name
        self.founder = founder
        self.members: Set[str] = {founder}
        self.garage: List[str] = []
        self.points = 0
        self.created_at = time.time()

class ClanService:
    def __init__(self):
        self.clans: Dict[str, Clan] = {}
        self.player_clan: Dict[str, str] = {}

    def create_clan(self, name: str, founder: str) -> str:
        if founder in self.player_clan:
            return ""
        c = Clan(name, founder)
        self.clans[c.id] = c
        self.player_clan[founder] = c.id
        return c.id

    def join_clan(self, clan_id: str, player: str) -> bool:
        c = self.clans.get(clan_id)
        if not c or player in self.player_clan:
            return False
        c.members.add(player)
        self.player_clan[player] = clan_id
        return True

    def add_garage_car(self, clan_id: str, vin: str) -> bool:
        c = self.clans.get(clan_id)
        if not c or len(c.garage) >= 30:
            return False
        c.garage.append(vin)
        return True

    def add_points(self, clan_id: str, pts: int):
        if clan_id in self.clans:
            self.clans[clan_id].points += pts

    def leaderboard(self) -> List[dict]:
        return sorted(
            [{"name": c.name, "points": c.points, "members": len(c.members)} for c in self.clans.values()],
            key=lambda x: -x["points"]
        )

if __name__ == "__main__":
    cs = ClanService()
    cid = cs.create_clan("Apex", "p1")
    cs.join_clan(cid, "p2")
    cs.add_garage_car(cid, "VIN999")
    cs.add_points(cid, 50)
    print(cs.leaderboard())
