import { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { UserRole } from '../models';
import ErrorMessage from './ErrorMessage';

/**
 * Renders children only for the allowed roles; admins-only routes show a 403
 * message to other signed-in users. The backend enforces the same rules.
 */
export default function RoleGuard({ allowed, children }: { allowed: UserRole[]; children: ReactNode }) {
  const { user } = useAuth();

  if (!user) {
    return <Navigate to="/login" replace />;
  }
  if (!allowed.includes(user.role)) {
    return (
      <ErrorMessage
        message="403 - You do not have permission to view this page."
        hint="This area is restricted. Use the navigation menu to return to your pages."
      />
    );
  }
  return <>{children}</>;
}
