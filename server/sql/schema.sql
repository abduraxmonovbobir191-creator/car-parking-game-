-- NextGen Drive: PostgreSQL schema. Every change is additive: CREATE ... IF NOT EXISTS only.

CREATE TABLE IF NOT EXISTS players (
    id             TEXT PRIMARY KEY,
    username       TEXT   NOT NULL,
    balance        BIGINT NOT NULL DEFAULT 0 CHECK (balance >= 0),
    escrow_locked  BIGINT NOT NULL DEFAULT 0 CHECK (escrow_locked >= 0),
    vip_coins      BIGINT NOT NULL DEFAULT 0 CHECK (vip_coins >= 0),
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS cars (
    vin                     TEXT PRIMARY KEY,
    owner_id                TEXT    NOT NULL REFERENCES players(id),
    model_name              TEXT    NOT NULL,
    horsepower              INTEGER NOT NULL CHECK (horsepower > 0),
    odometer_km             NUMERIC(12, 1) NOT NULL DEFAULT 0 CHECK (odometer_km >= 0),
    accidents_count         INTEGER NOT NULL DEFAULT 0 CHECK (accidents_count >= 0),
    condition_percent       INTEGER NOT NULL DEFAULT 100 CHECK (condition_percent BETWEEN 0 AND 100),
    last_accident_at        BIGINT  NOT NULL DEFAULT 0,
    last_accident_speed_kmh INTEGER,
    tuning                  JSONB,
    custom_parts            JSONB   NOT NULL DEFAULT '[]'::jsonb,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Carfax: append-only owner history, one row per owner, in order
CREATE TABLE IF NOT EXISTS car_owners (
    vin       TEXT    NOT NULL REFERENCES cars(vin),
    seq       INTEGER NOT NULL CHECK (seq >= 0),
    owner_id  TEXT    NOT NULL REFERENCES players(id),
    PRIMARY KEY (vin, seq)
);

CREATE TABLE IF NOT EXISTS vip_plates (
    plate       TEXT PRIMARY KEY,
    tier        TEXT    NOT NULL,
    price_coins INTEGER NOT NULL CHECK (price_coins > 0),
    owner_id    TEXT REFERENCES players(id)
);

CREATE TABLE IF NOT EXISTS auctions (
    vin               TEXT PRIMARY KEY REFERENCES cars(vin),
    auction_id        TEXT   NOT NULL UNIQUE,
    seller_id         TEXT   NOT NULL REFERENCES players(id),
    current_bid       BIGINT NOT NULL CHECK (current_bid >= 0),
    buyout_price      BIGINT NOT NULL CHECK (buyout_price >= 0),
    highest_bidder_id TEXT REFERENCES players(id),
    end_time          BIGINT NOT NULL,
    status            TEXT   NOT NULL CHECK (status IN ('ACTIVE', 'COMPLETED'))
);

CREATE TABLE IF NOT EXISTS races (
    race_id        TEXT PRIMARY KEY,
    challenger_id  TEXT   NOT NULL REFERENCES players(id),
    opponent_id    TEXT   NOT NULL REFERENCES players(id),
    challenger_vin TEXT   NOT NULL REFERENCES cars(vin),
    opponent_vin   TEXT REFERENCES cars(vin),
    bet_amount     BIGINT NOT NULL CHECK (bet_amount >= 0),
    is_pink_slip   BOOLEAN NOT NULL,
    status         TEXT   NOT NULL CHECK (status IN ('PENDING', 'IN_PROGRESS', 'FINISHED')),
    start_time     BIGINT,
    created_at     BIGINT NOT NULL,
    results        JSONB  NOT NULL DEFAULT '{}'::jsonb
);

-- Audit trail of every money movement; written in the same transaction as the change
CREATE TABLE IF NOT EXISTS ledger (
    id         BIGSERIAL PRIMARY KEY,
    player_id  TEXT   NOT NULL REFERENCES players(id),
    field      TEXT   NOT NULL CHECK (field IN ('balance', 'escrow_locked', 'vip_coins')),
    delta      BIGINT NOT NULL,
    reason     TEXT   NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ledger_player_idx ON ledger (player_id, id);
CREATE INDEX IF NOT EXISTS cars_owner_idx ON cars (owner_id);
