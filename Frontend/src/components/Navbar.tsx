import { Link, NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { useCart } from '../context/CartContext';

export default function Navbar() {
  const { user, logout } = useAuth();
  const { itemCount } = useCart();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const navClass = ({ isActive }: { isActive: boolean }) =>
    isActive ? 'nav-link nav-link-active' : 'nav-link';

  return (
    <header className="navbar">
      <div className="navbar-inner">
        <Link to="/products" className="brand">
          Order<span className="brand-accent">Inventory</span>
        </Link>

        <nav className="nav-links" aria-label="Main navigation">
          {user && (
            <NavLink to="/products" className={navClass}>
              Products
            </NavLink>
          )}
          {user && user.role === 'Customer' && (
            <>
              <NavLink to="/cart" className={navClass}>
                Cart{itemCount > 0 ? ` (${itemCount})` : ''}
              </NavLink>
              <NavLink to="/orders" className={navClass}>
                My Orders
              </NavLink>
            </>
          )}
          {user && user.role === 'Admin' && (
            <>
              <NavLink to="/admin/products" className={navClass}>
                Manage Products
              </NavLink>
              <NavLink to="/admin/orders" className={navClass}>
                Orders
              </NavLink>
              <NavLink to="/admin/top-customers" className={navClass}>
                Top Customers
              </NavLink>
            </>
          )}
        </nav>

        <div className="nav-user">
          {user ? (
            <>
              <span className="nav-user-info">
                {user.fullName} <span className="role-chip">{user.role}</span>
              </span>
              <button type="button" className="btn btn-secondary btn-sm" onClick={handleLogout}>
                Log out
              </button>
            </>
          ) : (
            <Link to="/login" className="btn btn-primary btn-sm">
              Log in
            </Link>
          )}
        </div>
      </div>
    </header>
  );
}
