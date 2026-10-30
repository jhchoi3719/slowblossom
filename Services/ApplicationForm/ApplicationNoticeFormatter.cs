namespace RotationDating.Web.Services.ApplicationForm;

public enum NoticeBlockKind
{
    Heading,
    Bullet,
    Note,
    Paragraph
}

public sealed record NoticeBlock(NoticeBlockKind Kind, string Text);

// 안내문을 제목([참가 대상]) · 목록(•) · 참고(※) · 문단으로 나눠 읽기 좋게 보여준다.
// 줄바꿈이 사라진 채 저장된 안내문도 표시 기호와 문장 끝을 기준으로 다시 나눈다.
public static class ApplicationNoticeFormatter
{
    private static readonly string[] SentenceEndEmoji = ["🌸", "🌷", "🙏", "🙂"];

    // 기본 안내문 각 줄의 첫 두 단어: 줄바꿈 없이 붙어 버린 글에서 원래 줄이 시작되던 자리를 찾는다.
    private static readonly HashSet<string> KnownLineStarts = ApplicationNoticeDefaults.DefaultNotice
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(line => !IsMarker(line))
        .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        .Where(words => words.Length >= 2)
        .Select(words => $"{words[0]} {words[1]}")
        .ToHashSet();

    public static List<NoticeBlock> Parse(string? text)
    {
        var normalized = (text ?? "").Replace("\r", "");
        var lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 1)
            lines = Unflatten(lines[0]);

        var blocks = new List<NoticeBlock>();
        foreach (var line in lines)
            AddLine(blocks, line);
        return blocks;
    }

    // 관리 화면 입력칸용: 줄바꿈이 사라진 안내문도 줄을 나눈 글로 돌려준다.
    public static string ToEditableText(string? text)
    {
        var blocks = Parse(text);
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < blocks.Count; i++)
        {
            if (i > 0)
                builder.Append(blocks[i].Kind == NoticeBlockKind.Bullet && blocks[i - 1].Kind == NoticeBlockKind.Bullet ? "\n" : "\n\n");
            builder.Append(blocks[i].Kind switch
            {
                NoticeBlockKind.Heading => $"[{blocks[i].Text}]",
                NoticeBlockKind.Bullet => $"• {blocks[i].Text}",
                _ => blocks[i].Text
            });
        }
        return builder.ToString();
    }

    private static bool IsMarker(string token) =>
        token.StartsWith('[') || token.StartsWith('•') || token.StartsWith('※');

    private static string[] Unflatten(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<List<string>> { new() };
        for (var i = 0; i < words.Length; i++)
        {
            var startsLine = IsMarker(words[i])
                || (i + 1 < words.Length && KnownLineStarts.Contains($"{words[i]} {words[i + 1]}"));
            if (startsLine && lines[^1].Count > 0)
                lines.Add([]);
            lines[^1].Add(words[i]);
        }
        return lines.Select(l => string.Join(' ', l)).ToArray();
    }

    private static void AddLine(List<NoticeBlock> blocks, string line)
    {
        if (line.StartsWith('[') && line.IndexOf(']') is var close and > 0)
        {
            blocks.Add(new NoticeBlock(NoticeBlockKind.Heading, line[1..close].Trim()));
            var rest = line[(close + 1)..].Trim();
            if (rest.Length > 0)
                AddLine(blocks, rest);
            return;
        }

        if (line.StartsWith('•'))
        {
            blocks.Add(new NoticeBlock(NoticeBlockKind.Bullet, line.TrimStart('•').Trim()));
            return;
        }

        var sentences = SplitSentences(line);
        if (line.StartsWith('※'))
        {
            blocks.Add(new NoticeBlock(NoticeBlockKind.Note, sentences[0]));
            sentences.RemoveAt(0);
        }

        blocks.AddRange(sentences.Select(s => new NoticeBlock(NoticeBlockKind.Paragraph, s)));
    }

    private static List<string> SplitSentences(string line)
    {
        var sentences = new List<string>();
        var current = new List<string>();
        foreach (var word in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            current.Add(word);
            var endsSentence = word.EndsWith('.') || word.EndsWith('!') || word.EndsWith('?') || word.EndsWith(":)")
                || (current.Count > 1 && SentenceEndEmoji.Contains(word));
            if (endsSentence)
            {
                sentences.Add(string.Join(' ', current));
                current.Clear();
            }
        }
        if (current.Count > 0)
            sentences.Add(string.Join(' ', current));
        return sentences;
    }
}
