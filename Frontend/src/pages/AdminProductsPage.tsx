import { FormEvent, useCallback, useEffect, useState } from 'react';
import { PagedResult, Product, ProductWritePayload } from '../models';
import { productService } from '../services/productService';
import { getApiErrorMessage } from '../services/errorUtils';
import { formatMoney } from '../utils/orderStatus';
import Pagination from '../components/Pagination';
import Loading from '../components/Loading';
import ErrorMessage from '../components/ErrorMessage';

interface ProductForm {
  id: number | null;
  sku: string;
  name: string;
  description: string;
  price: string;
  stockQuantity: string;
  isActive: boolean;
}

const EMPTY_FORM: ProductForm = {
  id: null,
  sku: '',
  name: '',
  description: '',
  price: '',
  stockQuantity: '',
  isActive: true
};

export default function AdminProductsPage() {
  const [result, setResult] = useState<PagedResult<Product> | null>(null);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  const [form, setForm] = useState<ProductForm>(EMPTY_FORM);
  const [formError, setFormError] = useState('');
  const [saving, setSaving] = useState(false);

  const load = useCallback(async (targetPage: number) => {
    setLoading(true);
    setError('');
    try {
      setResult(await productService.getProducts({ page: targetPage, pageSize: 10, sortBy: 'name' }));
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load(page);
  }, [load, page]);

  const startEdit = (product: Product) => {
    setNotice('');
    setFormError('');
    setForm({
      id: product.id,
      sku: product.sku,
      name: product.name,
      description: product.description ?? '',
      price: String(product.price),
      stockQuantity: String(product.stockQuantity),
      isActive: product.isActive
    });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFormError('');
    setNotice('');

    const price = Number(form.price);
    const stockQuantity = Number(form.stockQuantity);
    if (!form.sku.trim() || !form.name.trim()) {
      setFormError('SKU and name are required.');
      return;
    }
    if (Number.isNaN(price) || price < 0) {
      setFormError('Price must be zero or a positive number.');
      return;
    }
    if (!Number.isInteger(stockQuantity) || stockQuantity < 0) {
      setFormError('Stock quantity must be a non-negative whole number.');
      return;
    }

    const payload: ProductWritePayload = {
      sku: form.sku.trim(),
      name: form.name.trim(),
      description: form.description.trim() || undefined,
      price,
      stockQuantity,
      isActive: form.isActive
    };

    setSaving(true);
    try {
      if (form.id === null) {
        const created = await productService.createProduct(payload);
        setNotice(`Product "${created.name}" created.`);
      } else {
        const updated = await productService.updateProduct(form.id, payload);
        setNotice(`Product "${updated.name}" updated.`);
      }
      setForm(EMPTY_FORM);
      await load(page);
    } catch (err) {
      setFormError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const handleToggleActive = async (product: Product) => {
    setNotice('');
    setError('');
    try {
      await productService.updateProduct(product.id, {
        sku: product.sku,
        name: product.name,
        description: product.description ?? undefined,
        price: product.price,
        stockQuantity: product.stockQuantity,
        isActive: !product.isActive
      });
      await load(page);
    } catch (err) {
      setError(getApiErrorMessage(err));
    }
  };

  return (
    <div className="page">
      <h1>Admin · Product management</h1>

      <section className="admin-form-card">
        <h2>{form.id === null ? 'Create product' : `Edit product #${form.id}`}</h2>
        {formError && <ErrorMessage message={formError} />}
        {notice && <div className="success-banner" role="status">{notice}</div>}
        <form onSubmit={handleSubmit} className="form-grid" noValidate>
          <label className="field">
            SKU *
            <input className="input" value={form.sku} maxLength={50} onChange={(e) => setForm({ ...form, sku: e.target.value })} />
          </label>
          <label className="field">
            Name *
            <input className="input" value={form.name} maxLength={200} onChange={(e) => setForm({ ...form, name: e.target.value })} />
          </label>
          <label className="field">
            Price (USD) *
            <input className="input" type="number" min="0" step="0.01" value={form.price} onChange={(e) => setForm({ ...form, price: e.target.value })} />
          </label>
          <label className="field">
            Stock quantity *
            <input className="input" type="number" min="0" step="1" value={form.stockQuantity} onChange={(e) => setForm({ ...form, stockQuantity: e.target.value })} />
          </label>
          <label className="field field-wide">
            Description
            <input className="input" value={form.description} maxLength={1000} onChange={(e) => setForm({ ...form, description: e.target.value })} />
          </label>
          <label className="filter-checkbox">
            <input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />
            Active (orderable)
          </label>
          <div className="form-actions">
            <button type="submit" className="btn btn-primary" disabled={saving}>
              {saving ? 'Saving…' : form.id === null ? 'Create product' : 'Save changes'}
            </button>
            {form.id !== null && (
              <button type="button" className="btn btn-secondary" onClick={() => setForm(EMPTY_FORM)}>
                Cancel edit
              </button>
            )}
          </div>
        </form>
      </section>

      {loading && <Loading label="Loading products…" />}
      {error && <ErrorMessage message={error} />}

      {result && (
        <>
          <table className="table">
            <thead>
              <tr>
                <th>SKU</th>
                <th>Name</th>
                <th>Price</th>
                <th>Stock</th>
                <th>Status</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {result.items.map((product) => (
                <tr key={product.id}>
                  <td>{product.sku}</td>
                  <td>{product.name}</td>
                  <td>{formatMoney(product.price)}</td>
                  <td>{product.stockQuantity}</td>
                  <td>{product.isActive ? <span className="chip chip-green">Active</span> : <span className="chip chip-grey">Inactive</span>}</td>
                  <td className="table-actions">
                    <button type="button" className="btn btn-secondary btn-sm" onClick={() => startEdit(product)}>
                      Edit
                    </button>
                    <button type="button" className="btn btn-danger btn-sm" onClick={() => void handleToggleActive(product)}>
                      {product.isActive ? 'Deactivate' : 'Activate'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <Pagination
            page={result.page}
            totalPages={result.totalPages}
            totalCount={result.totalCount}
            onChange={setPage}
          />
        </>
      )}
    </div>
  );
}
