import { Link } from 'react-router-dom';
import { useCart } from '../context/CartContext';
import { formatMoney } from '../utils/orderStatus';
import CartSummary from '../components/CartSummary';
import EmptyState from '../components/EmptyState';

export default function CartPage() {
  const { items, setQuantity, removeItem } = useCart();

  if (items.length === 0) {
    return (
      <div className="page">
        <h1>Your cart</h1>
        <EmptyState
          title="Your cart is empty"
          message="Browse the catalog and add products to get started."
          action={
            <Link to="/products" className="btn btn-primary">
              Go to products
            </Link>
          }
        />
      </div>
    );
  }

  return (
    <div className="page">
      <h1>Your cart</h1>
      <div className="cart-layout">
        <div className="cart-lines">
          {items.map(({ product, quantity }) => (
            <div key={product.id} className="cart-line">
              <div className="cart-line-info">
                <strong>{product.name}</strong>
                <span className="muted">{product.sku} · {formatMoney(product.price)} each</span>
                <span className={product.stockQuantity < quantity ? 'stock-warning' : 'muted'}>
                  {product.stockQuantity < quantity
                    ? `Warning: only ${product.stockQuantity} in stock - checkout will fail until you reduce the quantity.`
                    : `${product.stockQuantity} in stock`}
                </span>
              </div>
              <div className="cart-line-controls">
                <label>
                  Qty
                  <input
                    type="number"
                    className="input cart-qty"
                    min={1}
                    max={product.stockQuantity > 0 ? product.stockQuantity : undefined}
                    value={quantity}
                    onChange={(e) => setQuantity(product.id, Number(e.target.value) || 0)}
                  />
                </label>
                <span className="cart-line-total">{formatMoney(product.price * quantity)}</span>
                <button type="button" className="btn btn-danger btn-sm" onClick={() => removeItem(product.id)}>
                  Remove
                </button>
              </div>
            </div>
          ))}
        </div>
        <CartSummary showCheckoutButton />
      </div>
    </div>
  );
}
