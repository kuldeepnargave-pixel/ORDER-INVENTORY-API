namespace Backend.Models;

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public User? Customer { get; set; }

    public string Status { get; set; } = OrderStatus.Pending;

    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Client-supplied key used to make order creation idempotent.
    /// Unique per customer (enforced by a database index).
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the canonical order payload. Used to detect a key
    /// being replayed with a different payload (HTTP 409).
    /// </summary>
    public string IdempotencyPayloadHash { get; set; } = string.Empty;

    /// <summary>
    /// SQL Server rowversion column. Guarantees concurrent status updates
    /// (e.g. simultaneous cancels) cannot both restore stock.
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
