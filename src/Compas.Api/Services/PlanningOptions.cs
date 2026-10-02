namespace Compas.Api.Services;

public class PlanningOptions
{
    public TimeSpan WorkDayStart { get; set; } = new(8, 0, 0);
    public TimeSpan WorkDayEnd { get; set; } = new(20, 0, 0);
    public int MinGapMinutes { get; set; } = 10;
    public int BreakAfterMinutes { get; set; } = 90;
    public int BreakDurationMinutes { get; set; } = 15;
}
