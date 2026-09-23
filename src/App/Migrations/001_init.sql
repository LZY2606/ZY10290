CREATE TABLE IF NOT EXISTS rounds (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    round_index INTEGER NOT NULL UNIQUE,
    offer_raw TEXT NOT NULL,
    answer_raw TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS media_states (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    round_index INTEGER NOT NULL,
    media_index INTEGER NOT NULL,
    mid TEXT,
    media TEXT NOT NULL,
    rejected INTEGER NOT NULL,
    offer_direction TEXT NOT NULL,
    answer_direction TEXT,
    negotiated_direction TEXT,
    codec_intersection TEXT NOT NULL,
    owns_bundle_transport INTEGER NOT NULL,
    ice_generation INTEGER,
    UNIQUE (round_index, media_index)
);

CREATE TABLE IF NOT EXISTS diagnostics (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    round_index INTEGER NOT NULL,
    media_index INTEGER,
    severity TEXT NOT NULL,
    code TEXT NOT NULL,
    message TEXT NOT NULL
);
