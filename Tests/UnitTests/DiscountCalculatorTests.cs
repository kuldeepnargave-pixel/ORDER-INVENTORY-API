using Backend.Helpers;
using Xunit;

namespace UnitTests;

public class DiscountCalculatorTests
{
    // ---------------------------------------------------------------
    // Rule A: 5% discount when subtotal > 10,000 (strictly greater)
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(9_999.99, 0)]   // just below the threshold
    [InlineData(10_000, 0)]     // exactly at the threshold -> no discount
    public void CalculateSubtotalDiscount_AtOrBelowThreshold_ReturnsZero(decimal subtotal, decimal expected)
    {
        Assert.Equal(expected, DiscountCalculator.CalculateSubtotalDiscount(subtotal));
    }

    [Theory]
    [InlineData(10_000.01, 500.00)]  // 10,000.01 x 0.05 = 500.0005 -> rounds to 500.00
    [InlineData(20_000, 1_000)]
    [InlineData(125_000, 6_250)]
    public void CalculateSubtotalDiscount_AboveThreshold_ReturnsFivePercent(decimal subtotal, decimal expected)
    {
        Assert.Equal(expected, DiscountCalculator.CalculateSubtotalDiscount(subtotal));
    }

    // ---------------------------------------------------------------
    // Rule B: additional 10% on every fifth order (sequence is 1-based
    // and includes the order being created)
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(9)]
    public void IsFifthOrder_NonMultipleOfFive_ReturnsFalse(int sequence)
    {
        Assert.False(DiscountCalculator.IsFifthOrder(sequence));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(15)]
    public void IsFifthOrder_MultipleOfFive_ReturnsTrue(int sequence)
    {
        Assert.True(DiscountCalculator.IsFifthOrder(sequence));
    }

    [Fact]
    public void CalculateDiscount_FifthOrderOnly_TenPercentOfSubtotal()
    {
        // 5th order, subtotal below the 5% threshold.
        Assert.Equal(100m, DiscountCalculator.CalculateDiscount(1_000m, 5));
    }

    [Fact]
    public void CalculateDiscount_BothRules_TenPercentAppliedOnAmountAfterSubtotalDiscount()
    {
        // subtotal 20,000 -> 5% = 1,000 -> remaining 19,000 -> 10% = 1,900
        // total discount = 2,900
        Assert.Equal(2_900m, DiscountCalculator.CalculateDiscount(20_000m, 5));
    }

    [Fact]
    public void CalculateDiscount_SubtotalDiscountOnly_NoFifthOrderBonus()
    {
        Assert.Equal(1_000m, DiscountCalculator.CalculateDiscount(20_000m, 3));
    }

    // ---------------------------------------------------------------
    // Rounding: 2 decimals, midpoint away from zero
    // ---------------------------------------------------------------

    [Fact]
    public void CalculateDiscount_Midpoint_RoundsAwayFromZero()
    {
        // 5th order, subtotal 2.25 -> 10% = 0.225 -> 0.23 (away from zero).
        // Banker's rounding would produce 0.22.
        Assert.Equal(0.23m, DiscountCalculator.CalculateDiscount(2.25m, 5));
    }

    [Fact]
    public void Money_Round_UsesTwoDecimalsAwayFromZero()
    {
        Assert.Equal(3.34m, Money.Round(3.335m));
        Assert.Equal(2.46m, Money.Round(2.455m));
        Assert.Equal(9.99m, Money.Round(9.994m));
    }
}
