import { useEffect, useState } from 'react';
import { TopCustomer } from '../models';
import { reportService } from '../services/reportService';
import { getApiErrorMessage } from '../services/errorUtils';
import { formatMoney } from '../utils/orderStatus';
import Loading from '../components/Loading';
import EmptyState from '../components/EmptyState';
import ErrorMessage from '../components/ErrorMessage';

export default function TopCustomersPage() {
  const [customers, setCustomers] = useState<TopCustomer[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError('');
    reportService
      .getTopCustomers(5)
      .then((data) => {
        if (!cancelled) setCustomers(data);
      })
      .catch((err) => {
        if (!cancelled) setError(getApiErrorMessage(err));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <div className="page">
      <h1>Admin · Top customers</h1>
      <p className="page-subtitle">
        Top 5 customers by total spending on qualifying orders (Confirmed or Shipped) - the same rule used by
        QUERIES.sql and the API. The query runs as a single SQL GROUP BY statement.
      </p>

      {loading && <Loading label="Calculating report…" />}
      {error && <ErrorMessage message={error} />}
      {!loading && !error && customers.length === 0 && (
        <EmptyState title="No qualifying orders yet" message="Once orders are confirmed or shipped, the report will populate." />
      )}

      {!loading && !error && customers.length > 0 && (
        <table className="table">
          <thead>
            <tr>
              <th>Rank</th>
              <th>Customer</th>
              <th>Email</th>
              <th>Qualifying orders</th>
              <th>Total spent</th>
            </tr>
          </thead>
          <tbody>
            {customers.map((customer, index) => (
              <tr key={customer.customerId}>
                <td>#{index + 1}</td>
                <td>
                  <strong>{customer.customerName}</strong>
                </td>
                <td>{customer.email}</td>
                <td>{customer.orderCount}</td>
                <td>
                  <strong>{formatMoney(customer.totalSpent)}</strong>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
