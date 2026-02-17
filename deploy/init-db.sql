-- =============================================================================
-- QuantTrader Database Initialization
-- Requires: PostgreSQL 16 with TimescaleDB extension
-- =============================================================================

-- Enable TimescaleDB
CREATE EXTENSION IF NOT EXISTS timescaledb;

-- =============================================================================
-- market_ticks: raw tick-level market data
-- =============================================================================
CREATE TABLE IF NOT EXISTS market_ticks (
    id              BIGSERIAL       NOT NULL,
    symbol          VARCHAR(20)     NOT NULL,
    exchange        VARCHAR(30)     NOT NULL,
    price           NUMERIC(24,8)   NOT NULL,
    quantity        NUMERIC(24,8)   NOT NULL,
    side            VARCHAR(4)      NOT NULL,  -- 'buy' or 'sell'
    trade_id        VARCHAR(64),
    timestamp       TIMESTAMPTZ     NOT NULL,
    received_at     TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

SELECT create_hypertable('market_ticks', 'timestamp',
    chunk_time_interval => INTERVAL '1 day',
    if_not_exists => TRUE
);

CREATE INDEX IF NOT EXISTS idx_market_ticks_symbol_ts
    ON market_ticks (symbol, timestamp DESC);

CREATE INDEX IF NOT EXISTS idx_market_ticks_exchange_symbol
    ON market_ticks (exchange, symbol, timestamp DESC);

-- =============================================================================
-- candles: OHLCV candlestick data
-- =============================================================================
CREATE TABLE IF NOT EXISTS candles (
    id              BIGSERIAL       NOT NULL,
    symbol          VARCHAR(20)     NOT NULL,
    exchange        VARCHAR(30)     NOT NULL,
    interval        VARCHAR(10)     NOT NULL,  -- '1m', '5m', '15m', '1h', '4h', '1d'
    open_time       TIMESTAMPTZ     NOT NULL,
    close_time      TIMESTAMPTZ     NOT NULL,
    open            NUMERIC(24,8)   NOT NULL,
    high            NUMERIC(24,8)   NOT NULL,
    low             NUMERIC(24,8)   NOT NULL,
    close           NUMERIC(24,8)   NOT NULL,
    volume          NUMERIC(24,8)   NOT NULL,
    quote_volume    NUMERIC(24,8),
    trade_count     INTEGER,
    is_closed       BOOLEAN         NOT NULL DEFAULT FALSE
);

SELECT create_hypertable('candles', 'open_time',
    chunk_time_interval => INTERVAL '7 days',
    if_not_exists => TRUE
);

CREATE INDEX IF NOT EXISTS idx_candles_symbol_interval_ts
    ON candles (symbol, interval, open_time DESC);

CREATE UNIQUE INDEX IF NOT EXISTS idx_candles_unique
    ON candles (symbol, exchange, interval, open_time);

-- =============================================================================
-- trades: executed trade records
-- =============================================================================
CREATE TABLE IF NOT EXISTS trades (
    id              UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id        UUID            NOT NULL,
    symbol          VARCHAR(20)     NOT NULL,
    exchange        VARCHAR(30)     NOT NULL,
    side            VARCHAR(4)      NOT NULL,  -- 'buy' or 'sell'
    type            VARCHAR(20)     NOT NULL,  -- 'market', 'limit', 'stop_limit'
    quantity        NUMERIC(24,8)   NOT NULL,
    price           NUMERIC(24,8)   NOT NULL,
    fee             NUMERIC(24,8)   NOT NULL DEFAULT 0,
    fee_currency    VARCHAR(10),
    realized_pnl    NUMERIC(24,8),
    strategy_id     VARCHAR(64),
    exchange_trade_id VARCHAR(64),
    executed_at     TIMESTAMPTZ     NOT NULL,
    created_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_trades_symbol_ts
    ON trades (symbol, executed_at DESC);

CREATE INDEX IF NOT EXISTS idx_trades_order_id
    ON trades (order_id);

CREATE INDEX IF NOT EXISTS idx_trades_strategy_id
    ON trades (strategy_id, executed_at DESC);

-- =============================================================================
-- orders: order lifecycle tracking
-- =============================================================================
CREATE TABLE IF NOT EXISTS orders (
    id              UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    symbol          VARCHAR(20)     NOT NULL,
    exchange        VARCHAR(30)     NOT NULL,
    side            VARCHAR(4)      NOT NULL,  -- 'buy' or 'sell'
    type            VARCHAR(20)     NOT NULL,  -- 'market', 'limit', 'stop_limit'
    status          VARCHAR(20)     NOT NULL DEFAULT 'pending',
                    -- 'pending', 'submitted', 'partial', 'filled', 'cancelled', 'rejected'
    quantity        NUMERIC(24,8)   NOT NULL,
    filled_quantity NUMERIC(24,8)   NOT NULL DEFAULT 0,
    price           NUMERIC(24,8),             -- NULL for market orders
    stop_price      NUMERIC(24,8),
    avg_fill_price  NUMERIC(24,8),
    time_in_force   VARCHAR(10)     NOT NULL DEFAULT 'GTC',  -- 'GTC', 'IOC', 'FOK'
    strategy_id     VARCHAR(64),
    exchange_order_id VARCHAR(64),
    reject_reason   TEXT,
    created_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    submitted_at    TIMESTAMPTZ,
    filled_at       TIMESTAMPTZ,
    cancelled_at    TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_orders_symbol_status
    ON orders (symbol, status, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_orders_strategy_id
    ON orders (strategy_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_orders_status
    ON orders (status) WHERE status IN ('pending', 'submitted', 'partial');

CREATE INDEX IF NOT EXISTS idx_orders_exchange_order_id
    ON orders (exchange, exchange_order_id);
