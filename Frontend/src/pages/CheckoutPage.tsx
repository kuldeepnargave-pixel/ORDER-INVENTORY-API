import { useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Order } from '../models';
import { useCart } from '../context/CartContext';
import { orderService } from '../services/orderService';
import { getApiErrorMessage } from '../services/errorUtils';
import { getCheckoutKey, clearCheckoutKey } from '../utils/checkoutKey';
import { formatMoney } from '../utils/orderStatus';
import ErrorMessage from '../components/ErrorMessage';
import EmptyState from '../components/EmptyState';

/**
 * Checkout. The idempotency key is generated once per checkout attempt and is
 * reused automatically if the user retries the SAME request (after a network
 * error, 409 conflict or 500). A changed cart produces a new key, so the
 * backend never sees a payload mismatch for a legit new attempt. The cart is
 * cleared ONLY after the order is successfully created (HTTP 201).
 */
export default function CheckoutPage() {
  const { items, subtotal, clear } = useCart();
  const navigate = useNavigate();

  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  // Stable fingerprint of the exact cart being checked out.
  const fingerprint = useMemo(
    () => JSON.stringify(items.map((line) => [line.product.id, line.quantity])),
    [items]
  );
  const idempotencyKey = useMemo(() => getCheckoutKey(fingerprint), [fingerprint]);

  if (items.length === 0 && !busy) {
    return (
      <div className="page">
        <h1>Checkout</h1>
        <EmptyState
          title="Nothing to check out"
          message="Your cart is empty."
          action={
            <Link to="/products" className="btn btn-primary">
              Go to products
            </Link>
          }
        />
      </div>
    );
  }

  const invalidLines = items.filter((line) => line.quantity < 1 || line.product.stockQuantity < line.quantity);

  const handlePlaceOrder = async () => {
    setError('');
    setBusy(true);
    try {
      const order: Order = await orderService.createOrder(
        items.map((line) => ({ productId: line.product.id, quantity: line.quantity })),
        idempotencyKey
      );
      clear(); // only after success
      clearCheckoutKey();
      navigate(`/orders/${order.id}`, { state: { justCreated: true } });
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="page">
      <h1>Checkout</h1>
      <p className="page-subtitle">
        Stock availability, prices and discounts are validated server-side when the order is created.
      </p>

      {error && (
        <ErrorMessage
          message={error}
          hint="Your idempotency key is preserved - fixing the issue and clicking again will not create duplicate orders."
        />
      )}

      {invalidLines.length > 0 && (
        <ErrorMessage
          message="Some quantities are not available."
          hint={invalidLines
            .map((line) => `${line.product.name}: requested ${line.quantity}, in stock ${line.product.stockQuantity}`)
            .join(' · ')}
        />
      )}

      <div className="checkout-layout">
        <table className="table">
          <thead>
            <tr>
              <th>Product</th>
              <th>Qty</th>
              <th>Unit price</th>
              <th>Line total</th>
            </tr>
          </thead>
          <tbody>
            {items.map((line) => (
              <tr key={line.product.id}>
                <td>
                  <strong>{line.product.name}</strong>
                  <div className="muted">{line.product.sku}</div>
                </td>
                <td>{line.quantity}</td>
                <td>{formatMoney(line.product.price)}</td>
                <td>{formatMoney(line.product.price * line.quantity)}</td>
              </tr>
            ))}
          </tbody>
        </table>

        <div className="cart-summary">
          <h3>Order summary</h3>
          <div className="summary-row">
            <span>Subtotal</span>
            <span>{formatMoney(subtotal)}</span>
          </div>
          <div className="summary-row muted">
            <span>Discounts</span>
            <span>calculated at order creation</span>
          </div>
          <button
            type="button"
            className="btn btn-primary btn-block"
            disabled={busy || invalidLines.length > 0}
            onClick={handlePlaceOrder}
          >
            {busy ? (
              <>
                <span className="loading-spinner" aria-hidden="true" /> Placing order…
              </>
            ) : (
              'Place order'
            )}
          </button>
        </div>
      </div>
    </div>
  );
}
