#!/usr/bin/env python3
"""
Car Token Economy Ledger — cryptographic-style ownership records,
pink-slip P2P transfers, high-tier asset trading.
SQLite-backed append-only style log + balances.
"""

import hashlib
import json
import sqlite3
import time
import uuid
from pathlib import Path

DB_PATH = Path(__file__).parent / "car_token_ledger.db"


def init_db():
    conn = sqlite3.connect(DB_PATH)
    cur = conn.cursor()
    cur.executescript("""
    CREATE TABLE IF NOT EXISTS accounts (
        account_id TEXT PRIMARY KEY,
        display_name TEXT,
        balance REAL DEFAULT 0,
        created_at REAL
    );
    CREATE TABLE IF NOT EXISTS assets (
        token_id TEXT PRIMARY KEY,
        owner_id TEXT NOT NULL,
        vehicle_class TEXT,
        metadata_json TEXT,
        created_at REAL,
        FOREIGN KEY(owner_id) REFERENCES accounts(account_id)
    );
    CREATE TABLE IF NOT EXISTS ledger (
        entry_id TEXT PRIMARY KEY,
        prev_hash TEXT,
        entry_hash TEXT,
        entry_type TEXT,
        from_id TEXT,
        to_id TEXT,
        token_id TEXT,
        amount REAL,
        timestamp REAL,
        payload_json TEXT
    );
    """)
    conn.commit()
    conn.close()


def _conn():
    return sqlite3.connect(DB_PATH)


def _last_hash(cur) -> str:
    cur.execute("SELECT entry_hash FROM ledger ORDER BY timestamp DESC LIMIT 1")
    row = cur.fetchone()
    return row[0] if row else "GENESIS"


def _hash_entry(prev: str, data: dict) -> str:
    raw = prev + json.dumps(data, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(raw.encode()).hexdigest()


def ensure_account(account_id: str, name: str = "Player") -> None:
    conn = _conn()
    cur = conn.cursor()
    cur.execute("SELECT account_id FROM accounts WHERE account_id = ?", (account_id,))
    if not cur.fetchone():
        cur.execute(
            "INSERT INTO accounts (account_id, display_name, balance, created_at) VALUES (?, ?, 10000, ?)",
            (account_id, name, time.time()),
        )
        conn.commit()
    conn.close()


def mint_vehicle_token(owner_id: str, vehicle_class: str, metadata: dict) -> str:
    ensure_account(owner_id)
    token_id = str(uuid.uuid4())
    conn = _conn()
    cur = conn.cursor()
    cur.execute(
        "INSERT INTO assets (token_id, owner_id, vehicle_class, metadata_json, created_at) VALUES (?, ?, ?, ?, ?)",
        (token_id, owner_id, vehicle_class, json.dumps(metadata), time.time()),
    )
    prev = _last_hash(cur)
    payload = {"action": "mint", "token_id": token_id, "owner": owner_id, "class": vehicle_class}
    h = _hash_entry(prev, payload)
    cur.execute(
        "INSERT INTO ledger (entry_id, prev_hash, entry_hash, entry_type, from_id, to_id, token_id, amount, timestamp, payload_json) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
        (str(uuid.uuid4()), prev, h, "mint", None, owner_id, token_id, 0, time.time(), json.dumps(payload)),
    )
    conn.commit()
    conn.close()
    return token_id


def transfer_pink_slip(token_id: str, from_id: str, to_id: str, price: float = 0.0) -> bool:
    ensure_account(from_id)
    ensure_account(to_id)
    conn = _conn()
    cur = conn.cursor()
    cur.execute("SELECT owner_id FROM assets WHERE token_id = ?", (token_id,))
    row = cur.fetchone()
    if not row or row[0] != from_id:
        conn.close()
        return False

    if price > 0:
        cur.execute("SELECT balance FROM accounts WHERE account_id = ?", (to_id,))
        bal = cur.fetchone()
        if not bal or bal[0] < price:
            conn.close()
            return False
        cur.execute("UPDATE accounts SET balance = balance - ? WHERE account_id = ?", (price, to_id))
        cur.execute("UPDATE accounts SET balance = balance + ? WHERE account_id = ?", (price, from_id))

    cur.execute("UPDATE assets SET owner_id = ? WHERE token_id = ?", (to_id, token_id))

    prev = _last_hash(cur)
    payload = {"action": "transfer", "token_id": token_id, "from": from_id, "to": to_id, "price": price}
    h = _hash_entry(prev, payload)
    cur.execute(
        "INSERT INTO ledger (entry_id, prev_hash, entry_hash, entry_type, from_id, to_id, token_id, amount, timestamp, payload_json) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
        (str(uuid.uuid4()), prev, h, "transfer", from_id, to_id, token_id, price, time.time(), json.dumps(payload)),
    )
    conn.commit()
    conn.close()
    return True


def get_ownership(token_id: str):
    conn = _conn()
    cur = conn.cursor()
    cur.execute("SELECT owner_id, vehicle_class, metadata_json FROM assets WHERE token_id = ?", (token_id,))
    row = cur.fetchone()
    conn.close()
    if not row:
        return None
    return {"owner_id": row[0], "vehicle_class": row[1], "metadata": json.loads(row[2] or "{}")}


def get_balance(account_id: str) -> float:
    conn = _conn()
    cur = conn.cursor()
    cur.execute("SELECT balance FROM accounts WHERE account_id = ?", (account_id,))
    row = cur.fetchone()
    conn.close()
    return float(row[0]) if row else 0.0


def verify_chain() -> bool:
    conn = _conn()
    cur = conn.cursor()
    cur.execute("SELECT prev_hash, entry_hash, payload_json FROM ledger ORDER BY timestamp ASC")
    rows = cur.fetchall()
    conn.close()
    prev = "GENESIS"
    for r in rows:
        if r[0] != prev:
            return False
        expected = _hash_entry(r[0], json.loads(r[2]))
        if expected != r[1]:
            return False
        prev = r[1]
    return True


if __name__ == "__main__":
    init_db()
    print("Car Token Ledger initialized:", DB_PATH)
    tid = mint_vehicle_token("player_demo", "Hypercar", {"name": "Demo GT"})
    print("Minted token:", tid)
    print("Chain valid:", verify_chain())
