namespace Backend.Helpers;

/// <summary>
/// Consistent currency rounding: 2 decimal places, midpoint away from zero
/// (e.g. 0.225 -> 0.23). Documented in README under "Discount calculation rules".
/// </summary>
public static class Money
{
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
