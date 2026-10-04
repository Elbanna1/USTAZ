using System.Text;

namespace Ustaz.Persistence.Seeding;

/// <summary>What happened to one entity type during a seeding run.</summary>
public sealed record SeedEntityOutcome
{
    public required string Entity { get; init; }

    public int Inserted { get; init; }

    public int Updated { get; init; }

    public int Skipped { get; init; }

    public string? Note { get; init; }
}

/// <summary>
/// The outcome of a seeding run, per entity.
///
/// Returned rather than only logged, so a test can assert on it and an operator can see what
/// a second run did (which should be: nothing but skips). Holds counts and entity names only
/// — never a credential, a token, or row content.
/// </summary>
public sealed class SeedReport(SeedMode mode)
{
    private readonly List<SeedEntityOutcome> _outcomes = [];
    private readonly List<string> _failures = [];

    public SeedMode Mode { get; } = mode;

    public IReadOnlyList<SeedEntityOutcome> Outcomes => _outcomes;

    /// <summary>Non-fatal problems: a module that could not run because its prerequisites were absent.</summary>
    public IReadOnlyList<string> Failures => _failures;

    public int TotalInserted => _outcomes.Sum(outcome => outcome.Inserted);

    public int TotalUpdated => _outcomes.Sum(outcome => outcome.Updated);

    public int TotalSkipped => _outcomes.Sum(outcome => outcome.Skipped);

    public bool WroteNothing => TotalInserted == 0 && TotalUpdated == 0;

    public void Record(string entity, int inserted, int updated = 0, int skipped = 0, string? note = null)
    {
        var existing = _outcomes.FindIndex(outcome => outcome.Entity == entity);

        if (existing >= 0)
        {
            var previous = _outcomes[existing];
            _outcomes[existing] = previous with
            {
                Inserted = previous.Inserted + inserted,
                Updated = previous.Updated + updated,
                Skipped = previous.Skipped + skipped,
                Note = note ?? previous.Note
            };

            return;
        }

        _outcomes.Add(new SeedEntityOutcome
        {
            Entity = entity,
            Inserted = inserted,
            Updated = updated,
            Skipped = skipped,
            Note = note
        });
    }

    public void RecordFailure(string message) => _failures.Add(message);

    /// <summary>Counts for one entity, or null when the run never touched it.</summary>
    public SeedEntityOutcome? For(string entity) =>
        _outcomes.FirstOrDefault(outcome => outcome.Entity == entity);

    /// <summary>A one-line-per-entity summary, safe to write to a log or a console.</summary>
    public string Summarize()
    {
        var builder = new StringBuilder();
        builder.Append("Seed run (").Append(Mode).Append("): ")
            .Append(TotalInserted).Append(" inserted, ")
            .Append(TotalUpdated).Append(" updated, ")
            .Append(TotalSkipped).Append(" skipped.");

        foreach (var outcome in _outcomes.Where(outcome =>
                     outcome.Inserted > 0 || outcome.Updated > 0 || outcome.Skipped > 0))
        {
            builder.AppendLine()
                .Append("  ").Append(outcome.Entity.PadRight(28))
                .Append(" +").Append(outcome.Inserted)
                .Append(" ~").Append(outcome.Updated)
                .Append(" =").Append(outcome.Skipped);

            if (outcome.Note is not null)
            {
                builder.Append("  (").Append(outcome.Note).Append(')');
            }
        }

        foreach (var failure in _failures)
        {
            builder.AppendLine().Append("  ! ").Append(failure);
        }

        return builder.ToString();
    }
}
