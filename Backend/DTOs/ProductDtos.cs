using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs;

public class ProductDto
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; }
}

public class ProductCreateRequest
{
    [Required, MaxLength(50)]
    public string Sku { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(0, 9_999_999, ErrorMessage = "Price cannot be negative.")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Stock quantity cannot be negative.")]
    public int StockQuantity { get; set; }

    public bool IsActive { get; set; } = true;
}

public class ProductUpdateRequest
{
    [Required, MaxLength(50)]
    public string Sku { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(0, 9_999_999, ErrorMessage = "Price cannot be negative.")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Stock quantity cannot be negative.")]
    public int StockQuantity { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Server-side query parameters for product listing.
/// Filtering/sorting/pagination are translated to SQL via IQueryable.
/// </summary>
public class ProductQueryParameters
{
    public string? Search { get; set; }

    [Range(0, 9_999_999)]
    public decimal? MinPrice { get; set; }

    [Range(0, 9_999_999)]
    public decimal? MaxPrice { get; set; }

    /// <summary>true = only products with StockQuantity > 0.</summary>
    public bool? InStock { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "page must be 1 or greater.")]
    public int Page { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "pageSize must be between 1 and 100.")]
    public int PageSize { get; set; } = 10;

    /// <summary>One of: name (default), price, stockQuantity, createdAt. Unknown values fall back to name.</summary>
    public string SortBy { get; set; } = "name";

    /// <summary>asc (default) or desc.</summary>
    public string SortDirection { get; set; } = "asc";
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
}
