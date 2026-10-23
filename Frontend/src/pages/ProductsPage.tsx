import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { PagedResult, Product } from '../models';
import { productService } from '../services/productService';
import { getApiErrorMessage } from '../services/errorUtils';
import { useDebounce } from '../hooks/useDebounce';
import { useCart } from '../context/CartContext';
import ProductCard from '../components/ProductCard';
import SearchFilters from '../components/SearchFilters';
import Pagination from '../components/Pagination';
import Loading from '../components/Loading';
import EmptyState from '../components/EmptyState';
import ErrorMessage from '../components/ErrorMessage';

/**
 * Product catalog: search (300 ms debounce), price/stock filters, sorting and
 * pagination are all reflected in the URL query string and executed
 * server-side by the API.
 */
export default function ProductsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const { addItem } = useCart();

  const searchFromUrl = searchParams.get('search') ?? '';
  const [searchInput, setSearchInput] = useState(searchFromUrl);
  const debouncedSearch = useDebounce(searchInput, 300);

  const page = Math.max(1, Number(searchParams.get('page') ?? '1') || 1);
  const pageSize = 9;
  const minPrice = searchParams.get('minPrice') ?? '';
  const maxPrice = searchParams.get('maxPrice') ?? '';
  const inStock = searchParams.get('inStock') === 'true';
  const sortBy = searchParams.get('sortBy') ?? 'name';
  const sortDirection = (searchParams.get('sortDirection') as 'asc' | 'desc') ?? 'asc';

  const [result, setResult] = useState<PagedResult<Product> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  // Push the debounced search term into the URL (resetting the page).
  useEffect(() => {
    if (debouncedSearch !== searchFromUrl) {
      updateParams({ search: debouncedSearch || null, page: null });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debouncedSearch]);

  // Fetch whenever the URL query changes.
  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError('');

    productService
      .getProducts({
        search: searchFromUrl || undefined,
        minPrice: minPrice ? Number(minPrice) : undefined,
        maxPrice: maxPrice ? Number(maxPrice) : undefined,
        inStock: inStock || undefined,
        page,
        pageSize,
        sortBy,
        sortDirection
      })
      .then((data) => {
        if (!cancelled) setResult(data);
      })
      .catch((err) => {
        if (!cancelled) setError(getApiErrorMessage(err));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams.toString()]);

  function updateParams(changes: Record<string, string | null>) {
    const next = new URLSearchParams(searchParams);
    for (const [key, value] of Object.entries(changes)) {
      if (value === null || value === '') {
        next.delete(key);
      } else {
        next.set(key, value);
      }
    }
    setSearchParams(next, { replace: true });
  }

  const handleAddToCart = (product: Product) => addItem(product);

  return (
    <div className="page">
      <div className="page-header">
        <h1>Product catalog</h1>
        <p className="page-subtitle">Browse the catalog, filter by price and availability, and add items to your cart.</p>
      </div>

      <SearchFilters
        search={searchInput}
        minPrice={minPrice}
        maxPrice={maxPrice}
        inStock={inStock}
        sortBy={sortBy}
        sortDirection={sortDirection}
        onSearchChange={setSearchInput}
        onPriceFilterChange={(min, max) => updateParams({ minPrice: min, maxPrice: max, page: null })}
        onInStockChange={(checked) => updateParams({ inStock: checked ? 'true' : null, page: null })}
        onSortChange={(newSortBy, newDirection) =>
          updateParams({ sortBy: newSortBy === 'name' ? null : newSortBy, sortDirection: newDirection === 'asc' ? null : newDirection, page: null })
        }
      />

      {loading && <Loading label="Loading products…" />}
      {!loading && error && <ErrorMessage message={error} hint="Adjust the filters or try again." />}
      {!loading && !error && result && result.items.length === 0 && (
        <EmptyState
          title="No products found"
          message="No products match the current search and filters. Try clearing them."
        />
      )}

      {!loading && !error && result && result.items.length > 0 && (
        <>
          <div className="product-grid">
            {result.items.map((product) => (
              <ProductCard key={product.id} product={product} onAddToCart={handleAddToCart} />
            ))}
          </div>
          <Pagination
            page={result.page}
            totalPages={result.totalPages}
            totalCount={result.totalCount}
            onChange={(nextPage) => updateParams({ page: String(nextPage) })}
          />
        </>
      )}
    </div>
  );
}
