import { Link } from 'react-router-dom';
import { useCart } from '../context/CartContext';
import { formatMoney } from '../utils/orderStatus';

interface CartSummaryProps {
  showCheckoutButton?: boolean;
}

export default function CartSummary({ showCheckoutButton = false }: CartSummaryProps) {
  const { subtotal, itemCount } = useCart();

  return (
    <aside className="cart-summary">
      <h3>Order summary</h3>
      <div className="summary-row">
        <span>Items</span>
        <span>{itemCount}</span>
      </div>
      <div className="summary-row summary-total">
        <span>Subtotal</span>
        <span>{formatMoney(subtotal)}</span>
      </div>
      <p className="summary-note">
        Discounts (5% over $10,000 and every 5th order) are calculated on the server at checkout.
      </p>
      {showCheckoutButton && itemCount > 0 && (
        <Link to="/checkout" className="btn btn-primary btn-block">
          Proceed to checkout
        </Link>
      )}
    </aside>
  );
}
