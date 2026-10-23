// Shared frontend type definitions mirroring the backend DTOs (camelCase JSON).

export type UserRole = 'Admin' | 'Customer';

export interface UserInfo {
  id: number;
  fullName: string;
  email: string;
  role: UserRole;
}

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
}

export interface AuthResponse extends AuthTokens {
  expiresAtUtc: string;
  user: UserInfo;
}

export interface Product {
  id: number;
  sku: string;
  name: string;
  description?: string | null;
  price: number;
  stockQuantity: number;
  isActive: boolean;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ProductQuery {
  search?: string;
  minPrice?: number;
  maxPrice?: number;
  inStock?: boolean;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

export interface ProductWritePayload {
  sku: string;
  name: string;
  description?: string;
  price: number;
  stockQuantity: number;
  isActive: boolean;
}

export type OrderStatus = 'Pending' | 'Confirmed' | 'Shipped' | 'Cancelled';

export interface OrderItem {
  productId: number;
  productName: string;
  sku: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface Order {
  id: number;
  customerId: number;
  customerName: string;
  status: OrderStatus;
  subTotal: number;
  discount: number;
  total: number;
  createdAt: string;
  updatedAt: string;
  items: OrderItem[];
}

export interface TopCustomer {
  customerId: number;
  customerName: string;
  email: string;
  orderCount: number;
  totalSpent: number;
}

export interface CartLine {
  product: Product;
  quantity: number;
}
