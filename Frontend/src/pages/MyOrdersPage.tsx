import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Order } from '../models';
import { useAuth } from '../context/AuthContext';
import { orderService } from '../services/orderService';
import { getApiErrorMessage } from '../services/errorUtils';
import { canCancel, formatDateTime, formatMoney } from '../utils/orderStatus';
import StatusBadge from '../components/StatusBadge';
import ConfirmDialog from '../components/ConfirmDialog';
import Loading from '../components/Loading';
import EmptyState from '../components/EmptyState';
import ErrorMessage from '../components/ErrorMessage';

export default function MyOrdersPage() {
  const { user } = useAuth();
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [actionError, setActionError] = useState('');
  const [cancellingId, setCancellingId] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      setOrders(await orderService.getMyOrders());
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const handleCancel = async (orderId: number) => {
    setBusy(true);
    setActionError('');
    try {
      await orderService.updateStatus(orderId, 'Cancelled');
      setCancellingId(null);
      await load(); // refresh displayed data after successful cancellation
    } catch (err) {
      setActionError(getApiErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="page">
      <h1>My orders</h1>

      {actionError && <ErrorMessage message={actionError} />}
      {loading && <Loading label="Loading your orders…" />}
      {!loading && error && <ErrorMessage message={error} />}
      {!loading && !error && orders.length === 0 && (
        <EmptyState
          title="No orders yet"
          message="When you place an order it will appear here."
          action={
            <Link to="/products" className="btn btn-primary">
              Start shopping
            </Link>
          }
        />
      )}

      {!loading && !error && orders.length > 0 && (
        <div className="order-list">
          {orders.map((order) => (
            <article key={order.id} className="order-card">
              <div className="order-card-header">
                <div>
                  <Link to={`/orders/${order.id}`} className="order-link">
                    Order #{order.id}
                  </Link>
                  <div className="muted">Placed {formatDateTime(order.createdAt)}</div>
                </div>
                <div className="order-card-meta">
                  <StatusBadge status={order.status} />
                  <strong>{formatMoney(order.total)}</strong>
                </div>
              </div>
              <div className="order-card-items muted">
                {order.items.map((item) => (
                  <span key={item.productId}>
                    {item.quantity} × {item.productName}
                  </span>
                ))}
              </div>
              {user && canCancel(order.status) && (
                <div className="order-card-actions">
                  <button type="button" className="btn btn-danger btn-sm" onClick={() => setCancellingId(order.id)}>
                    Cancel order
                  </button>
                </div>
              )}
            </article>
          ))}
        </div>
      )}

      {cancellingId !== null && (
        <ConfirmDialog
          title={`Cancel order #${cancellingId}?`}
          message="The order will be cancelled and the reserved stock will be restored. This cannot be undone."
          confirmLabel="Cancel order"
          busy={busy}
          onConfirm={() => void handleCancel(cancellingId)}
          onCancel={() => setCancellingId(null)}
        />
      )}
    </div>
  );
}
