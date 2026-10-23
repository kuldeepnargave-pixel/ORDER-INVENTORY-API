# REVIEW.md — Common Production Issues and How This Project Addresses Them

This document reviews common production issues in order/inventory systems and
explains, for each: (1) what the problem is, (2) why it matters, and (3) how the
current implementation prevents it.

> Note per the machine-test instructions: no separate faulty code snippet was
> supplied with the test, so this review covers the canonical issues listed in
> the test brief and documents how each one is prevented (or should be
> corrected) in this codebase.

---

## 1. Missing database transactions

**Problem.** Multi-step writes (create order + decrement stock for N products)
executed as several independent `SaveChanges` calls with no transaction. A
failure halfway through leaves the database in a half-applied state: stock was
reserved but no order exists (or vice versa).

**Why it matters.** Inventory silently drifts away from reality; customers are
charged for orders that do not exist; reconciliation becomes manual work.

**How it is handled.** `OrderService.CreateOrderAsync` and
`UpdateStatusAsync` open an explicit transaction
(`_db.Database.BeginTransactionAsync()`) and every write in the use case —
stock decrements, the order row, order items — is committed with a single
`SaveChanges` inside that transaction, then `CommitAsync()`. Any thrown
exception disposes the transaction and rolls everything back. The unit test
`CreateOrder_DatabaseFailureMidSave_RollsBackStockAndOrder` proves it: the
database itself rejects the save midway, and both the stock reservation and the
order are gone afterwards.

## 2. Inventory race conditions (overselling)

**Problem.** Classic read-then-write: `SELECT stock` (10), decide it is enough,
`UPDATE stock = stock - 10`. Two concurrent requests both read 10 and both
decrement, selling stock that does not exist (or driving it negative).

**Why it matters.** You sell units you cannot ship; refunds, cancellations and
angry customers follow. Stock can even become negative, which breaks reporting.

**How it is handled.** Stock validation happens on loaded entities, and the
decrement is persisted through EF Core's optimistic concurrency: the generated
`UPDATE Products SET StockQuantity = ... WHERE Id = ... AND RowVersion = @original`.
If a competing request already changed the row, the UPDATE affects 0 rows and EF
throws `DbUpdateConcurrencyException`, which the service converts to
HTTP 409 — the whole transaction rolls back. Additionally, database CHECK
constraints (`CK_Products_Stock_NonNegative`) make negative stock impossible
even if application logic regressed. The concurrency integration test
(`TwoConcurrentOrdersForLastUnit_*`) asserts exactly one 201, one 409, final
stock 0 on a real SQL Server.

## 3. Missing optimistic concurrency checks

**Problem.** Entities are updated with "last write wins" and no version token.
Two admins edit the same product; both saves succeed and one edit silently
disappears.

**Why it matters.** Silent data loss is worst in exactly the high-contention
paths that matter most (stock, prices, order status).

**How it is handled.** `Product` and `Order` both carry a SQL Server
`rowversion` column (`Product.RowVersion`, `Order.RowVersion`) configured with
EF Core `IsRowVersion()`. Every UPDATE is conditional on the version read;
stale writers receive `DbUpdateConcurrencyException` (HTTP 409). Unit tests
additionally exercise the same token semantics against SQLite, and the
integration tests cover the real SQL Server behaviour.

## 4. Duplicate order creation (double submit / retries)

**Problem.** A user double-clicks "Place order", or an HTTP client retries a
timed-out request, and two identical orders are created and stock reserved
twice.

**Why it matters.** Direct revenue impact, double stock reservation, support
tickets, and broken analytics.

**How it is handled.** `POST /api/orders` requires an `Idempotency-Key` header
(HTTP 400 if missing). The `(CustomerId, IdempotencyKey)` pair has a unique
database index (`UX_Orders_Customer_IdempotencyKey`), which makes duplicates
impossible even under concurrency. Before inserting, the service checks for an
existing order with the same key: same payload → return the existing order
(HTTP 200, no new stock reservation); different payload → HTTP 409. If two
concurrent requests race past the check, one insert hits the unique index; the
service detects this, re-reads the winning order and returns it. Failed
requests never consume a key (nothing is persisted), so the client can safely
retry.

