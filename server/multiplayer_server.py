#!/usr/bin/env python3
"""
Ultra Car Simulator — Dedicated Multiplayer Backend
Asyncio + WebSockets + SQLite
Handles: matchmaking, 64-bit position sync, Avtobozor, ownership/cash, drag race, voice chat relay
"""

import asyncio
import json
import sqlite3
import time
import uuid
from pathlib import Path
from typing import Dict, Set, Optional

import websockets
from websockets.server import WebSocketServerProtocol

DB_PATH = Path(__file__).parent / "multiplayer.db"
HOST = "0.0.0.0"
PORT = 8765

def init_db() -> None:
    conn = sqlite3.connect(DB_PATH)
    cur = conn.cursor()
    cur.executescript("""
    CREATE TABLE IF NOT EXISTS players (
        net_id TEXT PRIMARY KEY,
        name TEXT NOT NULL,
        cash REAL DEFAULT 15000,
        last_seen REAL
    );
    CREATE TABLE IF NOT EXISTS vehicles (
        vehicle_id TEXT PRIMARY KEY,
        owner_net_id TEXT NOT NULL,
        data_json TEXT NOT NULL,
        FOREIGN KEY(owner_net_id) REFERENCES players(net_id)
    );
    CREATE TABLE IF NOT EXISTS market_listings (
        listing_id TEXT PRIMARY KEY,
        seller_net_id TEXT NOT NULL,
        vehicle_id TEXT NOT NULL,
        price REAL NOT NULL,
        specs_json TEXT,
        created_at REAL,
        FOREIGN KEY(seller_net_id) REFERENCES players(net_id)
    );
    """)
    conn.commit()
    conn.close()

def db_execute(query: str, params: tuple = (), fetch: bool = False):
    conn = sqlite3.connect(DB_PATH)
    cur = conn.cursor()
    cur.execute(query, params)
    result = cur.fetchall() if fetch else None
    conn.commit()
    conn.close()
    return result

class Client:
    def __init__(self, ws: WebSocketServerProtocol, net_id: str, name: str):
        self.ws = ws
        self.net_id = net_id
        self.name = name
        self.room: Optional[str] = None
        self.pos = {"x": 0.0, "y": 0.0, "z": 0.0}
        self.rot = {"x": 0.0, "y": 0.0, "z": 0.0, "w": 1.0}
        self.origin_offset = {"x": 0.0, "y": 0.0, "z": 0.0}
        self.rpm = 0.0
        self.gear = 1
        self.steer = 0.0

clients: Dict[str, Client] = {}
rooms: Dict[str, Set[str]] = {}
race_sessions: Dict[str, dict] = {}

async def send(ws: WebSocketServerProtocol, data: dict) -> None:
    try:
        await ws.send(json.dumps(data))
    except Exception:
        pass

async def broadcast_room(room_id: str, data: dict, exclude: Optional[str] = None) -> None:
    if room_id not in rooms:
        return
    for net_id in list(rooms[room_id]):
        if net_id == exclude:
            continue
        client = clients.get(net_id)
        if client:
            await send(client.ws, data)

def ensure_player(net_id: str, name: str) -> None:
    rows = db_execute("SELECT net_id FROM players WHERE net_id = ?", (net_id,), fetch=True)
    if not rows:
        db_execute(
            "INSERT INTO players (net_id, name, cash, last_seen) VALUES (?, ?, 15000, ?)",
            (net_id, name, time.time()),
        )
    else:
        db_execute("UPDATE players SET last_seen = ?, name = ? WHERE net_id = ?", (time.time(), name, net_id))

async def handle_join(client: Client, msg: dict) -> None:
    name = msg.get("name", "Driver")
    room_id = msg.get("room", "world_1")
    client.name = name
    client.room = room_id
    ensure_player(client.net_id, name)

    if room_id not in rooms:
        rooms[room_id] = set()
    rooms[room_id].add(client.net_id)

    others = []
    for nid in rooms[room_id]:
        if nid == client.net_id:
            continue
        c = clients.get(nid)
        if c:
            others.append({
                "netId": c.net_id,
                "name": c.name,
                "pos": c.pos,
                "rot": c.rot,
                "originOffset": c.origin_offset,
            })

    await send(client.ws, {"type": "joined", "netId": client.net_id, "room": room_id, "players": others})
    await broadcast_room(room_id, {
        "type": "player_joined",
        "netId": client.net_id,
        "name": name,
        "pos": client.pos,
        "rot": client.rot,
    }, exclude=client.net_id)

async def handle_state(client: Client, msg: dict) -> None:
    client.pos = msg.get("pos", client.pos)
    client.rot = msg.get("rot", client.rot)
    client.origin_offset = msg.get("originOffset", client.origin_offset)
    client.rpm = float(msg.get("rpm", 0))
    client.gear = int(msg.get("gear", 1))
    client.steer = float(msg.get("steer", 0))

    if client.room:
        await broadcast_room(client.room, {
            "type": "state",
            "netId": client.net_id,
            "pos": client.pos,
            "rot": client.rot,
            "originOffset": client.origin_offset,
            "rpm": client.rpm,
            "gear": client.gear,
            "steer": client.steer,
            "t": time.time(),
        }, exclude=client.net_id)

async def handle_voice(client: Client, msg: dict) -> None:
    """Relay voice packets to other players in the same room (spatial handled client-side)."""
    if not client.room:
        return
    payload = {
        "type": "voice",
        "netId": client.net_id,
        "pcm": msg.get("pcm", ""),
        "pos": client.pos,
    }
    await broadcast_room(client.room, payload, exclude=client.net_id)

