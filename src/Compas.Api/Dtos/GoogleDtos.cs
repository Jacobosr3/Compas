namespace Compas.Api.Dtos;

public record GoogleCalendarDto(string Id, string Summary, string? ColorHex, bool IsPrimary, bool Selected);

public record GoogleStatusDto(
    bool Configured,
    bool Connected,
    string? Email,
    DateTime? LastSyncAt,
    string? LastSyncError,
    List<GoogleCalendarDto> Calendars);

public record GoogleConnectRequest(string? ReturnUrl);

public record GoogleConnectResponse(string Url);

public record GoogleSelectionDto(List<string> CalendarIds);
