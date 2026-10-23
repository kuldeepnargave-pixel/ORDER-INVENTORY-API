import { OrderStatus } from '../models';

const STATUS_CLASS: Record<OrderStatus, string> = {
  Pending: 'status-badge status-pending',
  Confirmed: 'status-badge status-confirmed',
  Shipped: 'status-badge status-shipped',
  Cancelled: 'status-badge status-cancelled'
};

export default function StatusBadge({ status }: { status: OrderStatus }) {
  return <span className={STATUS_CLASS[status] ?? 'status-badge'}>{status}</span>;
}
