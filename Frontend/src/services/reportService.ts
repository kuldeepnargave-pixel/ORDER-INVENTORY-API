import { api } from './api';
import { TopCustomer } from '../models';

export const reportService = {
  async getTopCustomers(top = 5): Promise<TopCustomer[]> {
    const response = await api.get<TopCustomer[]>('/reports/top-customers', { params: { top } });
    return response.data;
  }
};
