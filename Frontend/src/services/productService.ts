import { api } from './api';
import { PagedResult, Product, ProductQuery, ProductWritePayload } from '../models';

function toParams(query: ProductQuery): Record<string, string | number | boolean> {
  const params: Record<string, string | number | boolean> = {};
  if (query.search) params.search = query.search;
  if (query.minPrice !== undefined) params.minPrice = query.minPrice;
  if (query.maxPrice !== undefined) params.maxPrice = query.maxPrice;
  if (query.inStock !== undefined) params.inStock = query.inStock;
  if (query.page !== undefined) params.page = query.page;
  if (query.pageSize !== undefined) params.pageSize = query.pageSize;
  if (query.sortBy) params.sortBy = query.sortBy;
  if (query.sortDirection) params.sortDirection = query.sortDirection;
  return params;
}

export const productService = {
  async getProducts(query: ProductQuery): Promise<PagedResult<Product>> {
    const response = await api.get<PagedResult<Product>>('/products', { params: toParams(query) });
    return response.data;
  },

  async getProduct(id: number): Promise<Product> {
    const response = await api.get<Product>(`/products/${id}`);
    return response.data;
  },

  async createProduct(payload: ProductWritePayload): Promise<Product> {
    const response = await api.post<Product>('/products', payload);
    return response.data;
  },

  async updateProduct(id: number, payload: ProductWritePayload): Promise<Product> {
    const response = await api.put<Product>(`/products/${id}`, payload);
    return response.data;
  }
};
