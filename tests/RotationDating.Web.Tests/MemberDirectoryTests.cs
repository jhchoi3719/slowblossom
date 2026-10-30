using RotationDating.Web.Models;
using RotationDating.Web.Services;

namespace RotationDating.Web.Tests;

public class MemberDirectoryTests
{
    private static readonly Event Uno1 = new() { Id = 1, Venue = EventVenue.UnoCoffee, Kind = EventKind.FixedDate, EventDate = new DateTime(2026, 9, 5) };
    private static readonly Event Uno2 = new() { Id = 2, Venue = EventVenue.UnoCoffee, Kind = EventKind.FixedDate, EventDate = new DateTime(2026, 10, 3) };
    private static readonly Event Suseong = new() { Id = 3, Venue = EventVenue.HotelSuseongSquare, Kind = EventKind.DatePoll, EventDate = new DateTime(2026, 8, 1), Title = "8월 호텔수성스퀘어" };

    private static ParticipantApplication App(int id, Event evt, string name, string gender, string? phone, bool confirmed = false) => new()
    {
        Id = id,
        EventId = evt.Id,
        Event = evt,
        Name = name,
        Gender = gender,
        Phone = phone,
        BirthDate = "19950219",
        IsConfirmed = confirmed,
        CreatedAt = evt.EventDate.AddDays(-7)
    };

    private static readonly List<ParticipantApplication> Apps =
    [
        App(1, Uno1, "홍길동", "남", "010-1111-2222", confirmed: true),
        App(2, Uno2, "홍길동", "남", "01011112222"),
        App(3, Suseong, "김영희", "여", "01033334444", confirmed: true),
        App(4, Uno2, "이철수", "남", "01055556666"),
        App(5, Suseong, "박지민", "여", null)
    ];

    [Fact]
    public void Same_phone_is_one_member_with_full_history()
    {
        var list = MemberDirectory.Build(Apps, venue: null);

        var hong = Assert.Single(list.Male, m => m.Name == "홍길동");
        Assert.Equal(2, hong.History.Count);
        Assert.Equal(1, hong.ConfirmedCount);
        Assert.Equal(2, list.Male.Count);
        Assert.Equal(2, list.Female.Count);
    }

    [Fact]
    public void Venue_filter_keeps_members_who_applied_there()
    {
        var uno = MemberDirectory.Build(Apps, EventVenue.UnoCoffee);
        Assert.Equal(["이철수", "홍길동"], uno.Male.Select(m => m.Name).Order());
        Assert.Empty(uno.Female);

        var suseong = MemberDirectory.Build(Apps, EventVenue.HotelSuseongSquare);
        Assert.Empty(suseong.Male);
        Assert.Equal(2, suseong.Female.Count);
    }

    [Fact]
    public void Most_recent_application_comes_first()
    {
        var list = MemberDirectory.Build(Apps, venue: null);
        Assert.Equal("이철수", list.Male[0].Name == "홍길동" ? list.Male[1].Name : list.Male[0].Name);
        Assert.Equal(new DateTime(2026, 10, 3).AddDays(-7), list.Male[0].LastAppliedAt);
    }

    [Fact]
    public void Search_matches_name_or_phone_digits()
    {
        Assert.Single(MemberDirectory.Build(Apps, null, "길동").Male);
        Assert.Single(MemberDirectory.Build(Apps, null, "3333").Female);
    }

    [Fact]
    public void PollDateCount_splits_by_gender()
    {
        var day = new DateTime(2026, 11, 7);
        var evt = new Event { Kind = EventKind.DatePoll };
        evt.Applications =
        [
            new ParticipantApplication { Gender = "남", Availabilities = [new ApplicationAvailability { AvailableDate = day }] },
            new ParticipantApplication { Gender = "여", Availabilities = [new ApplicationAvailability { AvailableDate = day }] },
            new ParticipantApplication { Gender = "여", Availabilities = [new ApplicationAvailability { AvailableDate = day.AddDays(7) }] }
        ];

        var count = EventDateHelper.CountAvailable(evt, day);

        Assert.Equal(1, count.Male);
        Assert.Equal(1, count.Female);
        Assert.Equal(2, count.Total);
    }
}
