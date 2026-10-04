namespace Ustaz.Persistence.Seeding;

/// <summary>
/// What a seeding run is allowed to write.
///
/// The mode is the whole safety model: it is chosen by the caller, validated against the
/// hosting environment by <see cref="SeedSafety"/>, and each mode writes a strict superset of
/// nothing — <see cref="ReferenceData"/> never creates a person, and <see cref="DemoData"/>
/// never runs anywhere a real customer could see it.
/// </summary>
public enum SeedMode
{
    /// <summary>
    /// Lookup and configuration rows the application cannot function without: roles, the
    /// subject and grade catalogues, the SaaS price list. No accounts, no content, no money.
    /// Safe in every environment, production included.
    /// </summary>
    ReferenceData = 0,

    /// <summary>
    /// <see cref="ReferenceData"/> plus the one account a brand-new installation needs: the
    /// first platform administrator, provisioned from configuration. Requires an explicit
    /// confirmation flag, because it is the only mode that creates a privileged identity.
    /// </summary>
    ProductionBootstrap = 1,

    /// <summary>
    /// A complete, internally consistent fictional dataset for development and demos:
    /// workspaces, teachers, students, guardians, groups, courses, attendance, exams,
    /// homework, certificates, messages, notifications and tuition payments.
    ///
    /// Refused outside Development/Testing unless an operator has explicitly opted in.
    /// </summary>
    DemoData = 2,

    /// <summary>
    /// The small, fixed set of rows the automated integration tests assert against. Keyed by
    /// stable identifiers from <see cref="TestFixtureIds"/> so a test can reference a row
    /// without querying for it first. Refused outside Development/Testing, always.
    /// </summary>
    TestFixtures = 3
}