## 5. Unsafe status transitions

**Problem.** Status is updated blindly: `PATCH` sets whatever the client sends,
so an order can go Cancelled → Shipped or Pending → Shipped, and cancellation
can happen after shipping.

**Why it matters.** Impossible states corrupt fulfilment, shipping and refund
flows; stock can be restored for orders that were already shipped.

**How it is handled.** `OrderStatusTransitions` encodes the state machine
(Pending → Confirmed/Cancelled, Confirmed → Shipped/Cancelled; Shipped and
Cancelled are terminal). `UpdateStatusAsync` validates the transition before
touching anything and rejects illegal moves with HTTP 400 and a message that
lists the allowed transitions. Unit tests cover all four legal transitions and
ten illegal ones.

## 6. Duplicate stock restoration on cancellation

**Problem.** Cancellation adds quantities back to stock, but nothing prevents
the restore from running twice — e.g. the user cancels while the auto-cancel
background job is processing the same order, or the cancel endpoint is called
twice concurrently.

**Why it matters.** Inflated stock = phantom inventory = overselling later.

**How it is handled.** Three layers: (a) the state machine makes `Cancelled` a
terminal status, so a sequential second cancel is rejected with 400 before any
stock math; (b) the status check + stock restore + status write happen in one
transaction guarded by the `Order.RowVersion` token, so two concurrent cancels
cannot both apply — the loser gets 409 and its (non-existent) restore is rolled
back; (c) the `PendingOrderCancellationService` re-reads the order inside its
transaction and skips anything no longer `Pending`, and treats concurrency
exceptions as "already handled". Unit tests assert stock is restored exactly
once, and the double-cancel attempt leaves stock untouched.

## 7. Incorrect discount calculation

**Problem.** Discounts computed on the client, or the 10% fifth-order discount
applied to the pre-discount subtotal, or floating-point arithmetic causing
rounding drift (0.1 + 0.2 != 0.3), or inconsistent rounding per line.

**Why it matters.** Money errors compound: every order slightly wrong, audits
fail, and trust is lost.

**How it is handled.** All money math is `decimal`, server-side only. Rules are
centralized in `DiscountCalculator`: 5% when subtotal > 10,000 (strictly), an
additional 10% on every fifth order calculated on the amount remaining *after*
the subtotal discount, all rounded to 2 decimals with midpoint-away-from-zero.
The unit tests pin exact expected values, including a midpoint case (0.225 →
0.23) that distinguishes away-from-zero from banker's rounding.

## 8. Invalid input and null handling

**Problem.** Requests are trusted: negative quantities, zero/negative prices,
null item lists, unbounded page sizes (a client asking for `pageSize=1000000`
can take the database down).

**Why it matters.** Bad data poisons every downstream report; unbounded queries
are a denial-of-service vector.

**How it is handled.** Data annotations on every DTO (`[Required]`, `[Range]`,
`[EmailAddress]`, `[MinLength]`, `pageSize` capped at 100) with
`[ApiController]` returning automatic 400 ValidationProblemDetails; service-side
validation for cross-field rules (duplicate product references, quantity > 0,
inactive products, sufficient stock, customer existence); database CHECK
constraints as the last line of defence; nullable references enabled project-wide.

## 9. Missing authorization and ownership checks

