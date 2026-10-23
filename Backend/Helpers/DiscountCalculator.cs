namespace Backend.Helpers;

/// <summary>
/// Server-side discount rules (never trust client prices/discounts):
///   Rule A: 5% discount when the order subtotal is strictly greater than 10,000.
///   Rule B: additional 10% discount on every fifth order placed by the customer.
///           The sequence includes the order being created (1-based), so a
///           customer's 5th, 10th, 15th... order qualifies.
///   When both apply, the 10% is calculated on the amount remaining after the
///   5% subtotal discount has been subtracted.
/// All values are rounded to 2 decimals (midpoint away from zero).
/// </summary>
public static class DiscountCalculator
{
    public const decimal SubtotalDiscountThreshold = 10_000m;
    public const decimal SubtotalDiscountRate = 0.05m;
    public const decimal FifthOrderDiscountRate = 0.10m;

    public static bool IsFifthOrder(int customerOrderSequenceIncludingCurrent)
        => customerOrderSequenceIncludingCurrent > 0
           && customerOrderSequenceIncludingCurrent % 5 == 0;

    public static decimal CalculateSubtotalDiscount(decimal subtotal)
        => subtotal > SubtotalDiscountThreshold
            ? Money.Round(subtotal * SubtotalDiscountRate)
            : 0m;

    public static decimal CalculateDiscount(decimal subtotal, int customerOrderSequenceIncludingCurrent)
    {
        var subtotalDiscount = CalculateSubtotalDiscount(subtotal);
        var amountAfterSubtotalDiscount = subtotal - subtotalDiscount;

        var fifthOrderDiscount = IsFifthOrder(customerOrderSequenceIncludingCurrent)
            ? Money.Round(amountAfterSubtotalDiscount * FifthOrderDiscountRate)
            : 0m;

        return Money.Round(subtotalDiscount + fifthOrderDiscount);
    }
}
