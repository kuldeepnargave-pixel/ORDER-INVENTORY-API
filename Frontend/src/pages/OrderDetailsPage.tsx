import { useCallback, useEffect, useState } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import { Order } from '../models';
import { useAuth } from '../context/AuthContext';
import { orderService } from '../services/orderService';
import { getApiErrorMessage } from '../services/errorUtils';
import { canCancel, formatDateTime, formatMoney } from '../utils/orderStatus';
import StatusBadge from '../components/StatusBadge';
import ConfirmDialog from '../components/ConfirmDialog';
import Loading from '../components/Loading';
import ErrorMessage from '../components/ErrorMessage';

export default function OrderDetailsPage() {
  const { id } = useParams<{ id: string }>();
  const location = useLocation();
  const { user } = useAuth();

  const [order, setOrder] = useState<Order | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [confirmingCancel, setConfirmingCancel] = useState(false);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState('');

  const justCreated = Boolean((location.state as { justCreated?: boolean } | null)?.justCreated);

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setError('');
    try {
      setOrder(await orderService.getOrder(Number(id)));
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    void load();
  }, [load]);

  const handleCancel = async () => {
    if (!order) return;
    setBusy(true);
    setActionError('');
    try {
      await orderService.updateStatus(order.id, 'Cancelled');
      setConfirmingCancel(false);
      await load();
    } catch (err) {
      setActionError(getApiErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  if (loading) {
    return (
      <div className="page">
        <Loading label="Loading order…" />
      </div>
    );
  }

  if (error || !order) {
    return (
      <div className="page">
        <ErrorMessage message={error || 'Order not found.'} />
        <Link to="/orders" className="btn btn-secondary">← Back to my orders</Link>
      </div>
    );
  }

  return (
    <div className="page">
      {justCreated && (
        <div className="success-banner" role="status">
          ✅ Order placed successfully! Reserved stock is held while the order is pending.
        </div>
      )}

      <div className="details-header">
        <h1>Order #{order.id}</h1>
        <StatusBadge status={order.status} />
      </div>
      <p className="muted">
        Placed {formatDateTime(order.createdAt)} · Last updated {formatDateTime(order.updatedAt)}
        {user && user.role === 'Admin' && <> · Customer: {order.customerName} (#{order.customerId})</>}
      </p>

      {actionError && <ErrorMessage message={actionError} />}

      <table className="table">
        <thead>
          <tr>
            <th>Product</th>
            <th>SKU</th>
            <th>Qty</th>
            <th>Unit price</th>
            <th>Line total</th>
          </tr>
        </thead>
        <tbody>
          {order.items.map((item) => (
            <tr key={item.productId}>
              <td>{item.productName}</td>
              <td>{item.sku}</td>
              <td>{item.quantity}</td>
              <td>{formatMoney(item.unitPrice)}</td>
              <td>{formatMoney(item.lineTotal)}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="totals-card">
        <div className="summary-row">
          <span>Subtotal</span>
          <span>{formatMoney(order.subTotal)}</span>
        </div>
        <div className="summary-row">
          <span>Discount</span>
          <span>−{formatMoney(order.discount)}</span>
        </div>
        <div className="summary-row summary-total">
          <span>Total</span>
          <span>{formatMoney(order.total)}</span>
        </div>
      </div>

      {canCancel(order.status) && (
        <button type="button" className="btn btn-danger" onClick={() => setConfirmingCancel(true)}>
          Cancel this order
        </button>
      )}

      {confirmingCancel && (
        <ConfirmDialog
          title={`Cancel order #${order.id}?`}
          message="The order will be cancelled and the reserved stock will be restored. This cannot be undone."
          confirmLabel="Cancel order"
          busy={busy}
          onConfirm={() => void handleCancel()}
          onCancel={() => setConfirmingCancel(false)}
        />
      )}
    </div>
  );
}
