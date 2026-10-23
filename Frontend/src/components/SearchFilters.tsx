import { useState } from 'react';

interface SearchFiltersProps {
  search: string;
  minPrice: string;
  maxPrice: string;
  inStock: boolean;
  sortBy: string;
  sortDirection: 'asc' | 'desc';
  onSearchChange: (value: string) => void;
  onPriceFilterChange: (min: string, max: string) => void;
  onInStockChange: (inStock: boolean) => void;
  onSortChange: (sortBy: string, sortDirection: 'asc' | 'desc') => void;
}

/**
 * Search + filter controls. The search input is debounced by the parent
 * (ProductsPage) before it updates the URL / triggers a new API call.
 */
export default function SearchFilters(props: SearchFiltersProps) {
  const [minPrice, setMinPrice] = useState(props.minPrice);
  const [maxPrice, setMaxPrice] = useState(props.maxPrice);

  const applyPriceFilter = () => props.onPriceFilterChange(minPrice, maxPrice);

  return (
    <section className="filters" aria-label="Product filters">
      <div className="filter-row">
        <input
          type="search"
          className="input filter-search"
          placeholder="Search products by name, SKU or description…"
          value={props.search}
          onChange={(e) => props.onSearchChange(e.target.value)}
          aria-label="Search products"
        />

        <select
          className="input filter-sort"
          value={props.sortBy}
          onChange={(e) => props.onSortChange(e.target.value, props.sortDirection)}
          aria-label="Sort by"
        >
          <option value="name">Sort by name</option>
          <option value="price">Sort by price</option>
          <option value="stockQuantity">Sort by stock</option>
        </select>

        <select
          className="input filter-sort"
          value={props.sortDirection}
          onChange={(e) => props.onSortChange(props.sortBy, e.target.value as 'asc' | 'desc')}
          aria-label="Sort direction"
        >
          <option value="asc">Ascending</option>
          <option value="desc">Descending</option>
        </select>
      </div>

      <div className="filter-row">
        <label className="filter-price">
          Min price
          <input
            type="number"
            className="input"
            min="0"
            step="0.01"
            placeholder="0"
            value={minPrice}
            onChange={(e) => setMinPrice(e.target.value)}
            onBlur={applyPriceFilter}
            onKeyDown={(e) => e.key === 'Enter' && applyPriceFilter()}
          />
        </label>
        <label className="filter-price">
          Max price
          <input
            type="number"
            className="input"
            min="0"
            step="0.01"
            placeholder="Any"
            value={maxPrice}
            onChange={(e) => setMaxPrice(e.target.value)}
            onBlur={applyPriceFilter}
            onKeyDown={(e) => e.key === 'Enter' && applyPriceFilter()}
          />
        </label>
        <button type="button" className="btn btn-secondary" onClick={applyPriceFilter}>
          Apply price
        </button>
        <label className="filter-checkbox">
          <input
            type="checkbox"
            checked={props.inStock}
            onChange={(e) => props.onInStockChange(e.target.checked)}
          />
          In stock only
        </label>
      </div>
    </section>
  );
}
