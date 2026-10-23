using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService) => _productService = productService;

    /// <summary>
    /// Server-side search / filter / sort / pagination of the product catalog.
    /// Results are cached with IMemoryCache and invalidated on create/update.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetProducts([FromQuery] ProductQueryParameters query)
    {
        return Ok(await _productService.GetProductsAsync(query));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> GetProduct(int id)
    {
        var product = await _productService.GetProductByIdAsync(id);
        return product is null ? NotFound() : Ok(product);
    }

    /// <summary>Create a product. Admin only.</summary>
    [HttpPost]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<ProductDto>> CreateProduct(ProductCreateRequest request)
    {
        var created = await _productService.CreateProductAsync(request);
        return CreatedAtAction(nameof(GetProduct), new { id = created.Id }, created);
    }

    /// <summary>Update a product. Admin only. Concurrent edits return HTTP 409.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<ProductDto>> UpdateProduct(int id, ProductUpdateRequest request)
    {
        return Ok(await _productService.UpdateProductAsync(id, request));
    }
}
