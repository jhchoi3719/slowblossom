using ClosedXML.Excel;
using RotationDating.Web.Models;

namespace RotationDating.Web.Services;

public static class ApplicationExcelExporter
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static byte[] Build(Event evt, IReadOnlyList<ParticipantApplication> applications)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("신청서");
        var includeAvailability = evt.Kind == EventKind.DatePoll;
        var headers = BuildHeaders(includeAvailability);

        for (var col = 0; col < headers.Count; col++)
            sheet.Cell(1, col + 1).SetValue(headers[col]);

        var header = sheet.Range(1, 1, 1, headers.Count);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#F4D6DE");
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        for (var i = 0; i < applications.Count; i++)
            WriteRow(sheet, i + 2, evt, applications[i], includeAvailability);

        sheet.SheetView.FreezeRows(1);
        sheet.RangeUsed()?.SetAutoFilter();
        sheet.Columns().AdjustToContents(8, 28);
        sheet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static string FileName(Event evt)
    {
        var date = EventDateHelper.GetEffectiveLoginDate(evt) ?? evt.EventDate;
        var raw = $"참가자신청서_{date:yyyyMMdd}.xlsx";
        var invalid = Path.GetInvalidFileNameChars();
        return new string(raw.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }

    private static List<string> BuildHeaders(bool includeAvailability)
    {
        var headers = new List<string>
        {
            "이름",
            "생년월일",
            "성별",
            "연락처",
            "거주지",
            "직장"
        };

        if (includeAvailability)
            headers.Add("가능일");

        headers.AddRange(
        [
            "원하는 연령대",
            "관심사",
            "음주",
            "흡연",
            "연락",
            "확정",
            "메모",
            "로그인 ID",
            "신청일시"
        ]);
        return headers;
    }

    private static void WriteRow(
        IXLWorksheet sheet,
        int row,
        Event evt,
        ParticipantApplication app,
        bool includeAvailability)
    {
        var col = 1;
        SetText(sheet, row, col++, app.Name);
        SetText(sheet, row, col++, app.BirthDate);
        SetText(sheet, row, col++, app.Gender);
        SetText(sheet, row, col++, app.Phone);
        SetText(sheet, row, col++, app.Residence);
        SetText(sheet, row, col++, app.Workplace);

        if (includeAvailability)
        {
            SetText(sheet, row, col++, EventDateHelper.FormatAvailableDates(
                app.Availabilities.Select(a => a.AvailableDate)));
        }

        SetText(sheet, row, col++, app.PreferredAgeRange);
        SetText(sheet, row, col++, app.Interests);
        SetText(sheet, row, col++, ParticipantApplication.OxLabel(app.Drinking));
        SetText(sheet, row, col++, ParticipantApplication.OxLabel(app.Smoking));
        SetText(sheet, row, col++, ParticipantApplication.OxLabel(app.AllowContact));
        SetText(sheet, row, col++, app.IsConfirmed ? "확정" : "미확정");
        SetText(sheet, row, col++, app.Memo);
        SetText(sheet, row, col++, LoginId(evt, app));
        sheet.Cell(row, col).SetValue(app.CreatedAt);
        sheet.Cell(row, col).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";
    }

    private static string? LoginId(Event evt, ParticipantApplication app)
    {
        if (!app.IsConfirmed)
            return null;

        if (evt.Kind == EventKind.FixedDate)
            return ParticipantAuthService.FormatLoginId(app.Name, evt.EventDate);

        if (evt.FinalizedDate.HasValue
            && app.Availabilities.Any(a => a.AvailableDate.Date == evt.FinalizedDate.Value.Date))
            return ParticipantAuthService.FormatLoginId(app.Name, evt.FinalizedDate.Value);

        return null;
    }

    private static void SetText(IXLWorksheet sheet, int row, int col, string? value)
    {
        var cell = sheet.Cell(row, col);
        cell.Style.NumberFormat.Format = "@";
        cell.SetValue(value ?? "");
    }
}
