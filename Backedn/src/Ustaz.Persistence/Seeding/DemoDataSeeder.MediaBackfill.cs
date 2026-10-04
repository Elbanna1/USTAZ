using Microsoft.EntityFrameworkCore;

namespace Ustaz.Persistence.Seeding;

internal sealed partial class DemoDataSeeder
{
    /// <summary>
    /// The prefix every pre-<see cref="DemoMedia"/> seeding run wrote into its media columns.
    ///
    /// Nothing serves it. It is not the public upload route — that is
    /// <c>LocalMediaService.PublicRequestPath</c>, <c>/uploads</c> — so a row still carrying
    /// one of these is a broken image or a dead download link, every time.
    /// </summary>
    private const string LegacyMediaPrefix = "/storage/demo/";

    /// <summary>
    /// Repairs media URLs that an earlier seeding run left pointing at nothing.
    ///
    /// This is the one place the seeder updates a column it did not just insert, and the
    /// reason is that the normal rule cannot reach these rows. Insertion is keyed by
    /// <see cref="SeedId"/>, so a row seeded before the media catalogue existed is "already
    /// present" forever: a re-run skips it and its dead URL survives every subsequent seed.
    ///
    /// The match is deliberately narrow. Only a value that still begins with
    /// <see cref="LegacyMediaPrefix"/> is touched, which means:
    ///
    /// <list type="bullet">
    /// <item>A genuine upload is never overwritten — those live under <c>/uploads/</c>.</item>
    /// <item>A URL an operator set by hand is never overwritten.</item>
    /// <item>A row already carrying a catalogue URL is not rewritten, so the second run of
    /// this method updates nothing and reports zero. That is what makes it idempotent.</item>
    /// </list>
    ///
    /// Runs last, after every module has had its chance to insert, so it sees the final set
    /// of rows rather than racing the modules that create them.
    /// </summary>
    private async Task BackfillLegacyMediaAsync(CancellationToken cancellationToken)
    {
        var updated = 0;

        updated += await RepairAsync(
            context.Users.Where(user => user.ProfileImageUrl!.StartsWith(LegacyMediaPrefix)),
            user => user.ProfileImageUrl = DemoMedia.Avatar(user.Id.ToString("N")),
            cancellationToken);

        updated += await RepairAsync(
            context.TeacherProfiles.Where(teacher =>
                teacher.ProfileImageUrl!.StartsWith(LegacyMediaPrefix)
                || teacher.CoverImageUrl!.StartsWith(LegacyMediaPrefix)),
            teacher =>
            {
                // The teacher's own key is not stored on the row, so the subject-matched
                // cover is resolved from the subject the profile actually teaches.
                var key = TeacherKeyFor(teacher.Id);

                if (teacher.ProfileImageUrl?.StartsWith(LegacyMediaPrefix, StringComparison.Ordinal) == true)
                {
                    teacher.ProfileImageUrl = DemoMedia.Avatar(key ?? teacher.Id.ToString("N"));
                }

                if (teacher.CoverImageUrl?.StartsWith(LegacyMediaPrefix, StringComparison.Ordinal) == true)
                {
                    teacher.CoverImageUrl = DemoMedia.TeacherCover(key ?? string.Empty);
                }
            },
            cancellationToken);

        updated += await RepairAsync(
            context.Courses.Where(course => course.ThumbnailUrl!.StartsWith(LegacyMediaPrefix)),
            course => course.ThumbnailUrl = DemoMedia.CourseThumbnail(CourseKeyFor(course.Id) ?? string.Empty),
            cancellationToken);

        updated += await RepairAsync(
            context.CourseLessons
                .Where(lesson => lesson.VideoUrl!.StartsWith(LegacyMediaPrefix))
                .Include(lesson => lesson.Section),
            lesson => lesson.VideoUrl = DemoMedia.LessonVideo(
                CourseKeyFor(lesson.Section.CourseId) ?? string.Empty,
                lesson.Section.SortOrder,
                lesson.SortOrder),
            cancellationToken);

        updated += await RepairAsync(
            context.GroupMaterials
                .Where(material => material.FileUrl.StartsWith(LegacyMediaPrefix))
                .Include(material => material.Group),
            material => material.FileUrl =
                DemoMedia.SubjectDocument(SubjectKey(material.Group.SubjectId), material.SortOrder),
            cancellationToken);

        updated += await RepairAsync(
            context.HomeworkAttachments
                .Where(attachment => attachment.FileUrl.StartsWith(LegacyMediaPrefix))
                .Include(attachment => attachment.Homework),
            attachment => attachment.FileUrl =
                DemoMedia.SubjectDocument(SubjectKey(attachment.Homework.SubjectId), 2),
            cancellationToken);

        updated += await RepairAsync(
            context.HomeworkSubmissions
                .Where(submission => submission.FileUrl!.StartsWith(LegacyMediaPrefix))
                .Include(submission => submission.Homework),
            submission => submission.FileUrl =
                DemoMedia.SubjectDocument(SubjectKey(submission.Homework.SubjectId), 1),
            cancellationToken);

        updated += await RepairAsync(
            context.TenantWebsites.Where(website =>
                website.HeroImageUrl!.StartsWith(LegacyMediaPrefix)
                || website.SeoImageUrl!.StartsWith(LegacyMediaPrefix)),
            website =>
            {
                var key = TenantKeyFor(website.TenantId);

                if (website.HeroImageUrl?.StartsWith(LegacyMediaPrefix, StringComparison.Ordinal) == true)
                {
                    website.HeroImageUrl = DemoMedia.WebsiteHero(key ?? string.Empty);
                }

                if (website.SeoImageUrl?.StartsWith(LegacyMediaPrefix, StringComparison.Ordinal) == true)
                {
                    website.SeoImageUrl = DemoMedia.WebsiteHero(key ?? string.Empty);
                }
            },
            cancellationToken);

        updated += await RepairAsync(
            context.WebsiteSections.Where(section => section.ImageUrl!.StartsWith(LegacyMediaPrefix)),
            section => section.ImageUrl = DemoMedia.WebsiteSectionImage(section.Type.ToString()),
            cancellationToken);

        updated += await RepairAsync(
            context.TeacherAchievements.Where(achievement =>
                achievement.ImageUrl!.StartsWith(LegacyMediaPrefix)),
            achievement => achievement.ImageUrl = DemoMedia.AchievementImage(achievement.SortOrder),
            cancellationToken);

        updated += await RepairAsync(
            context.MessageAttachments.Where(attachment =>
                attachment.FileUrl.StartsWith(LegacyMediaPrefix)),
            attachment => attachment.FileUrl =
                DemoMedia.SubjectDocument(SubjectKey(_groups["center-math-3sec"].SubjectId), 2),
            cancellationToken);

        updated += await RepairBrandingAsync(cancellationToken);

        report.Record(
            "LegacyMediaBackfill",
            0,
            updated: updated,
            note: updated == 0 ? "nothing left pointing at /storage/demo" : "dead media URLs repaired");

        await UpgradeCommonsVideoAsync(cancellationToken);
    }

