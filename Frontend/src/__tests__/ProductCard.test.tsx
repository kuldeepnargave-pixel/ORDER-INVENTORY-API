import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import ProductCard from '../components/ProductCard';
import type { Product } from '../models';

const inStockProduct: Product = {
  id: 1,
  sku: 'SKU-001',
  name: 'Wireless Mouse',
  description: 'Ergonomic wireless mouse.',
  price: 24.99,
  stockQuantity: 12,
  isActive: true
};

describe('ProductCard', () => {
  it('renders product name, formatted price and in-stock badge', () => {
    render(<ProductCard product={inStockProduct} onAddToCart={vi.fn()} />);

    expect(screen.getByText('Wireless Mouse')).toBeInTheDocument();
    expect(screen.getByText('$24.99')).toBeInTheDocument();
    expect(screen.getByText(/in stock \(12\)/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /add to cart/i })).toBeEnabled();
  });

  it('disables add-to-cart and shows out-of-stock badge when stock is zero', () => {
    render(
      <ProductCard product={{ ...inStockProduct, stockQuantity: 0 }} onAddToCart={vi.fn()} />
    );

    expect(screen.getByText(/out of stock/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /add to cart/i })).toBeDisabled();
  });

  it('calls onAddToCart with the product when the button is clicked', () => {
    const onAddToCart = vi.fn();
    render(<ProductCard product={inStockProduct} onAddToCart={onAddToCart} />);

    fireEvent.click(screen.getByRole('button', { name: /add to cart/i }));
    expect(onAddToCart).toHaveBeenCalledTimes(1);
    expect(onAddToCart).toHaveBeenCalledWith(inStockProduct);
  });
});
