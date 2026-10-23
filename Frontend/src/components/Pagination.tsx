interface PaginationProps {
  page: number;
  totalPages: number;
  totalCount: number;
  onChange: (page: number) => void;
}

export default function Pagination({ page, totalPages, totalCount, onChange }: PaginationProps) {
  if (totalPages <= 0) return null;

  return (
    <div className="pagination">
      <button
        type="button"
        className="btn btn-secondary"
        disabled={page <= 1}
        onClick={() => onChange(page - 1)}
      >
        ← Previous
      </button>
      <span className="pagination-info">
        Page {page} of {totalPages} · {totalCount} item{totalCount === 1 ? '' : 's'}
      </span>
      <button
        type="button"
        className="btn btn-secondary"
        disabled={page >= totalPages}
        onClick={() => onChange(page + 1)}
      >
        Next →
      </button>
    </div>
  );
}
