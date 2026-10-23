import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import { CartProvider } from './context/CartContext';
import Navbar from './components/Navbar';
import ProtectedRoute from './components/ProtectedRoute';
import RoleGuard from './components/RoleGuard';
import LoginPage from './pages/LoginPage';
import RegisterPage from './pages/RegisterPage';
import ProductsPage from './pages/ProductsPage';
import CartPage from './pages/CartPage';
import CheckoutPage from './pages/CheckoutPage';
import MyOrdersPage from './pages/MyOrdersPage';
import OrderDetailsPage from './pages/OrderDetailsPage';
import AdminProductsPage from './pages/AdminProductsPage';
import AdminOrdersPage from './pages/AdminOrdersPage';
import TopCustomersPage from './pages/TopCustomersPage';
import EmptyState from './components/EmptyState';

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <CartProvider>
          <Navbar />
          <main className="container">
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route path="/register" element={<RegisterPage />} />

              <Route
                path="/products"
                element={
                  <ProtectedRoute>
                    <ProductsPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path="/cart"
                element={
                  <ProtectedRoute>
                    <CartPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path="/checkout"
                element={
                  <ProtectedRoute>
                    <CheckoutPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path="/orders"
                element={
                  <ProtectedRoute>
                    <MyOrdersPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path="/orders/:id"
                element={
                  <ProtectedRoute>
                    <OrderDetailsPage />
                  </ProtectedRoute>
                }
              />

              <Route
                path="/admin/products"
                element={
                  <ProtectedRoute>
                    <RoleGuard allowed={['Admin']}>
                      <AdminProductsPage />
                    </RoleGuard>
                  </ProtectedRoute>
                }
              />
              <Route
                path="/admin/orders"
                element={
                  <ProtectedRoute>
                    <RoleGuard allowed={['Admin']}>
                      <AdminOrdersPage />
                    </RoleGuard>
                  </ProtectedRoute>
                }
              />
              <Route
                path="/admin/top-customers"
                element={
                  <ProtectedRoute>
                    <RoleGuard allowed={['Admin']}>
                      <TopCustomersPage />
                    </RoleGuard>
                  </ProtectedRoute>
                }
              />

              <Route path="/" element={<Navigate to="/products" replace />} />
              <Route
                path="*"
                element={
                  <div className="page">
                    <EmptyState title="Page not found" message="The page you are looking for does not exist." />
                  </div>
                }
              />
            </Routes>
          </main>
          <footer className="footer">
            <span>OrderInventory — .NET 8 + React machine-test project</span>
          </footer>
        </CartProvider>
      </AuthProvider>
    </BrowserRouter>
  );
}
