using RotationDating.Web.Models;

namespace RotationDating.Web.Services;

public static class VenueHelper
{
    public const string UnoParam = "uno";
    public const string SuseongParam = "suseong";

    public static EventVenue? TryParse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        UnoParam => EventVenue.UnoCoffee,
        SuseongParam => EventVenue.HotelSuseongSquare,
        _ => null
    };

    public static string ToParam(EventVenue venue) => venue switch
    {
        EventVenue.UnoCoffee => UnoParam,
        EventVenue.HotelSuseongSquare => SuseongParam,
        _ => UnoParam
    };

    public static EventKind ToEventKind(EventVenue venue) => venue switch
    {
        EventVenue.HotelSuseongSquare => EventKind.DatePoll,
        _ => EventKind.FixedDate
    };

    public static EventVenue FromEventKind(EventKind kind) =>
        kind == EventKind.DatePoll ? EventVenue.HotelSuseongSquare : EventVenue.UnoCoffee;

    public static string DisplayName(EventVenue venue) => venue switch
    {
        EventVenue.UnoCoffee => "우노커피",
        EventVenue.HotelSuseongSquare => "호텔수성스퀘어",
        _ => ""
    };

    public static string ShortDescription(EventVenue venue) => venue switch
    {
        EventVenue.UnoCoffee => "날짜가 정해진 행사",
        EventVenue.HotelSuseongSquare => "여러 날 중 하루 확정",
        _ => ""
    };

    public static string LocationName(EventVenue venue) => DisplayName(venue);

    public static string VenueQuery(EventVenue venue) => $"venue={ToParam(venue)}";

    public static string VenueQuery(EventKind kind) => VenueQuery(FromEventKind(kind));

    public static string AdminHomeUrl(EventVenue? venue = null) =>
        venue.HasValue ? $"/home?{VenueQuery(venue.Value)}" : "/home";

    public static string AdminPageUrl(
        string path,
        EventVenue venue,
        int? eventId = null,
        string? extraQuery = null,
        string? fragment = null)
    {
        var query = new List<string> { VenueQuery(venue) };
        if (eventId.HasValue)
            query.Add($"eventId={eventId.Value}");
        if (!string.IsNullOrWhiteSpace(extraQuery))
            query.Add(extraQuery.TrimStart('?', '&'));

        var url = $"{path}?{string.Join("&", query)}";
        if (!string.IsNullOrWhiteSpace(fragment))
            url += fragment.StartsWith('#') ? fragment : $"#{fragment}";
        return url;
    }

    public static IEnumerable<Event> FilterByVenue(IEnumerable<Event> events, EventVenue venue) =>
        events.Where(e => e.Kind == ToEventKind(venue));
}
