namespace Personal.Dashboard.Api.Host.Leagues;

public class RefreshLeaguesSettings
{
    public const string SectionName = "RefreshLeagues";
    public int IntervalHours { get; set; } = 24;
}
