-- ============================================================================
-- QUERIES.sql — SQL Server queries for the Order & Inventory Management System
--
-- Conventions shared with the API:
--   * "Qualifying orders" for revenue reporting = status IN ('Confirmed', 'Shipped').
--     Pending orders are not yet fulfilled; Cancelled orders were never paid.
--     The API (ReportService.QualifyingStatuses) uses exactly the same set.
-- ============================================================================


-- ----------------------------------------------------------------------------
-- Query 1: Top 5 customers by spending on qualifying orders (Confirmed/Shipped)
--          created during the last 30 days.
-- ----------------------------------------------------------------------------
SELECT TOP (5)
    u.Id            AS CustomerId,
    u.FullName,
    u.Email,
    COUNT(o.Id)     AS OrderCount,
    SUM(o.Total)    AS TotalSpent
FROM Orders o
JOIN Users u ON u.Id = o.CustomerId
WHERE o.Status IN ('Confirmed', 'Shipped')              -- qualifying statuses (same as the API)
  AND o.CreatedAt >= DATEADD(DAY, -30, SYSUTCDATETIME()) -- last 30 days (UTC)
GROUP BY u.Id, u.FullName, u.Email
ORDER BY TotalSpent DESC;


-- ----------------------------------------------------------------------------
-- Query 2: Products that have never appeared in any order item.
--          (LEFT JOIN / NOT EXISTS both work; NOT EXISTS reads clearly and
--          lets the optimizer use an anti-semi join on the indexed FK.)
-- ----------------------------------------------------------------------------
SELECT p.Id, p.Sku, p.Name, p.Price, p.StockQuantity, p.IsActive
FROM Products p
WHERE NOT EXISTS (
    SELECT 1 FROM OrderItems oi WHERE oi.ProductId = p.Id
)
ORDER BY p.Name;


-- ----------------------------------------------------------------------------
-- Query 3: Monthly revenue for the current calendar year with a running total
--          (window function). Revenue counts qualifying orders only.
-- ----------------------------------------------------------------------------
WITH MonthlyRevenue AS (
    SELECT
        MONTH(o.CreatedAt) AS Month,
        SUM(o.Total)       AS Revenue
    FROM Orders o
    WHERE o.Status IN ('Confirmed', 'Shipped')
      AND YEAR(o.CreatedAt) = YEAR(GETUTCDATE())
    GROUP BY MONTH(o.CreatedAt)
)
SELECT
    Month,
    Revenue,
    SUM(Revenue) OVER (ORDER BY Month
                       ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS RunningTotal
FROM MonthlyRevenue
ORDER BY Month;


-- ----------------------------------------------------------------------------
-- Query 4: Each customer's second-highest order total.
--          * Customers with fewer than TWO qualifying orders are excluded.
--          * Ranking is based on DISTINCT totals (DENSE_RANK), so if a
--            customer's two biggest orders have the same total, that value is
--            still reported as their second-highest DISTINCT total. If you
--            instead want "the total of the second-largest order row", swap
--            DENSE_RANK() for ROW_NUMBER().
-- ----------------------------------------------------------------------------
WITH RankedOrders AS (
    SELECT
        o.CustomerId,
        u.FullName,
        o.Total,
        DENSE_RANK() OVER (PARTITION BY o.CustomerId ORDER BY o.Total DESC)      AS TotalRank,
        COUNT(*)     OVER (PARTITION BY o.CustomerId)                            AS QualifyingOrderCount
    FROM Orders o
    JOIN Users u ON u.Id = o.CustomerId
    WHERE o.Status IN ('Confirmed', 'Shipped')
)
SELECT
    CustomerId,
    FullName,
    Total AS SecondHighestTotal
FROM RankedOrders
WHERE TotalRank = 2
  AND QualifyingOrderCount >= 2
ORDER BY CustomerId;


-- ----------------------------------------------------------------------------
-- Query 5: Low-stock products.
--
-- Interpretation (documented as required by the machine test):
--   A product is "low stock" when its CURRENT stock is below the AVERAGE
--   quantity sold per order for that product (i.e. one typical order of that
--   product would consume more than the remaining stock, so it is likely to
--   run out imminently). Average quantity is computed over qualifying orders
--   (Confirmed/Shipped) to ignore cancelled demand. Products that have never
--   been ordered are excluded because no demand baseline exists for them.
-- ----------------------------------------------------------------------------
SELECT
    p.Id,
    p.Sku,
    p.Name,
    p.StockQuantity,
    AVG(oi.Quantity * 1.0) AS AvgQuantityPerOrder   -- * 1.0 forces decimal division
FROM Products p
JOIN OrderItems oi ON oi.ProductId = p.Id
JOIN Orders o ON o.Id = oi.OrderId AND o.Status IN ('Confirmed', 'Shipped')
GROUP BY p.Id, p.Sku, p.Name, p.StockQuantity
HAVING p.StockQuantity < AVG(oi.Quantity * 1.0)
ORDER BY p.StockQuantity ASC;
