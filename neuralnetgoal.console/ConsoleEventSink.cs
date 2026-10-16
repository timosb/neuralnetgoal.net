using NeuralNetGoal.Parser.Events;

namespace NeuralNetGoal.ConsoleApp;

/// <summary>Prints the parser's live events, one line per match, so you can watch the scrape like the old console did.</summary>
public sealed class ConsoleEventSink(bool verbose) : IScrapeEventSink
{
    private readonly object _lock = new();

    public void Emit(ScrapeEvent e)
    {
        if (e.Level == ScrapeLevel.Debug && !verbose) return;
        // MatchStarted is only interesting when debugging; the outcome line (parsed / failed) follows anyway
        if (e.Kind is ScrapeEventKind.MatchStarted or ScrapeEventKind.PageFetched && !verbose) return;

        var color = e.Level switch
        {
            ScrapeLevel.Error => ConsoleColor.Red,
            ScrapeLevel.Warn => ConsoleColor.Yellow,
            ScrapeLevel.Debug => ConsoleColor.DarkGray,
            _ => e.Kind == ScrapeEventKind.MatchParsed ? ConsoleColor.Green : ConsoleColor.Gray,
        };

        var progress = e.Counters is { Total: > 0 } c ? $"[{c.Done + c.Failed + c.Skipped}/{c.Total}] " : "";
        var teams = e.Home is null ? "" : $"{e.Home} - {e.Away}  ";
        var line = $"{e.At.ToLocalTime():HH:mm:ss} {progress}{e.Kind,-17} {teams}{e.Message}";

        lock (_lock)
        {
            var old = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(line);
            Console.ForegroundColor = old;
        }
    }
}
