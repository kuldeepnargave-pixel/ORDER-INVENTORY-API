# Order & Inventory Management System

A complete, locally runnable Order and Inventory Management System built for a
.NET Full-Stack machine test: **ASP.NET Core 8 Web API + EF Core + SQL Server**
backend, **React 18 + TypeScript + Vite** frontend, JWT authentication with
refresh-token rotation, transactional order creation with optimistic
concurrency, idempotency keys, discount rules, an auto-cancel background
service, and automated tests on all layers.

---

## Table of contents

1. [Project overview](#project-overview)
2. [Technology stack](#technology-stack)
3. [Folder structure](#folder-structure)
4. [Architecture explanation](#architecture-explanation)
5. [Prerequisites](#prerequisites)
6. [SQL Server setup](#sql-server-setup)
7. [Database configuration](#database-configuration)
8. [Migration and seed instructions](#migration-and-seed-instructions)
9. [Backend startup instructions](#backend-startup-instructions)
10. [Frontend startup instructions](#frontend-startup-instructions)
11. [Demo credentials](#demo-credentials)
12. [API endpoint list](#api-endpoint-list)
13. [Authentication and roles](#authentication-and-roles)
14. [Discount calculation rules](#discount-calculation-rules)
15. [Stock concurrency handling](#stock-concurrency-handling)
16. [Idempotency behavior](#idempotency-behavior)
17. [Order cancellation behavior](#order-cancellation-behavior)
18. [Test commands and actual test results](#test-commands-and-actual-test-results)
19. [Generated SQL logging](#generated-sql-logging)
20. [Authentication & token storage trade-offs](#authentication--token-storage-trade-offs)
21. [Assumptions and known limitations](#assumptions-and-known-limitations)

---

## Project overview

| Area | What it does |
|---|---|
| Catalog | Searchable / filterable / sortable / paginated product list (server-side, SQL-translated), cached with `IMemoryCache` |
| Orders | Transactional order creation that reserves stock atomically, prices derived only from the database |
| Concurrency | SQL Server `rowversion` optimistic concurrency — the last unit of stock can only be sold once (409 for the loser) |
| Idempotency | `Idempotency-Key` header on order creation; replays return the original order; unique index prevents duplicates even under concurrency |
| Discounts | Server-calculated: 5% over subtotal 10,000, extra 10% on every 5th order per customer |
| Lifecycle | Explicit status state machine + `BackgroundService` that cancels Pending orders older than 30 minutes and restores stock |
| Auth | JWT access tokens (15 min) + rotating refresh tokens (7 days, stored hashed); role-based + ownership authorization; login rate limiting |
| Reporting | Admin-only top-customers report executed as a single SQL `GROUP BY` query |

## Technology stack

**Backend**
- C# / .NET 8 (ASP.NET Core Web API)
- Entity Framework Core 8 (SQL Server provider)
- SQL Server (2019/2022 or SQL Server Express / LocalDB / Docker)
- JWT bearer authentication (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- ASP.NET Core Identity `PasswordHasher<User>` (PBKDF2 password hashing)
- Built-in rate limiting (`Microsoft.AspNetCore.RateLimiting`)
- `IMemoryCache` output caching for the product list
- `BackgroundService` (`PeriodicTimer`) for automatic pending-order cancellation
- Swagger / OpenAPI with JWT bearer support (Swashbuckle)
- xUnit + `Microsoft.AspNetCore.Mvc.Testing` (integration tests)

**Frontend**
- React 18 + TypeScript + Vite 5
- React Router 6
- Axios (single-flight token-refresh interceptor)
- Plain CSS (no UI framework)
- Vitest + React Testing Library + jsdom

## Folder structure

```
OrderInventory/
├── Backend/                        # ASP.NET Core 8 Web API
│   ├── Controllers/                # Auth, Products, Orders, Reports
│   ├── Models/                     # EF Core entities + UserRoles/OrderStatus
│   ├── DTOs/                       # Request/response contracts
│   ├── Services/                   # Token, Auth, Product, Order, Report services
│   ├── Data/                       # AppDbContext, migrations, seeder, initializer
│   ├── Middleware/                 # ExceptionHandlingMiddleware (ProblemDetails)
│   ├── BackgroundServices/         # PendingOrderCancellationService
│   ├── Helpers/                    # DiscountCalculator, transitions, hashing...
│   ├── Migrations/                 # EF Core migrations (InitialCreate)
│   ├── Program.cs
│   ├── appsettings.json            # dev-only example configuration
│   └── Backend.csproj
├── Frontend/                       # React 18 + TypeScript + Vite
│   ├── src/
│   │   ├── components/             # Navbar, guards, cards, dialogs, states...
│   │   ├── pages/                  # Login, Register, Catalog, Cart, Checkout,
│   │   │                           # MyOrders, OrderDetails, Admin x3
│   │   ├── services/               # Axios instance + API service layer
│   │   ├── models/                 # Shared TypeScript interfaces
│   │   ├── hooks/                  # useDebounce
│   │   ├── context/                # AuthContext, CartContext
│   │   ├── utils/                  # order status map, checkout key helpers
│   │   ├── __tests__/              # Vitest + RTL tests
│   │   ├── App.tsx
│   │   └── main.tsx
│   ├── package.json
│   └── vite.config.ts
├── Tests/
│   ├── UnitTests/                  # 58 xUnit unit tests (SQLite relational mode)
│   └── IntegrationTests/           # WebApplicationFactory integration tests
├── QUERIES.sql                     # 5 documented SQL Server queries
├── REVIEW.md                       # Production issues & how they are prevented
├── README.md
├── docker-compose.yml              # Optional local SQL Server
└── OrderInventory.sln
```

## Architecture explanation

The backend uses a straightforward layered design — no CQRS, MediatR, generic
repositories or heavy Clean Architecture — deliberately sized for a ~2-year
developer profile:

- **Controllers** bind and validate input (data annotations + `[ApiController]`),
  resolve the current user from claims, and shape HTTP responses (201/200/204).
- **Services** contain the business rules: `OrderService` owns transactions,
  stock reservation, discount calculation and idempotency; `ProductService`
  owns catalog queries and cache; `AuthService`/`TokenService` own credential
  verification and token issue/rotation; `ReportService` owns the aggregation
  query.
- **`AppDbContext`** centralizes all EF Core configuration: precision, unique
  indexes, CHECK constraints, delete behaviours, and rowversion concurrency
  tokens.
- **Cross-cutting concerns** live in dedicated pieces: `ExceptionHandlingMiddleware`
  (ProblemDetails), the login rate-limiter policy, and
  `PendingOrderCancellationService` (a scoped-service-per-tick background job).
- DTOs are always returned instead of EF entities, so `PasswordHash`,
  `RowVersion` and refresh-token secrets never reach the JSON layer.

The frontend mirrors this: a single Axios instance with interceptors (token
attach + single-flight refresh-and-retry), a service layer per resource, a
small auth context (tokens + user) and cart context (localStorage-persisted),
and presentational pages/components. Catalog filters are synchronized with URL
query parameters so views are shareable and refresh-safe.

## Prerequisites

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server 2019/2022, SQL Server Express, or Docker (for the optional compose file)
- Node.js 18+ and npm 9+ (frontend; Node 24 used during development)
- Optional: `dotnet-ef` tool (`dotnet tool install --global dotnet-ef --version 8.0.11`)

## SQL Server setup

Any of these options works; the app only needs a reachable SQL Server and a
database name (it creates the schema itself via migrations).

**Option A — locally installed SQL Server / Express (Windows)**
The default connection string in `Backend/appsettings.json` already targets a
local instance:

```
Server=localhost;Database=OrderInventoryDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true
```

**Option B — SQL Server in Docker (any OS)**
`docker compose up -d` starts SQL Server 2022 (sa password `Your_password123`)
and then use this connection string instead:

```
Server=localhost,1433;Database=OrderInventoryDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True;MultipleActiveResultSets=true
```

## Database configuration

Edit `Backend/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "<your connection string>"
  },
  "Jwt": {
    "Issuer": "OrderInventory.Api",
    "Audience": "OrderInventory.Client",
    "Key": "DEV-ONLY__replace_me__order_inventory_super_secret_signing_key_min_32_chars",
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 7
  }
}
```

- Replace `Jwt:Key` with your own secret (min 32 characters) before any real
  deployment. The committed value is a **development-only example**.
- Token lifetimes are configurable via `AccessTokenMinutes` / `RefreshTokenDays`.
- `Orders:PendingCancellationMinutes` (default 30) and
  `Orders:AutoCancelScanIntervalSeconds` (default 60) control the background job.
- `RateLimiting:LoginPermitLimit` (default 5) controls the login rate limit.
- The frontend API base URL defaults to `/api` and is proxied to
  `http://localhost:5080` by the Vite dev server; set `VITE_API_BASE_URL` in
  `Frontend/.env` to point elsewhere (e.g. `https://localhost:7280/api`).

## Migration and seed instructions

From the repository root:

```bash
dotnet restore OrderInventory.sln
dotnet ef migrations add InitialCreate --project Backend --output-dir Migrations
dotnet ef database update --project Backend
```

> `InitialCreate` is already committed in `Backend/Migrations`, so the first
> command is only needed if you change the model. `dotnet ef database update`
> creates the database and applies the schema.

**Seed data** is applied automatically by `DbSeeder` on API startup (development)
or by any startup that runs the migrations: 1 admin user, 3 customer users, and
15 products with realistic prices/stock (including one out-of-stock product for
filter testing and a low-stock product for the QUERY 5 demo). Seeding is
idempotent — it no-ops when users already exist.

Demo credentials are listed [below](#demo-credentials) and use **development
passwords only**; they are public in this repository and must be changed before
any real deployment.

## Backend startup instructions

```bash
# from the repository root
dotnet run --project Backend
```

- HTTP profile: `http://localhost:5080` (HTTPS profile: `https://localhost:7280`)
- Swagger UI: `http://localhost:5080/swagger` (JWT bearer configured — click
  *Authorize* and paste the access token from `/api/auth/login`)
- On startup (Development) the app applies migrations and seeds demo data.

## Frontend startup instructions

```bash
cd Frontend
npm install
npm run dev          # http://localhost:5173
```

The Vite dev server proxies `/api/*` to `http://localhost:5080` (see
`vite.config.ts`), so no CORS friction during development. CORS is still fully
configured on the backend (`Cors:AllowedOrigins`) for the case where the
frontend talks to the API directly.

Production build:

```bash
npm run build        # type-checks (tsc) then bundles to dist/
npm run preview      # serve the production bundle locally
```

## Demo credentials

> Development credentials only. They are public in this repository and **must**
> be changed before production deployment.

| Role | Email | Password |
|---|---|---|
| Admin | `admin@orderinventory.com` | `Admin@123` |
| Customer | `alice@example.com` | `Customer1@123` |
| Customer | `bob@example.com` | `Customer2@123` |
| Customer | `carol@example.com` | `Customer3@123` |

## API endpoint list

| Method | Route | Auth | Notes |
|---|---|---|---|
| POST | `/api/auth/register` | none | 201 + tokens; duplicate email → 409 |
| POST | `/api/auth/login` | none | **rate limited: 5/min/IP → 429** |
| POST | `/api/auth/refresh` | none | rotates the refresh token |
| GET | `/api/products` | any user | `search, minPrice, maxPrice, inStock, page, pageSize (1–100), sortBy (name/price/stockQuantity/createdAt), sortDirection (asc/desc)` |
| GET | `/api/products/{id}` | any user | 404 if missing |
| POST | `/api/products` | Admin | 409 on duplicate SKU |
| PUT | `/api/products/{id}` | Admin | 409 on concurrent modification |
| POST | `/api/orders` | any user | requires `Idempotency-Key` header; 201 created / 200 replay / 409 key mismatch or stock conflict |
| GET | `/api/orders/my` | any user | current customer's orders |
| GET | `/api/orders` | Admin | all orders, paged |
| GET | `/api/orders/{id}` | owner or Admin | 403 for other customers |
| PATCH | `/api/orders/{id}/status` | owner (cancel only) or Admin | 400 on illegal transition |
| GET | `/api/reports/top-customers` | Admin | `?top=N` (default 5) |

All errors are RFC 7807 `application/problem+json` responses with proper
400/401/403/404/409/429/500 status codes.

## Authentication and roles

- `POST /api/auth/register` creates **Customer** accounts only (Admin accounts
  come from seed data).
- Access tokens are JWTs (15 min) carrying `nameidentifier`, `email` and `role`
  claims; validation checks issuer, audience, lifetime and signing key.
- Refresh tokens are 512-bit random values; only their SHA-256 hash is stored.
  Each refresh **rotates** the token (old one is marked revoked). Expired,
  revoked or unknown refresh tokens → 401.
- Authorization: `[Authorize(Roles = "Admin")]` for admin endpoints; ownership
  checks for customer-specific orders (a customer reading someone else's order
  gets 403).
- The frontend attaches the access token to every request and, on 401, runs a
  **single-flight refresh** (one refresh call even if many requests fail at
  once), then retries the original request **at most once**. If the refresh
  fails, auth state is cleared and the user is sent to login.
- Frontend route guards (`ProtectedRoute`, `RoleGuard`) are UX only — the
  backend re-checks every request.

## Discount calculation rules

All money math is `decimal`, server-side, rounded to 2 decimals with
`MidpointRounding.AwayFromZero`.

1. **Subtotal** = Σ(line total), where line total = unit price (from the DB,
   never from the client) × quantity, each line rounded to 2 decimals.
2. **5% discount** when subtotal is **strictly greater than 10,000**
   (10,000.00 exactly → no discount).
3. **Fifth-order discount**: an additional 10% on every fifth order of that
   customer. The sequence is 1-based and **includes the order being created**:
   the service counts the customer's existing orders (any status) and adds 1.
   Orders 5, 10, 15… qualify. When both discounts apply, the 10% is computed on
   the amount **remaining after** the 5% discount
   (`discount = round(0.05 × subtotal) + round(0.10 × (subtotal − 5% part))`).
4. **Total** = subtotal − discount, persisted on the order. Clients cannot
   override any money value.

*Assumption (documented deliberately):* the order sequence counts all orders
ever created by the customer **including cancelled ones**, so sequence numbers
never shift retroactively.

## Stock concurrency handling

- `Product.RowVersion` is a SQL Server `rowversion` column (`IsRowVersion()`).
- Order creation loads products **inside** a transaction, validates stock,
  decrements it, inserts the order and saves everything in one
  `SaveChanges` — the UPDATE carries `WHERE RowVersion = @original`.
- Two simultaneous requests for the last unit: one commits (201), the other
  gets `DbUpdateConcurrencyException` → **409 Conflict** with the message
  "Product stock was changed by another concurrent order. No order was
  created; please retry." Stock can never go negative (rowversion + a
  `StockQuantity >= 0` CHECK constraint).
- Failed orders leave zero partial state: the transaction is rolled back.
- The same mechanism protects concurrent product edits (admin PUT) and
  concurrent order status changes.

## Idempotency behavior

- `POST /api/orders` **requires** the `Idempotency-Key` header (any non-empty
  string, recommended: a UUID generated by the client). Missing → 400.
- The `(CustomerId, IdempotencyKey)` pair is unique in the database
  (`UX_Orders_Customer_IdempotencyKey`).
- Replay with the **same key + equivalent payload** → HTTP **200** with the
  originally created order; **no** duplicate order, **no** extra stock
  reservation. Payload equivalence is a SHA-256 hash of the item set
  (order-insensitive).
- Same key with a **different payload** → HTTP **409** ("This Idempotency-Key
  was already used with a different payload").
- Two concurrent requests with the same key: one creates the order, the other
  hits the unique index and is answered with the winning order (or 409 on
  payload mismatch).
- **Failed requests do not consume the key** — nothing is persisted when
  validation fails, so the client can fix the cart and retry with the same key.
- Frontend: the checkout page generates one key per checkout attempt, reuses it
  across retries of the same cart (stored in `sessionStorage` with a cart
  fingerprint), regenerates it when the cart changes, and clears the cart only
  after a successful (201) response.

## Order cancellation behavior

- Allowed transitions: `Pending → Confirmed`, `Pending → Cancelled`,
  `Confirmed → Shipped`, `Confirmed → Cancelled`. Everything else → 400 with
  the allowed transitions listed.
- Admins can perform any allowed transition; customers may only **cancel their
  own** Pending/Confirmed orders (anything else → 403).
- Cancellation restores stock inside the same transaction as the status change.
  The `Order.RowVersion` token guarantees the restore happens **exactly once**:
  a second cancel (sequential) fails the state machine with 400; a concurrent
  cancel fails with 409 and rolls back its duplicate restore.
- `PendingOrderCancellationService` (a `BackgroundService`) runs **every 60
  seconds**, finds `Pending` orders older than **30 minutes**, and cancels them
  with the same transactional stock-restore logic. Each order is processed in
  its own transaction; failures are logged per-order and never stop the
  service; concurrency conflicts are treated as "already handled by someone
  else".

## Test commands and actual test results

```bash
# Backend (build + all tests)
dotnet build OrderInventory.sln
dotnet test OrderInventory.sln

# Backend unit tests only
dotnet test Tests/UnitTests/UnitTests.csproj

# Integration tests (SQL Server concurrency tests need TEST_SQL_CONNECTION_STRING)
dotnet test Tests/IntegrationTests/IntegrationTests.csproj

# Frontend
cd Frontend
npm install
npm test          # Vitest + React Testing Library
npm run build     # type-check + production bundle
```

**Actual verification results** (executed on Linux x64, .NET SDK 8.0.426,
Node v24.21.0 — see "known limitations" for what could not run in this
environment):

| Check | Result |
|---|---|
| `dotnet restore OrderInventory.sln` | ✅ Passed |
| `dotnet build OrderInventory.sln` | ✅ 0 errors, 0 warnings |
| `dotnet test Tests/UnitTests/UnitTests.csproj` | ✅ **Passed: 58, Failed: 0** (58 total, ~1 s) |
| `dotnet test Tests/IntegrationTests/IntegrationTests.csproj` | ✅ **Passed: 2, Skipped: 2, Failed: 0** — the 2 SQL Server concurrency tests **skip with an explicit message** when `TEST_SQL_CONNECTION_STRING` is not configured (see below) |
| `npm install` (Frontend) | ✅ Passed (no vulnerabilities blocking) |
| `npm run build` (Frontend) | ✅ Passed (tsc strict type-check + vite build, 122 modules) |
| `npm test` (Frontend) | ✅ **6/6 passed** (2 test files) |

Details of the integration tests:

- `GetProducts_WithoutToken_Returns401Unauthorized` — ✅ passed (no DB needed).
- `Login_SixRapidRequests_SixthIsRateLimited429` — ✅ passed (verifies the
  built-in rate limiter returns 429 on the 6th login request per minute/IP).
- `TwoConcurrentOrdersForLastUnit_ExactlyOneSucceeds_OneConflicts` and
  `TwoConcurrentOrdersForLastUnit_FinalStateIsConsistent` — ⏭️ **skipped** in
  this environment because no SQL Server instance is available. These tests
  deliberately **do not** run against the InMemory/SQLite providers, since
  neither reproduces SQL Server `rowversion` semantics — faking a pass would be
  dishonest. They run wherever a test database is provided:

  ```bash
  # Create an empty database (e.g. OrderInventoryTests) and point the env var at it:
  export TEST_SQL_CONNECTION_STRING="Server=localhost,1433;Database=OrderInventoryTests;User Id=sa;Password=Your_password123;TrustServerCertificate=True"
  dotnet test Tests/IntegrationTests/IntegrationTests.csproj
  ```

  On start the tests apply migrations, wipe the test tables, seed one customer
  and one product with **stock 1**, then fire two concurrent order requests and
  assert: exactly one 201, exactly one 409, final stock 0, and exactly one
  order created.

Frontend tests cover: ProductCard rendering (price, in/out-of-stock badges,
add-to-cart disabled state) and cart behavior (add/merge/remove/clear,
subtotal math, localStorage persistence).

## Generated SQL logging

- In Development, `appsettings.Development.json` sets
  `Microsoft.EntityFrameworkCore.Database.Command: Information`, so **every SQL
  query is printed to the console** while the API runs.
- The top-customers report additionally logs its own generated SQL via
  `IQueryable.ToQueryString()` (visible in the API console output when
  `GET /api/reports/top-customers` is called).
- `QUERIES.sql` contains the hand-written SQL Server equivalents for the five
  reporting/analysis queries required by the test, with the same
  "qualifying order" definition used by the API (`Status IN ('Confirmed','Shipped')`).

## Authentication & token storage trade-offs

The React app stores the access and refresh tokens in `localStorage`
(documented decision, kept deliberately simple for a machine test):

- ✅ Simple, survives page reloads, no backend changes.
- ❌ **Not secure against XSS** — any successfully injected script could read
  the tokens. This is a real trade-off, not a claim of full security.
- Production-grade alternatives: httpOnly + Secure + SameSite cookies (with
  CSRF protection), or BFF-style sessions; both require backend changes.

Other hardening already in place: passwords hashed (PBKDF2), refresh tokens
stored hashed + rotated, generic login errors, login rate limiting, no secrets
returned by any endpoint, and no stack traces in error responses.

## Assumptions and known limitations

**Assumptions**

1. The fifth-order discount sequence counts **all** of a customer's orders
   (including cancelled ones); documented in the discount rules section.
2. "Qualifying orders" for reports = `Confirmed` or `Shipped`; used
   consistently by the API and `QUERIES.sql`.
3. `Order` also carries a `RowVersion` column (beyond the listed entity
   fields) — it is the mechanism that makes concurrent status changes and
   single-stock-restoration safe.
4. An admin-only `GET /api/orders` (paged list of all orders) was added because
   the required "Admin Order Management" page needs it.
5. Replay of a successful idempotent request returns HTTP 200 (not 201) with
   the existing order.
6. Ownership denial returns 403 (rather than masking with 404).
7. Registration always creates Customer accounts; admin accounts are seeded.

**Known limitations**

1. **No SQL Server in the verification environment.** All unit tests (58) ran
   for real against the SQLite relational provider; the two concurrent-order
   integration tests are fully implemented but **skip without a real SQL
   Server** (by design — see test results). Everything else was verified.
2. The fifth-order discount reads the customer's order count inside the
   transaction without a lock; two *simultaneous* orders by the same customer
   could observe the same count (stock integrity is unaffected — only the
   sequence number could tie, and both orders would still be valid).
3. Tokens in `localStorage` (trade-off documented above).
4. Product deletes are not exposed by design — products are deactivated
   (`IsActive = false`) instead, which keeps order history intact
   (`OrderItem → Product` uses `Restrict` delete behaviour).
5. `IMemoryCache` is per-process; in a multi-instance deployment the
   version-based invalidation would need a distributed cache/eventing.
6. The Docker Compose file is a convenience for SQL Server only; it does not
   containerize the API/frontend (the test allows running locally).
7. The sample JWT key and seeded passwords are development-only and public in
   this repository — replace both before any real deployment.
