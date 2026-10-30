using ClosedXML.Excel;
using RotationDating.Web.Models;
using RotationDating.Web.Services;

namespace RotationDating.Web.Tests;

public class ApplicationExcelExporterTests
{
    [Fact]
    public void Export_includes_web_application_columns()
    {
        var evt = new Event { Kind = EventKind.FixedDate, EventDate = new DateTime(2026, 11, 7) };
        var app = new ParticipantApplication
        {
            Name = "홍길동",
            KakaoId = "hong95",
            Education = "경북대학교",
            ReferralSource = "인스타",
            Motivation = "동기",
            SelfIntro = "소개",
            IsReturning = true,
            Source = "web"
        };

        using var stream = new MemoryStream(ApplicationExcelExporter.Build(evt, [app]));
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        var row = sheet.Row(1).CellsUsed().ToDictionary(c => c.GetString(), c => sheet.Cell(2, c.Address.ColumnNumber).GetString());

        Assert.Equal("hong95", row["카카오톡 ID"]);
        Assert.Equal("경북대학교", row["최종학력"]);
        Assert.Equal("인스타", row["참여경로"]);
        Assert.Equal("동기", row["신청동기"]);
        Assert.Equal("소개", row["자기소개"]);
        Assert.Equal("O", row["기존 참가자"]);
        Assert.Equal("홈페이지", row["신청 경로"]);
    }
}
