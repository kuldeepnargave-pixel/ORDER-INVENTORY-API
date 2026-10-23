import { describe, expect, it, vi, beforeEach } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { CartProvider, useCart } from '../context/CartContext';
import type { Product } from '../models';

const mouse: Product = {
  id: 1,
  sku: 'SKU-001',
  name: 'Wireless Mouse',
  price: 24.99,
  stockQuantity: 10,
  isActive: true
};

const monitor: Product = {
  id: 2,
  sku: 'SKU-003',
  name: '27-inch 4K Monitor',
  price: 329.99,
  stockQuantity: 5,
  isActive: true
};

function CartProbe() {
  const { items, itemCount, subtotal, addItem, removeItem, clear } = useCart();
  return (
    <div>
      <div data-testid="lines">{items.length}</div>
      <div data-testid="count">{itemCount}</div>
      <div data-testid="subtotal">{subtotal.toFixed(2)}</div>
      <button onClick={() => addItem(mouse, 2)}>add-mouse</button>
      <button onClick={() => addItem(monitor, 1)}>add-monitor</button>
      <button onClick={() => removeItem(mouse.id)}>remove-mouse</button>
      <button onClick={() => clear()}>clear</button>
    </div>
  );
}

describe('CartContext', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('adds items, merges quantities and computes the subtotal', () => {
    render(
      <CartProvider>
        <CartProbe />
      </CartProvider>
    );

    fireEvent.click(screen.getByText('add-mouse'));
    fireEvent.click(screen.getByText('add-mouse')); // same product -> merged

    expect(screen.getByTestId('lines').textContent).toBe('1');
    expect(screen.getByTestId('count').textContent).toBe('4'); // 2 + 2
    expect(screen.getByTestId('subtotal').textContent).toBe('99.96'); // 4 × 24.99
  });

  it('persists the cart to localStorage', () => {
    render(
      <CartProvider>
        <CartProbe />
      </CartProvider>
    );

    fireEvent.click(screen.getByText('add-mouse'));
    const stored = JSON.parse(localStorage.getItem('oi_cart') ?? '[]');
    expect(stored).toHaveLength(1);
    expect(stored[0].product.id).toBe(1);
    expect(stored[0].quantity).toBe(2);
  });

  it('removes items and clears the cart', () => {
    render(
      <CartProvider>
        <CartProbe />
      </CartProvider>
    );

    fireEvent.click(screen.getByText('add-mouse'));
    fireEvent.click(screen.getByText('add-monitor'));
    fireEvent.click(screen.getByText('remove-mouse'));

    expect(screen.getByTestId('lines').textContent).toBe('1');
    expect(screen.getByTestId('subtotal').textContent).toBe('329.99');

    fireEvent.click(screen.getByText('clear'));
    expect(screen.getByTestId('lines').textContent).toBe('0');
    expect(screen.getByTestId('subtotal').textContent).toBe('0.00');
  });
});