**Problem.** Endpoints check authentication but not authorization: any logged-in
user can read other customers' orders, or a customer can confirm their own order
(bypassing the shop's confirmation flow).

**Why it matters.** Data breach (IDOR) and business-flow bypass.

**How it is handled.** Role-based authorization (`[Authorize(Roles = Admin)]`)
on product create/update, the admin order list, and all report endpoints; an
explicit ownership check (`order.CustomerId == current user`) for order read and
status change, returning 403 otherwise; customers may only *cancel* — every
other transition is admin-only. The frontend guards (`ProtectedRoute`,
`RoleGuard`) exist for UX only and are never treated as security. Unit tests
cover cross-customer access and customer-vs-admin transition rights.

## 10. SQL injection risks

**Problem.** Search terms or sort fields concatenated into SQL strings
(`$"SELECT ... WHERE Name LIKE '%{search}%'"`).

**Why it matters.** One of the most exploited vulnerability classes: data
exfiltration and tampering.

**How it is handled.** All data access is LINQ-to-Entities, which produces
parameterized SQL; there is no string-built SQL anywhere in the codebase. Sort
fields are mapped through a whitelist (`price`, `stockQuantity`, `createdAt`,
`name`) rather than interpolated. `QUERIES.sql` exists for reference/ops use and
is never executed with user input.

## 11. Unhandled exceptions

**Problem.** Exceptions bubble out as raw 500 responses with stack traces,
connection strings, or SQL error text; different failure modes all look the
same to the client.

**Why it matters.** Leaks internals that help attackers; makes API behaviour
unpredictable for client developers.

**How it is handled.** `ExceptionHandlingMiddleware` maps known exceptions to
consistent RFC 7807 `application/problem+json` responses (400 validation,
401/403 auth, 404, 409 conflict/concurrency, 429 via the rate limiter). For
unhandled exceptions it returns a generic 500 body and logs the full exception
server-side. No stack traces, tokens, passwords, or SQL details ever reach the
client.

## 12. Missing indexes and constraints

**Problem.** No unique constraint on email/SKU/idempotency keys, no indexes on
foreign keys, no check constraints — correctness depends entirely on
application code that can (and will) be bypassed.

**Why it matters.** Duplicated accounts/products, full-table scans, and corrupt
data that no code review catches.

**How it is handled.** The EF Core model (and the generated migration) defines:
unique indexes on `Users.Email`, `Products.Sku`, `RefreshTokens.TokenHash`, and
`(Orders.CustomerId, Orders.IdempotencyKey)`; indexes on `OrderItems.ProductId`
and refresh-token lookups; CHECK constraints for non-negative
price/stock/money fields and positive quantities; deliberate delete behaviours
(Cascade for owned children, Restrict for product/customer references).

## 13. Token handling and credential security

**Problem.** Plain-text passwords in the database, refresh tokens stored
verbatim, tokens that never expire, secrets committed to source control, and
error messages that reveal whether an email exists.

**Why it matters.** Credential stuffing, replay attacks, and lateral movement
after any database leak.

**How it is handled.** Passwords hashed with ASP.NET Core's
`PasswordHasher<User>` (PBKDF2); refresh tokens stored only as SHA-256 hashes
and rotated on every use (used tokens are marked revoked); access tokens expire
in 15 minutes with issuer/audience/lifetime validation; login failures return a
single generic message; login is rate limited (5/min/IP → 429); the JWT key in
`appsettings.json` is an explicitly labelled development-only value, and the
README explains how to replace it. **Documented trade-off:** the React app
stores tokens in `localStorage` for simplicity — that is *not* immune to XSS;
an httpOnly-cookie design is the production-grade alternative.

## 14. Inadequate logging and testing

**Problem.** No structured logging (incidents are undebuggable), or worse,
sensitive data in logs; no tests, so every business-rule regression ships
silently.

**Why it matters.** You cannot operate what you cannot observe, and you cannot
change what you are afraid to touch.

**How it is handled.** Significant operations are logged (order creation with
totals, status changes with actor, auto-cancellations, failed logins) with
cancellation scans and per-order failures logged without stopping the service;
the top-customers report logs its generated SQL (`ToQueryString()`) and
development enables full EF SQL logging. Testing: 58 xUnit unit tests cover
discount math (thresholds, fifth order, rounding), status transitions,
idempotent replay and payload mismatch, rollback, stock restoration, and
concurrency tokens — run against the real `OrderService` with a relational
provider; plus WebApplicationFactory integration tests for auth (401), rate
limiting (429) and the two-concurrent-orders race (requires SQL Server, skipped
with an explicit message when unavailable). Frontend behaviour is covered by
Vitest + React Testing Library tests.
