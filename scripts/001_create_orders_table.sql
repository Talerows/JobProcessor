CREATE TABLE IF NOT EXISTS orders
(
    id           BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    -- name         TEXT         NOT NULL,
    status       VARCHAR(50)  NOT NULL DEFAULT 'Open'
                              CHECK (status IN ('Open', 'InProgress', 'Completed', 'Timeout')),
    created_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    started_at   TIMESTAMPTZ,
    finished_at     TIMESTAMPTZ,
    timeout_at TIMESTAMPTZ
);

INSERT INTO orders (status)
SELECT 'Open'
FROM generate_series(1, 50);