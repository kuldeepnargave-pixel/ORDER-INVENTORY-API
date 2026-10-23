using Backend.Data;
using Backend.DTOs;
using Backend.Helpers;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Backend.Services;

/// <summary>
/// Bump this version whenever a product is created/updated; every cached
/// product-list key embeds the current version, so bumping invalidates all
/// previously cached lists in one operation (IMemoryCache cannot be enumerated).
/// </summary>
public interface ICacheVersionProvider
{
    int GetVersion();
    void Bump();
}

public class CacheVersionProvider : ICacheVersionProvider
{
    private const string CacheKey = "products:cache-version";
    private readonly IMemoryCache _cache;

    public CacheVersionProvider(IMemoryCache cache) => _cache = cache;

    public int GetVersion() =>
        _cache.GetOrCreate(CacheKey, entry =>
        {
            entry.Priority = CacheItemPriority.NeverRemove;
            return 1;
        });

    public void Bump() => _cache.Set(CacheKey, GetVersion() + 1);
}

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetProductsAsync(ProductQueryParameters query);
    Task<ProductDto?> GetProductByIdAsync(int id);
    Task<ProductDto> CreateProductAsync(ProductCreateRequest request);
    Task<ProductDto> UpdateProductAsync(int id, ProductUpdateRequest request);
}

public class ProductService : IProductService
{
    private const string ListCachePrefix = "products:list:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ICacheVersionProvider _cacheVersion;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        AppDbContext db,
        IMemoryCache cache,
        ICacheVersionProvider cacheVersion,
        ILogger<ProductService> logger)
    {
        _db = db;
        _cache = cache;
        _cacheVersion = cacheVersion;
        _logger = logger;
    }

    public async Task<PagedResult<ProductDto>> GetProductsAsync(ProductQueryParameters query)
    {
        var cacheKey = BuildCacheKey(query);
        if (_cache.TryGetValue<PagedResult<ProductDto>>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        // Everything below is IQueryable and executes in SQL Server:
        // WHERE (search/filters) -> COUNT -> ORDER BY -> OFFSET/FETCH.
        var products = _db.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            products = products.Where(p =>
                p.Name.Contains(term) ||
                p.Sku.Contains(term) ||
                (p.Description != null && p.Description.Contains(term)));
        }

        if (query.MinPrice.HasValue)
        {
            products = products.Where(p => p.Price >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            products = products.Where(p => p.Price <= query.MaxPrice.Value);
        }

        if (query.InStock == true)
        {
            products = products.Where(p => p.StockQuantity > 0);
        }

        var totalCount = await products.CountAsync();

        var items = await ApplySorting(products, query.SortBy, query.SortDirection)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Sku = p.Sku,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                IsActive = p.IsActive
            })
            .ToListAsync();

        var result = new PagedResult<ProductDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };

        // The cache holds only catalog data derived from query parameters -
        // no user-specific or authorization-sensitive information.
        _cache.Set(cacheKey, result, CacheDuration);
        return result;
    }

    public async Task<ProductDto?> GetProductByIdAsync(int id)
    {
        return await _db.Products.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Sku = p.Sku,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                IsActive = p.IsActive
            })
            .FirstOrDefaultAsync();
    }

    public async Task<ProductDto> CreateProductAsync(ProductCreateRequest request)
    {
        var sku = request.Sku.Trim();
        if (await _db.Products.AnyAsync(p => p.Sku == sku))
        {
            throw new ConflictException($"A product with SKU '{sku}' already exists.");
        }

        var product = new Product
        {
            Sku = sku,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            IsActive = request.IsActive
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        _cacheVersion.Bump();

        _logger.LogInformation("Product created: {Sku} (Id {ProductId})", product.Sku, product.Id);
        return ToDto(product);
    }

    public async Task<ProductDto> UpdateProductAsync(int id, ProductUpdateRequest request)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id)
                      ?? throw new NotFoundException($"Product {id} was not found.");

        var sku = request.Sku.Trim();
        if (await _db.Products.AnyAsync(p => p.Sku == sku && p.Id != id))
        {
            throw new ConflictException($"A product with SKU '{sku}' already exists.");
        }

        product.Sku = sku;
        product.Name = request.Name.Trim();
        product.Description = request.Description?.Trim();
        product.Price = request.Price;
        product.StockQuantity = request.StockQuantity;
        product.IsActive = request.IsActive;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The product was modified by another request. Please reload and try again.");
        }

        _cacheVersion.Bump();
        _logger.LogInformation("Product updated: {Sku} (Id {ProductId})", product.Sku, product.Id);
        return ToDto(product);
    }

    private static IQueryable<Product> ApplySorting(IQueryable<Product> query, string? sortBy, string? sortDirection)
    {
        var sortField = (sortBy ?? "name").Trim().ToLowerInvariant();
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortField switch
        {
            "price" => descending ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
            "stockquantity" or "stock" => descending ? query.OrderByDescending(p => p.StockQuantity) : query.OrderBy(p => p.StockQuantity),
            "createdat" => descending ? query.OrderByDescending(p => p.Id) : query.OrderBy(p => p.Id),
            _ => descending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name)
        };
    }

    private string BuildCacheKey(ProductQueryParameters query) =>
        $"{ListCachePrefix}v{_cacheVersion.GetVersion()}:" +
        $"{query.Page}:{query.PageSize}:{query.Search?.Trim().ToLowerInvariant()}:" +
        $"{query.MinPrice}:{query.MaxPrice}:{query.InStock}:{query.SortBy}:{query.SortDirection}";

    private static ProductDto ToDto(Product product) => new()
    {
        Id = product.Id,
        Sku = product.Sku,
        Name = product.Name,
        Description = product.Description,
        Price = product.Price,
        StockQuantity = product.StockQuantity,
        IsActive = product.IsActive
    };
}
