using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Tests.Domain.Careers;

public class CareerWeekTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1041)]
    public void CareerWeek_rejects_out_of_range(int w) => Assert.Throws<ArgumentOutOfRangeException>(() => new CareerWeek(w));

    [Fact]
    public void Next_advances_one_week() => Assert.Equal(new CareerWeek(2), new CareerWeek(1).Next());

    [Fact]
    public void Only_week_1040_is_last()
    {
        Assert.True(new CareerWeek(1040).IsLast);
        Assert.False(new CareerWeek(1039).IsLast);
    }

    [Fact]
    public void Next_after_the_last_week_throws() => Assert.Throws<ArgumentOutOfRangeException>(() => new CareerWeek(1040).Next());
}