async def handle_market_list(client: Client, msg: dict) -> None:
    rows = db_execute(
        "SELECT listing_id, seller_net_id, vehicle_id, price, specs_json FROM market_listings",
        fetch=True,
    )
    listings = []
    for r in rows or []:
        listings.append({
            "listingId": r[0],
            "sellerNetId": r[1],
            "vehicleId": r[2],
            "price": r[3],
            "specs": json.loads(r[4]) if r[4] else {},
        })
    await send(client.ws, {"type": "market_list", "listings": listings})

async def handle_market_create(client: Client, msg: dict) -> None:
    listing_id = str(uuid.uuid4())
    vehicle_id = msg.get("vehicleId", str(uuid.uuid4()))
    price = float(msg.get("price", 0))
    specs = msg.get("specs", {})

    db_execute(
        "INSERT INTO market_listings (listing_id, seller_net_id, vehicle_id, price, specs_json, created_at) VALUES (?, ?, ?, ?, ?, ?)",
        (listing_id, client.net_id, vehicle_id, price, json.dumps(specs), time.time()),
    )

    payload = {
        "type": "market_created",
        "listingId": listing_id,
        "sellerNetId": client.net_id,
        "vehicleId": vehicle_id,
        "price": price,
        "specs": specs,
    }
    if client.room:
        await broadcast_room(client.room, payload)
    else:
        await send(client.ws, payload)

async def handle_market_buy(client: Client, msg: dict) -> None:
    listing_id = msg.get("listingId")
    rows = db_execute(
        "SELECT seller_net_id, vehicle_id, price FROM market_listings WHERE listing_id = ?",
        (listing_id,),
        fetch=True,
    )
    if not rows:
        await send(client.ws, {"type": "error", "message": "Listing not found"})
        return

    seller_id, vehicle_id, price = rows[0]
    if seller_id == client.net_id:
        await send(client.ws, {"type": "error", "message": "Cannot buy own listing"})
        return

    buyer_rows = db_execute("SELECT cash FROM players WHERE net_id = ?", (client.net_id,), fetch=True)
    if not buyer_rows or buyer_rows[0][0] < price:
        await send(client.ws, {"type": "error", "message": "Insufficient funds"})
        return

    db_execute("UPDATE players SET cash = cash - ? WHERE net_id = ?", (price, client.net_id))
    db_execute("UPDATE players SET cash = cash + ? WHERE net_id = ?", (price, seller_id))
    db_execute("DELETE FROM market_listings WHERE listing_id = ?", (listing_id,))
    db_execute(
        "INSERT OR REPLACE INTO vehicles (vehicle_id, owner_net_id, data_json) VALUES (?, ?, ?)",
        (vehicle_id, client.net_id, json.dumps({"transferred_at": time.time()})),
    )

    result = {
        "type": "market_sold",
        "listingId": listing_id,
        "vehicleId": vehicle_id,
        "buyerNetId": client.net_id,
        "sellerNetId": seller_id,
        "price": price,
    }
    if client.room:
        await broadcast_room(client.room, result)
    else:
        await send(client.ws, result)

async def handle_race_challenge(client: Client, msg: dict) -> None:
    race_id = str(uuid.uuid4())
    opponent_id = msg.get("opponentNetId")
    race_type = msg.get("raceType", "Drag400m")

    race_sessions[race_id] = {
        "player1": client.net_id,
        "player2": opponent_id,
        "type": race_type,
        "state": "countdown",
        "start_time": time.time() + 3.0,
    }

    payload = {
        "type": "race_start",
        "raceId": race_id,
        "player1": client.net_id,
        "player2": opponent_id,
        "raceType": race_type,
        "countdown": 3.0,
    }
    await send(client.ws, payload)
    opp = clients.get(opponent_id)
    if opp:
        await send(opp.ws, payload)

async def handle_message(client: Client, raw: str) -> None:
    try:
        msg = json.loads(raw)
    except json.JSONDecodeError:
        return

    t = msg.get("type")
    if t == "join":
        await handle_join(client, msg)
    elif t == "state":
        await handle_state(client, msg)
    elif t == "voice":
        await handle_voice(client, msg)
    elif t == "market_list":
        await handle_market_list(client, msg)
    elif t == "market_create":
        await handle_market_create(client, msg)
    elif t == "market_buy":
        await handle_market_buy(client, msg)
    elif t == "race_challenge":
        await handle_race_challenge(client, msg)
    elif t == "ping":
        await send(client.ws, {"type": "pong", "t": time.time()})

async def connection_handler(ws: WebSocketServerProtocol) -> None:
    net_id = str(uuid.uuid4())
    client = Client(ws, net_id, "Driver")
    clients[net_id] = client
    print(f"[+] Connected {net_id}")

    try:
        async for raw in ws:
            await handle_message(client, raw)
    except websockets.exceptions.ConnectionClosed:
        pass
    finally:
        if client.room and client.room in rooms:
            rooms[client.room].discard(net_id)
            await broadcast_room(client.room, {"type": "player_left", "netId": net_id})
        clients.pop(net_id, None)
        print(f"[-] Disconnected {net_id}")

async def main() -> None:
    init_db()
    print("Ultra Car Simulator Multiplayer Server")
    print(f"Listening on ws://{HOST}:{PORT}")
    async with websockets.serve(connection_handler, HOST, PORT, ping_interval=20, ping_timeout=20):
        await asyncio.Future()

if __name__ == "__main__":
    asyncio.run(main())
