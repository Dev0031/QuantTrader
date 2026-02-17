# QuantTrader - Elite Crypto Trading Bot

## Project Overview
High-performance, event-driven crypto trading bot built on .NET 8 LTS with microservices architecture, deployed on Azure (AKS). Connects to Binance (testnet first) for automated trading with institutional-grade risk controls.

## Architecture
Microservices communicating via Azure Service Bus (event-driven). Each service is a separate .NET project in `src/Services/`.

### Services
- **DataIngestion** - Binance WebSocket/REST + CoinGecko/CryptoPanic feeds → TimescaleDB + Redis
- **StrategyEngine** - Rule-based strategies (MA crossover, momentum, mean-reversion) + optional ML
- **RiskManager** - Position sizing (1-2% risk), stop-loss, max drawdown (5-8%), kill-switch
- **ExecutionEngine** - Binance REST order placement, HMAC-signed, order lifecycle management
- **ApiGateway** - REST/WebSocket API for dashboard, JWT auth, rate limiting

### Frontend
- **dashboard/** - React + TypeScript SPA with real-time charts, trade logs, P&L monitoring

### Shared Libraries (src/Shared/)
- **QuantTrader.Common** - Domain models, DTOs, enums, constants
- **QuantTrader.Infrastructure** - DB context, Redis, Service Bus, Key Vault integrations
- **QuantTrader.Indicators** - Technical indicators (SMA, EMA, RSI, MACD, ATR, Bollinger Bands)

## Tech Stack
- **Runtime**: .NET 8 LTS (C# 12)
- **Database**: PostgreSQL + TimescaleDB (time-series), Redis (cache/pub-sub)
- **Messaging**: Azure Service Bus (or RabbitMQ for local dev)
- **Auth**: Azure AD B2C / JWT Bearer tokens
- **Secrets**: Azure Key Vault (local: dotnet user-secrets)
- **Observability**: OpenTelemetry + Prometheus + Grafana + Jaeger
- **CI/CD**: GitHub Actions → ACR → AKS
- **Frontend**: React 18 + TypeScript + Vite + TailwindCSS + Recharts

## Solution Structure
```
QuantTrader.sln
src/
  Services/
    DataIngestion/        # Binance WS, API polling, data normalization
    StrategyEngine/       # Trading strategies, signal generation
    RiskManager/          # Risk controls, position sizing, kill-switch
    ExecutionEngine/      # Order placement/management on Binance
    ApiGateway/           # HTTP/WS API for frontend
  Shared/
    QuantTrader.Common/           # Models, DTOs, events, enums
    QuantTrader.Infrastructure/   # EF Core, Redis, ServiceBus, KeyVault
    QuantTrader.Indicators/       # Technical analysis indicators
tests/
  DataIngestion.Tests/
  StrategyEngine.Tests/
  RiskManager.Tests/
  ExecutionEngine.Tests/
  Indicators.Tests/
  Integration.Tests/
dashboard/                # React frontend
deploy/
  docker/                 # Dockerfiles per service
  k8s/                    # Kubernetes manifests
  helm/                   # Helm charts
.github/workflows/        # CI/CD pipelines
```

## Coding Conventions
- Use `async/await` throughout; never block on async (.Result/.Wait())
- Use `CancellationToken` on all async methods
- Prefer `IOptions<T>` pattern for configuration
- Use `record` types for DTOs and events (immutable)
- All services implement `IHostedService` or `BackgroundService`
- Use `System.Text.Json` (not Newtonsoft) for serialization
- Health checks at `/health/live` and `/health/ready`
- Metrics endpoint at `/metrics` (Prometheus format)
- Structured logging with Serilog (JSON output)
- Error handling: use Result pattern, avoid exceptions for control flow

## Event Bus Conventions
- Events are records in `QuantTrader.Common.Events`
- Naming: `{Entity}{Action}Event` (e.g., `MarketTickReceivedEvent`, `TradeSignalGeneratedEvent`)
- All events include `Timestamp`, `CorrelationId`, `Source`

## Risk Rules (Non-Negotiable)
- Max risk per trade: 1-2% of account equity
- Max drawdown before kill-switch: 5-8% (configurable)
- Every trade MUST have a stop-loss
- Min risk-reward ratio: 1:2
- Position sizes computed from stop distance, never arbitrary

## Testing
- Unit tests: xUnit + Moq + FluentAssertions
- Integration tests: Testcontainers (PostgreSQL, Redis, RabbitMQ)
- Backtesting: Historical OHLCV data replay through strategy engine
- Run `dotnet test` from solution root

## Local Development
```bash
# Prerequisites: .NET 8 SDK, Node.js 20+, Docker
docker compose up -d          # PostgreSQL, Redis, RabbitMQ
dotnet build QuantTrader.sln
dotnet run --project src/Services/ApiGateway
cd dashboard && npm install && npm run dev
```

## Key Commands
- `dotnet build` - Build all projects
- `dotnet test` - Run all tests
- `dotnet format` - Format code
- `docker compose up` - Start local infrastructure
- `cd dashboard && npm run dev` - Start React dev server

## Important Notes
- NEVER commit API keys or secrets
- Always use Binance TESTNET for development (wss://testnet.binance.com:9443/ws)
- All monetary values use `decimal` type, never `float`/`double`
- Timestamps are UTC `DateTimeOffset`
- Symbol format: uppercase (e.g., "BTCUSDT")
