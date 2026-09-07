using RotationDating.Web.Models;

namespace RotationDating.Web.Services;

public static class VenueHelper
{
    public const string UnoParam = "uno";
    public const string SuseongParam = "suseong";
    public const string StayYeonParam = "stayyeon";

    public static EventVenue? TryParse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        UnoParam => EventVenue.UnoCoffee,
        SuseongParam => EventVenue.HotelSuseongSquare,
        StayYeonParam => EventVenue.StayYeon,
        _ => null
    };

    public static string ToParam(EventVenue venue) => venue switch
    {
        EventVenue.UnoCoffee => UnoParam,
        EventVenue.HotelSuseongSquare => SuseongParam,
        EventVenue.StayYeon => StayYeonParam,
        _ => UnoParam
    };

    public static EventKind ToEventKind(EventVenue venue) => venue switch
    {
        EventVenue.HotelSuseongSquare => EventKind.DatePoll,
        _ => EventKind.FixedDate
    };

    public static EventVenue FromEventKind(EventKind kind) =>
        kind == EventKind.DatePoll ? EventVenue.HotelSuseongSquare : EventVenue.UnoCoffee;

    public static EventVenue FromEvent(Event evt) =>
        Enum.IsDefined(typeof(EventVenue), evt.Venue) ? evt.Venue : FromEventKind(evt.Kind);

    public static EventVenue? FromDisplayName(string? name) => name?.Trim() switch
    {
        "우노커피" => EventVenue.UnoCoffee,
        "호텔수성스퀘어" => EventVenue.HotelSuseongSquare,
        "스테이연" => EventVenue.StayYeon,
        _ => null
    };

    public static bool IsFixedDateVenue(EventVenue venue) =>
        venue is EventVenue.UnoCoffee or EventVenue.StayYeon;

    public static bool SupportsMidVote(EventVenue venue) =>
        venue is EventVenue.UnoCoffee or EventVenue.StayYeon;

    public static string MidVoteDisplayName(EventVenue venue) => venue switch
    {
        EventVenue.StayYeon => "첫인상 투표",
        _ => "중간투표"
    };

    public static string DisplayName(EventVenue venue) => venue switch
    {
        EventVenue.UnoCoffee => "우노커피",
        EventVenue.HotelSuseongSquare => "호텔수성스퀘어",
        EventVenue.StayYeon => "스테이연",
        _ => ""
    };

    public static string ShortDescription(EventVenue venue) => venue switch
    {
        EventVenue.UnoCoffee => "날짜가 정해진 행사",
        EventVenue.HotelSuseongSquare => "여러 날 중 하루 확정",
        EventVenue.StayYeon => "날짜가 정해진 행사",
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
        events.Where(e => FromEvent(e) == venue);
}
