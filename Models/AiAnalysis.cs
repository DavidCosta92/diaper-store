namespace panialera.Models;

// Enums below are stored as text, so adding values does not break old rows.

public enum AiAnalysisStatus
{
    Running,
    Succeeded,
    Failed,
    Cancelled
}

public enum AiFindingKind
{
    Recommendation,
    Trend,
    Warning,

    // A short sentence of "what to apply", the closing summary of the analysis.
    Action
}

// What the owner thought of a recommendation. It goes back to the AI in the next analysis.
public enum AiFindingFeedback
{
    None,
    Useful,
    NotUseful,
    Done
}

// One run of the AI analysis over the Insights numbers: what was sent, what came back, what it
// cost and, when it failed, why. Failed and cancelled runs are kept too, for traceability.
public class AiAnalysis
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    // Who ran it. No FK, like the audit log: the history outlives the user.
    public int? UserId { get; set; }
    public string? UserName { get; set; }

    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public AiAnalysisStatus Status { get; set; }

    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;       // the configured one
    public string? AnsweredBy { get; set; }                  // the one that answered (a fallback can)
    public string PromptVersion { get; set; } = string.Empty;

    // Exactly what was sent (anonymized numbers) and the raw answer, kept even when it could
    // not be read.
    public string InputJson { get; set; } = string.Empty;
    public string? OutputJson { get; set; }

    public string? Summary { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal CostUsd { get; set; }                     // estimated from the tokens
    public long DurationMs { get; set; }
    public string? StopReason { get; set; }
    public string? ResponseId { get; set; }

    // When it failed: AiErrorCode name, the message shown to the user and the technical detail.
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorDetail { get; set; }

    public List<AiFinding> Findings { get; set; } = new();
}

// A recommendation, trend or data warning returned by the AI.
public class AiFinding
{
    public int Id { get; set; }
    public int AiAnalysisId { get; set; }
    public AiAnalysis? Analysis { get; set; }

    public AiFindingKind Kind { get; set; }
    public int Position { get; set; }

    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;        // trend: the metric; warning: the text
    public string Detail { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;      // product, family or type it talks about
    public string? Direction { get; set; }                   // trends only
    public string? Confidence { get; set; }                  // trends only

    // Trends only: the month-by-month series that shows it, as "scope|name|metric" (see
    // AiTrendSeries). The chart draws the app's numbers, never values written by the AI.
    public string? Chart { get; set; }

    public AiFindingFeedback Feedback { get; set; }
    public DateTime? FeedbackAt { get; set; }
    public string? FeedbackBy { get; set; }

    // The owner's follow-up: what was done and what happened, oldest first. It goes back to the
    // AI too.
    public List<AiFindingNote> Notes { get; set; } = new();
}

// A dated note on a recommendation ("I ordered 4 packs", "they sold in two weeks, it worked").
public class AiFindingNote
{
    public const int MaxLength = 1000;

    public int Id { get; set; }
    public int AiFindingId { get; set; }
    public AiFinding? Finding { get; set; }

    public DateTime CreatedAt { get; set; }

    // Who wrote it. No FK, like the opinion: the note outlives the user.
    public string? UserName { get; set; }
    public string Text { get; set; } = string.Empty;
}

// A row of the analysis history (without the sent and received JSON, which can be long).
public sealed record AiAnalysisListItem(
    int Id,
    DateTime StartedAt,
    DateTime PeriodFrom,
    DateTime PeriodTo,
    AiAnalysisStatus Status,
    string Model,
    decimal CostUsd,
    string? UserName,
    string? ErrorMessage,
    int Recommendations);

public static class AiFindingFeedbackExtensions
{
    public static string DisplayName(this AiFindingFeedback f) => f switch
    {
        AiFindingFeedback.Useful => "Útil",
        AiFindingFeedback.NotUseful => "No sirve",
        AiFindingFeedback.Done => "Hecha",
        _ => "Sin opinión"
    };
}
