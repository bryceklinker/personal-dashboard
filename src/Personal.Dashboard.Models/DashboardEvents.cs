namespace Personal.Dashboard.Models;

public record DashboardEvent(string Type, object? Payload = null);
public record LeaguesRefreshedDashboardEvent() : DashboardEvent("LeaguesRefreshed");