    private const string CommonsFilePrefix = "https://upload.wikimedia.org/wikipedia/commons/";

    /// <summary>
    /// Points a lesson at Commons' scaled rendition of its video instead of the uploaded
    /// original.
    ///
    /// An earlier run stored the original file, which on this dataset means anything up to a
    /// gigabyte behind a single lesson. Commons also publishes a scaled VP9 copy of the same
    /// footage — same content, same licence, a fraction of the bytes — and
    /// <see cref="DemoMedia"/> now names that one.
    ///
    /// The replacement is read from the catalogue rather than derived by rewriting the path,
    /// because the rendition is not uniform: nearly every file has a 480p copy, one has only
    /// 360p, and guessing would hand a lesson a URL that 404s. The catalogue already records
    /// which rendition actually exists for each.
    ///
    /// Only a non-transcoded <c>upload.wikimedia.org</c> URL is replaced, so a real upload or
    /// a link a teacher entered is never touched.
    /// </summary>
    private async Task UpgradeCommonsVideoAsync(CancellationToken cancellationToken)
    {
        var originals = await context.CourseLessons
            .Where(lesson =>
                lesson.VideoUrl!.StartsWith(CommonsFilePrefix)
                && !lesson.VideoUrl.Contains("/transcoded/"))
            .Include(lesson => lesson.Section)
            .ToListAsync(cancellationToken);

        if (originals.Count == 0)
        {
            report.Record("CommonsVideoRendition", 0, note: "already on the scaled rendition");
            return;
        }

        var upgraded = 0;

        foreach (var lesson in originals)
        {
            var courseKey = CourseKeyFor(lesson.Section.CourseId);

            if (courseKey is null)
            {
                // Not one of the seeded demo courses; leave it alone.
                continue;
            }

            lesson.VideoUrl = DemoMedia.LessonVideo(
                courseKey,
                lesson.Section.SortOrder,
                lesson.SortOrder);

            upgraded++;
        }

        await context.SaveChangesAsync(cancellationToken);

        report.Record(
            "CommonsVideoRendition",
            0,
            updated: upgraded,
            note: "originals swapped for the scaled rendition");
    }

