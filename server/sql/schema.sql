CREATE TABLE IF NOT EXISTS players (
    id VARCHAR(64) PRIMARY KEY,
    username VARCHAR(64) NOT NULL,
    balance BIGINT NOT NULL DEFAULT 150000 CHECK (balance >= 0),
    escrow_locked BIGINT NOT NULL DEFAULT 0 CHECK (escrow_locked >= 0),
    vip_coins INT NOT NULL DEFAULT 1200 CHECK (vip_coins >= 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS cars (
    vin VARCHAR(32) PRIMARY KEY,
    owner_id VARCHAR(64) NOT NULL REFERENCES players(id) ON DELETE CASCADE,
    model_name VARCHAR(128) NOT NULL,
    horsepower INT NOT NULL DEFAULT 600,
    odometer_km NUMERIC(10, 2) NOT NULL DEFAULT 0.0,
    accidents_count INT NOT NULL DEFAULT 0,
    last_accident_speed_kmh INT NOT NULL DEFAULT 0,
    condition_percent INT NOT NULL DEFAULT 100 CHECK (condition_percent BETWEEN 0 AND 100),
    owners_history JSONB NOT NULL DEFAULT '[]'::jsonb,
    custom_parts JSONB NOT NULL DEFAULT '[]'::jsonb,
    tuning_setup JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS vip_plates (
    plate_code VARCHAR(32) PRIMARY KEY,
    tier VARCHAR(32) NOT NULL,
    price_coins INT NOT NULL,
    owner_id VARCHAR(64) REFERENCES players(id) ON DELETE SET NULL,
    assigned_vin VARCHAR(32) REFERENCES cars(vin) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS auctions (
    auction_id UUID PRIMARY KEY,
    vin VARCHAR(32) UNIQUE NOT NULL REFERENCES cars(vin) ON DELETE CASCADE,
    seller_id VARCHAR(64) NOT NULL REFERENCES players(id),
    current_bid BIGINT NOT NULL,
    buyout_price BIGINT NOT NULL,
    highest_bidder_id VARCHAR(64) REFERENCES players(id),
    end_time TIMESTAMPTZ NOT NULL,
    status VARCHAR(24) NOT NULL DEFAULT 'ACTIVE'
);

INSERT INTO vip_plates (plate_code, tier, price_coins) VALUES
    ('01 A 777 AA', 'PLATINUM', 500),
    ('01 001 VIP', 'DIAMOND', 1000),
    ('01 Z 888 ZZ', 'GOLD', 350),
    ('10 X 707 XX', 'GOLD', 300)
ON CONFLICT (plate_code) DO NOTHING;

-- ---------------------------------------------------------------------------
-- Additive section (added by the server team; nothing above is changed).
-- Needed by server/src/auction_server.js for race state and the money audit trail.
-- ---------------------------------------------------------------------------

ALTER TABLE cars ADD COLUMN IF NOT EXISTS last_accident_at BIGINT NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS races (
    race_id        VARCHAR(16) PRIMARY KEY,
    challenger_id  VARCHAR(64) NOT NULL REFERENCES players(id),
    opponent_id    VARCHAR(64) NOT NULL REFERENCES players(id),
    challenger_vin VARCHAR(32) NOT NULL REFERENCES cars(vin),
    opponent_vin   VARCHAR(32) REFERENCES cars(vin),
    bet_amount     BIGINT NOT NULL CHECK (bet_amount >= 0),
    is_pink_slip   BOOLEAN NOT NULL,
    status         VARCHAR(24) NOT NULL CHECK (status IN ('PENDING', 'IN_PROGRESS', 'FINISHED')),
    start_time     BIGINT,
    created_at     BIGINT NOT NULL,
    results        JSONB NOT NULL DEFAULT '{}'::jsonb
);

-- Every money movement, written in the same transaction as the change
CREATE TABLE IF NOT EXISTS ledger (
    id         BIGSERIAL PRIMARY KEY,
    player_id  VARCHAR(64) NOT NULL REFERENCES players(id),
    field      VARCHAR(24) NOT NULL CHECK (field IN ('balance', 'escrow_locked', 'vip_coins')),
    delta      BIGINT NOT NULL,
    reason     VARCHAR(64) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ledger_player_idx ON ledger (player_id, id);
CREATE INDEX IF NOT EXISTS cars_owner_idx ON cars (owner_id);

-- Police heat (additive): pursuit level and when it was last raised, used to decay it
ALTER TABLE players ADD COLUMN IF NOT EXISTS heat INT NOT NULL DEFAULT 0 CHECK (heat BETWEEN 0 AND 5);
ALTER TABLE players ADD COLUMN IF NOT EXISTS heat_at BIGINT NOT NULL DEFAULT 0;
