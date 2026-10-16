using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace NeuralNetGoal.Parser.Parsing;

internal static class ParserHelpers
{
    private static readonly Regex Pair = new(@"(\d+)\s*:\s*(\d+)", RegexOptions.Compiled);

    public static HtmlDocument Load(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    public static string Text(HtmlNode? node) =>
        node is null ? "" : HtmlEntity.DeEntitize(node.InnerText).Trim();

    /// <summary>The cells (td) of a table row; header rows (th first) yield an empty list.</summary>
    public static List<HtmlNode> Cells(HtmlNode tr)
    {
        var first = tr.ChildNodes.FirstOrDefault(n => n.NodeType == HtmlNodeType.Element);
        if (first is null || first.Name == "th") return [];
        return tr.ChildNodes.Where(n => n.Name == "td").ToList();
    }

    public static string? Href(HtmlNode? cell) =>
        cell?.SelectSingleNode(".//a[@href]")?.GetAttributeValue("href", null);

    /// <summary>The match link of a list row and the "Home - Away" label built from its spans.</summary>
    public static (string url, string teams)? MatchLink(HtmlNode tr)
    {
        var a = tr.SelectSingleNode($".//a[@href][contains(concat(' ', normalize-space(@class), ' '), ' {Selectors.MatchLinkClass} ')]")
                ?? tr.SelectSingleNode(".//a[@href]");
        var href = a?.GetAttributeValue("href", null);
        if (a is null || string.IsNullOrWhiteSpace(href)) return null;

        var spans = a.ChildNodes.Where(n => n.Name == "span").ToList();
        var teams = spans.Count >= 2 ? $"{Text(spans[0])} - {Text(spans[1])}" : Text(a);
        return (href, teams);
    }

    /// <summary>The 1 / X / 2 odds cells of a row, found by class (not by position).</summary>
    public static (decimal? home, decimal? draw, decimal? away) RowOdds(HtmlNode tr)
    {
        var cells = tr.ChildNodes.Where(n => n.HasClass(Selectors.OddsCellClass)).ToList();
        decimal? At(int i) => i < cells.Count ? Odd(cells[i]) : null;
        return (At(0), At(1), At(2));
    }

    /// <summary>Reads the odds attribute of a cell. "colored" cells nest the real value deeper (legacy behaviour).</summary>
    public static decimal? Odd(HtmlNode? cell)
    {
        if (cell is null) return null;

        string? raw;
        if (cell.HasClass(Selectors.ColoredCellClass))
        {
            raw = cell.SelectNodes($".//*[@{Selectors.OddAttribute}]")?.LastOrDefault()?.GetAttributeValue(Selectors.OddAttribute, null)
                  ?? cell.GetAttributeValue(Selectors.OddAttribute, null);
        }
        else
        {
            raw = cell.GetAttributeValue(Selectors.OddAttribute, null)
                  ?? cell.SelectNodes($".//*[@{Selectors.OddAttribute}]")?.FirstOrDefault()?.GetAttributeValue(Selectors.OddAttribute, null);
        }

        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    /// <summary>Parses the site's "data-dt" value: "day,month,year,hour,minute", e.g. "22,5,2022,17,00".</summary>
    public static DateTime? DataDt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var p = value.Split(',');
        if (p.Length < 5) return null;
        if (!int.TryParse(p[0], out var d) || !int.TryParse(p[1], out var mo) || !int.TryParse(p[2], out var y)
            || !int.TryParse(p[3], out var h) || !int.TryParse(p[4], out var mi)) return null;
        try { return new DateTime(y, mo, d, h, mi, 0); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    /// <summary>First "a:b" pair found in the text, e.g. "2:1" or "(1:0, 1:1)" -> (1,0).</summary>
    public static (int home, int away)? FirstPair(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var m = Pair.Match(text);
        return m.Success ? (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)) : null;
    }
}
