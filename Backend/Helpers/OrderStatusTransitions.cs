using Backend.Models;

namespace Backend.Helpers;

/// <summary>
/// Allowed order status transitions:
///   Pending   -> Confirmed | Cancelled
///   Confirmed -> Shipped   | Cancelled
///   Shipped   -> (terminal)
///   Cancelled -> (terminal)
/// Any other transition must be rejected with HTTP 400.
/// </summary>
public static class OrderStatusTransitions
{
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        [OrderStatus.Pending] = new[] { OrderStatus.Confirmed, OrderStatus.Cancelled },
        [OrderStatus.Confirmed] = new[] { OrderStatus.Shipped, OrderStatus.Cancelled },
        [OrderStatus.Shipped] = Array.Empty<string>(),
        [OrderStatus.Cancelled] = Array.Empty<string>()
    };

    public static bool CanTransition(string from, string to)
        => Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static string AllowedTransitionsDescription(string from)
        => Allowed.TryGetValue(from, out var targets) && targets.Length > 0
            ? $"Allowed: {string.Join(", ", targets)}."
            : $"'{from}' is a terminal status; no further transitions are allowed.";
}