    /// <summary>
    /// Branding is its own step because the monogram needs the workspace's name and colour,
    /// which live on two different rows.
    /// </summary>
    private async Task<int> RepairBrandingAsync(CancellationToken cancellationToken)
    {
        var stale = await context.TenantBrandings
            .Where(branding =>
                branding.LogoUrl!.StartsWith(LegacyMediaPrefix)
                || branding.CoverImageUrl!.StartsWith(LegacyMediaPrefix)
                || branding.FaviconUrl!.StartsWith(LegacyMediaPrefix))
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
        {
            return 0;
        }

        foreach (var branding in stale)
        {
            var key = TenantKeyFor(branding.TenantId);
            var tenant = key is null ? null : _tenants[key];
            var colour = branding.PrimaryColorHex ?? "#2563EB";

            if (branding.LogoUrl?.StartsWith(LegacyMediaPrefix, StringComparison.Ordinal) == true)
            {
                branding.LogoUrl = DemoMedia.Monogram(tenant?.DisplayName ?? tenant?.Name ?? "USTAZ", colour);
            }

            if (branding.CoverImageUrl?.StartsWith(LegacyMediaPrefix, StringComparison.Ordinal) == true)
            {
                branding.CoverImageUrl = DemoMedia.BrandingCover(key ?? string.Empty);
            }

            if (branding.FaviconUrl?.StartsWith(LegacyMediaPrefix, StringComparison.Ordinal) == true)
            {
                branding.FaviconUrl = DemoMedia.Monogram(tenant?.Slug ?? "ustaz", colour);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return stale.Count;
    }

    /// <summary>
    /// Loads the rows matching <paramref name="stale"/>, applies <paramref name="repair"/> to
    /// each, and saves. Returns 0 without touching the database when nothing matches, which
    /// is the steady state after the first run.
    /// </summary>
    private async Task<int> RepairAsync<TEntity>(
        IQueryable<TEntity> stale,
        Action<TEntity> repair,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var rows = await stale.ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return 0;
        }

        foreach (var row in rows)
        {
            repair(row);
        }

        await context.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    // -----------------------------------------------------------------------
    // Reverse lookups: row id back to the seed key its media is chosen by
    // -----------------------------------------------------------------------

    private string? TenantKeyFor(Guid tenantId) =>
        _tenants.FirstOrDefault(entry => entry.Value.Id == tenantId).Key;

    private string? TeacherKeyFor(Guid teacherId) =>
        _teachers.FirstOrDefault(entry => entry.Value.Id == teacherId).Key;

    private string? CourseKeyFor(Guid courseId) =>
        _courses.FirstOrDefault(entry => entry.Value.Id == courseId).Key;
}
