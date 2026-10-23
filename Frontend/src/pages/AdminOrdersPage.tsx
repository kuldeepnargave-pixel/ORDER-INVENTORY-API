import { useCallback, useEffect, useState } from 'react';
import { Order, OrderStatus } from '../models';
import { orderService } from '../services/orderService';
import { getApiErrorMessage } from '../services/errorUtils';
import { ALLOWED_TRANSITIONS, formatDateTime, formatMoney } from '../utils/orderStatus';
import StatusBadge from '../components/StatusBadge';
import Pagination from '../components/Pagination';
import Loading from '../components/Loading';
import EmptyState from '../components/EmptyState';
import ErrorMessage from '../components/ErrorMessage';
import ConfirmDialog from '../components/ConfirmDialog';

export default function AdminOrdersPage() {
  const [result, setResult] = useState<{ items: Order[]; page: number; totalPages: number; totalCount: number } | null>(null);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [pendingChange, setPendingChange] = useState<{ orderId: number; status: OrderStatus } | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async (targetPage: number) => {
    setLoading(true);
    setError('');
    try {
      const paged = await orderService.getAllOrders(targetPage, 10);
      setResult(paged);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load(page);
  }, [load, page]);

  const applyStatusChange = async () => {
    if (!pendingChange) return;
    setBusy(true);
    setError('');
    setNotice('');
    try {
      await orderService.updateStatus(pendingChange.orderId, pendingChange.status);
      setNotice(`Order #${pendingChange.orderId} is now ${pendingChange.status}.`);
      setPendingChange(null);
      await load(page);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="page">
      <h1>Admin · Order management</h1>
      <p className="page-subtitle">
        Allowed transitions: Pending → Confirmed/Cancelled, Confirmed → Shipped/Cancelled. Cancelling restores stock.
      </p>

      {notice && <div className="success-banner" role="status">{notice}</div>}
      {error && <ErrorMessage message={error} />}
      {loading && <Loading label="Loading orders…" />}

      {!loading && result && result.items.length === 0 && (
        <EmptyState title="No orders yet" message="Orders placed by customers will appear here." />
      )}

      {!loading && result && result.items.length > 0 && (
        <>
          <table className="table">
            <thead>
              <tr>
                <th>Order</th>
                <th>Customer</th>
                <th>Items</th>
                <th>Total</th>
                <th>Created</th>
                <th>Status</th>
                <th>Change status</th>
              </tr>
            </thead>
            <tbody>
              {result.items.map((order) => {
                const nextStatuses = ALLOWED_TRANSITIONS[order.status];
                return (
                  <tr key={order.id}>
                    <td>
                      <strong>#{order.id}</strong>
                    </td>
                    <td>{order.customerName}</td>
                    <td>{order.items.reduce((sum, item) => sum + item.quantity, 0)}</td>
                    <td>
                      {formatMoney(order.total)}
                      {order.discount > 0 && <div className="muted">−{formatMoney(order.discount)} discount</div>}
                    </td>
                    <td>{formatDateTime(order.createdAt)}</td>
                    <td>
                      <StatusBadge status={order.status} />
                    </td>
                    <td>
                      {nextStatuses.length === 0 ? (
                        <span className="muted">—</span>
                      ) : (
                        <div className="table-actions">
                          {nextStatuses.map((status) => (
                            <button
                              key={status}
                              type="button"
                              className={`btn btn-sm ${status === 'Cancelled' ? 'btn-danger' : 'btn-primary'}`}
                              onClick={() => setPendingChange({ orderId: order.id, status })}
                            >
                              {status}
                            </button>
                          ))}
                        </div>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          <Pagination page={result.page} totalPages={result.totalPages} totalCount={result.totalCount} onChange={setPage} />
        </>
      )}

      {pendingChange && (
        <ConfirmDialog
          title={`Set order #${pendingChange.orderId} to ${pendingChange.status}?`}
          message={
            pendingChange.status === 'Cancelled'
              ? 'Cancelling will restore the reserved stock exactly once. This cannot be undone.'
              : 'The order status will be updated immediately.'
          }
          confirmLabel={`Mark as ${pendingChange.status}`}
          busy={busy}
          onConfirm={() => void applyStatusChange()}
          onCancel={() => setPendingChange(null)}
        />
      )}
    </div>
  );
}
