import { api } from './api';
import { Order, OrderStatus, PagedResult } from '../models';

export interface CheckoutItem {
  productId: number;
  quantity: number;
}

export const orderService = {
  /**
   * Create an order. `idempotencyKey` MUST be provided and stable across
   * retries of the same checkout attempt.
   */
  async createOrder(items: CheckoutItem[], idempotencyKey: string): Promise<Order> {
    const response = await api.post<Order>('/orders', { items }, { headers: { 'Idempotency-Key': idempotencyKey } });
    return response.data;
  },

  async getMyOrders(): Promise<Order[]> {
    const response = await api.get<Order[]>('/orders/my');
    return response.data;
  },

  async getAllOrders(page: number, pageSize: number): Promise<PagedResult<Order>> {
    const response = await api.get<PagedResult<Order>>('/orders', { params: { page, pageSize } });
    return response.data;
  },

  async getOrder(id: number): Promise<Order> {
    const response = await api.get<Order>(`/orders/${id}`);
    return response.data;
  },

  async updateStatus(orderId: number, status: OrderStatus): Promise<Order> {
    const response = await api.patch<Order>(`/orders/${orderId}/status`, { status });
    return response.data;
  }
};
