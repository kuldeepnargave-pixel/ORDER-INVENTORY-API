namespace Backend.Models;

public static class OrderStatus
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Shipped = "Shipped";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = { Pending, Confirmed, Shipped, Cancelled };
}
