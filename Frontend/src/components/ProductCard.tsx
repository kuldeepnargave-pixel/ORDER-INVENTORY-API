import { Product } from '../models';
import { formatMoney } from '../utils/orderStatus';

interface ProductCardProps {
  product: Product;
  onAddToCart: (product: Product) => void;
}

export default function ProductCard({ product, onAddToCart }: ProductCardProps) {
  const outOfStock = product.stockQuantity <= 0;
  const lowStock = !outOfStock && product.stockQuantity <= 5;

  return (
    <article className={`product-card${product.isActive ? '' : ' product-inactive'}`}>
      <div className="product-card-body">
        <div className="product-card-top">
          <span className="product-sku">{product.sku}</span>
          {!product.isActive && <span className="product-flag">Inactive</span>}
        </div>
        <h3 className="product-name">{product.name}</h3>
        {product.description && <p className="product-description">{product.description}</p>}
        <div className="product-card-footer">
          <span className="product-price">{formatMoney(product.price)}</span>
          <span className={`stock-badge ${outOfStock ? 'stock-out' : lowStock ? 'stock-low' : 'stock-in'}`}>
            {outOfStock ? 'Out of stock' : lowStock ? `Only ${product.stockQuantity} left` : `In stock (${product.stockQuantity})`}
          </span>
        </div>
      </div>
      <button
        type="button"
        className="btn btn-primary btn-block"
        disabled={outOfStock || !product.isActive}
        onClick={() => onAddToCart(product)}
      >
        Add to cart
      </button>
    </article>
  );
}
