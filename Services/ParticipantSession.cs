using System.Security.Claims;
using RotationDating.Web.Models;

namespace RotationDating.Web.Services;

public sealed class ParticipantSession
{
    public int ApplicationId { get; init; }
    public int EventId { get; init; }
    public string Name { get; init; } = "";
    public string Gender { get; init; } = "";
    public string EventTitle { get; init; } = "";
    public string VenueLabel { get; init; } = "";

    public static ParticipantSession? FromClaims(ClaimsPrincipal user)
    {
        if (!user.IsInRole(AuthRoles.Participant))
            return null;

        if (!int.TryParse(user.FindFirst(AuthClaims.ApplicationId)?.Value, out var applicationId))
            return null;

        if (!int.TryParse(user.FindFirst(AuthClaims.EventId)?.Value, out var eventId))
            return null;

        return new ParticipantSession
        {
            ApplicationId = applicationId,
            EventId = eventId,
            Name = user.Identity?.Name ?? "",
            Gender = user.FindFirst(AuthClaims.Gender)?.Value ?? "",
            EventTitle = user.FindFirst(AuthClaims.EventTitle)?.Value ?? "",
            VenueLabel = user.FindFirst(AuthClaims.VenueLabel)?.Value ?? ""
        };
    }

    public string OppositeGender => Gender switch
    {
        "남" => "여",
        "여" => "남",
        _ => ""
    };

    public bool IsHotelSuseongSquare =>
        VenueLabel.Contains("호텔수성스퀘어", StringComparison.Ordinal);
}
