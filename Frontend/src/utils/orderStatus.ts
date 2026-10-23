import { OrderStatus } from '../models';

/**
 * Server-authoritative transition map, duplicated on the client for UX only
 * (the backend validates every status change again).
 */
export const ALLOWED_TRANSITIONS: Record<OrderStatus, OrderStatus[]> = {
  Pending: ['Confirmed', 'Cancelled'],
  Confirmed: ['Shipped', 'Cancelled'],
  Shipped: [],
  Cancelled: []
};

export function canCancel(status: OrderStatus): boolean {
  return ALLOWED_TRANSITIONS[status].includes('Cancelled');
}

export function formatMoney(value: number): string {
  return value.toLocaleString('en-US', { style: 'currency', currency: 'USD' });
}

export function formatDateTime(value: string): string {
  return new Date(value).toLocaleString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  });
}
