using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;

namespace Ustaz.Persistence.Seeding;

internal sealed partial class DemoDataSeeder
{
    /// <summary>
    /// Each workspace's public website.
    ///
    /// Four shapes, because the public site has four states worth rendering: published with a
    /// full set of sections and testimonials; published but with nothing to show because the
    /// workspace publishes no groups or courses; unpublished with partial content; and barely
    /// configured at all. A public endpoint that returns the same thing for all four is wrong,
    /// and these rows are how that gets caught.
    /// </summary>
    private async Task SeedWebsitesAsync(CancellationToken cancellationToken)
    {
        var websites = new List<TenantWebsite>();

        foreach (var (key, tenant) in _tenants)
        {
            // omar and dalia are the under-configured workspaces; nour is published but has
            // no published content behind it; tarek's teacher is not verified yet; and
            // yasmin, fatma and sameh teach at somebody else's centre, so their own
            // workspaces were never set up. Everything that is not published is minimal.
            var isPublished = key is "ahmed" or "mariam" or "center" or "nour";
            var isMinimal = !isPublished;

            websites.Add(new TenantWebsite
            {
                Id = SeedId.For("demo:website", key),
                TenantId = tenant.Id,
                IsPublished = isPublished,
                HeroTitle = tenant.DisplayName,
                HeroSubtitle = isMinimal ? null : "تعلم بثقة مع منصة أستاذ",
                HeroImageUrl = DemoMedia.WebsiteHero(key),
                HeroCtaLabel = isMinimal ? null : "احجز مكانك الآن",
                HeroCtaUrl = isMinimal ? null : "/" + tenant.Slug + "/groups",
                AboutTitle = isMinimal ? null : "نبذة عني",
                AboutBody = isMinimal
                    ? null
                    : tenant.DisplayName + " - خبرة طويلة في التدريس وشرح مبسط يركز على الفهم وبناء أساس قوي.",
                ContactEmail = tenant.ContactEmail,
                ContactPhone = tenant.ContactPhone,
                ContactAddress = isMinimal ? null : tenant.City,
                WhatsAppNumber = tenant.WhatsAppNumber,
                SeoTitle = tenant.DisplayName + " | أستاذ",
                SeoDescription = "احجز مكانك في مجموعات " + tenant.DisplayName + " على منصة أستاذ.",
                SeoKeywords = isMinimal ? null : "دروس خصوصية, " + tenant.City + ", أستاذ",
                // Open Graph needs its own image, and the hero is the right one to
                // share: it is what somebody following the link is about to see.
                SeoImageUrl = DemoMedia.WebsiteHero(key),
                CreatedAt = tenant.CreatedAt
            });
        }

        await AddMissingAsync("TenantWebsites", websites, cancellationToken);
        await SeedWebsiteSectionsAsync(cancellationToken);
        await SeedTestimonialsAsync(cancellationToken);
    }

    private async Task SeedWebsiteSectionsAsync(CancellationToken cancellationToken)
    {
        var sections = new List<WebsiteSection>();

        // The fully configured sites get the whole set; the "published but empty" one gets
        // only hero and contact, which is what a site with no catalogue should look like.
        (string TenantKey, WebsiteSectionType[] Types)[] layout =
        [
            ("ahmed",
            [
                WebsiteSectionType.Hero, WebsiteSectionType.About, WebsiteSectionType.Achievements,
                WebsiteSectionType.FeaturedGroups, WebsiteSectionType.FeaturedCourses,
                WebsiteSectionType.Testimonials, WebsiteSectionType.Contact
            ]),
            ("mariam",
            [
                WebsiteSectionType.Hero, WebsiteSectionType.About,
                WebsiteSectionType.FeaturedCourses, WebsiteSectionType.Contact
            ]),
            ("center",
            [
                WebsiteSectionType.Hero, WebsiteSectionType.About, WebsiteSectionType.Teachers,
                WebsiteSectionType.FeaturedGroups, WebsiteSectionType.Testimonials,
                WebsiteSectionType.Contact, WebsiteSectionType.Custom
            ]),
            ("nour", [WebsiteSectionType.Hero, WebsiteSectionType.Contact])
        ];

        foreach (var (tenantKey, types) in layout)
        {
            var tenant = _tenants[tenantKey];
            var websiteId = SeedId.For("demo:website", tenantKey);
            var order = 0;

            foreach (var type in types)
            {
                order++;

                sections.Add(new WebsiteSection
                {
                    Id = SeedId.For("demo:website-section", websiteId, (int)type),
                    TenantWebsiteId = websiteId,
                    TenantId = tenant.Id,
                    Type = type,
                    Title = SectionTitle(type),
                    ImageUrl = DemoMedia.WebsiteSectionImage(type.ToString()),
                    Subtitle = type == WebsiteSectionType.Custom ? "مواعيد التسجيل للعام الدراسي الجديد" : null,
                    Body = type == WebsiteSectionType.Custom
                        ? "التسجيل مفتوح حتى نهاية الشهر. الأماكن محدودة لكل مجموعة."
                        : null,
                    SettingsJson = type is WebsiteSectionType.FeaturedGroups or WebsiteSectionType.FeaturedCourses
                        ? "{\"itemCount\":3,\"layout\":\"grid\"}"
                        : null,
                    // One hidden section per full site, so the visibility filter has work to do.
                    IsVisible = !(tenantKey == "ahmed" && type == WebsiteSectionType.Achievements),
                    SortOrder = order,
                    CreatedAt = tenant.CreatedAt
                });
            }
        }

        await AddMissingAsync("WebsiteSections", sections, cancellationToken);

        static string SectionTitle(WebsiteSectionType type) => type switch
        {
            WebsiteSectionType.Hero => "الصفحة الرئيسية",
            WebsiteSectionType.About => "من نحن",
            WebsiteSectionType.Achievements => "إنجازاتنا",
            WebsiteSectionType.Testimonials => "آراء الطلاب",
            WebsiteSectionType.FeaturedGroups => "المجموعات المميزة",
            WebsiteSectionType.FeaturedCourses => "الكورسات المميزة",
            WebsiteSectionType.Teachers => "فريق التدريس",
            WebsiteSectionType.Contact => "تواصل معنا",
            _ => "قسم مخصص"
        };
    }

    /// <summary>
    /// Curated quotes the workspace wrote for its own site.
    ///
    /// These are not reviews: a review is written by a student and moderated, a testimonial is
    /// marketing copy the workspace controls. They are invented text attributed to invented
    /// students, and they are never presented as verified customer feedback — which is exactly
    /// why the product keeps the two in separate tables.
    /// </summary>
    private async Task SeedTestimonialsAsync(CancellationToken cancellationToken)
    {
        (string TenantKey, string Author, string Title, string Quote, int Rating, bool IsVisible)[] quotes =
        [
            ("ahmed", "سلمى إبراهيم", "طالبة بالصف الثالث الثانوي",
                "شرح الأستاذ أحمد خلى الفيزياء أسهل حاجة في المنهج، والمراجعات قبل الامتحان كانت فارقة.", 5, true),
            ("ahmed", "والد الطالب يوسف طارق", "ولي أمر",
                "متابعة أسبوعية بتقارير واضحة عن الحضور والدرجات. أول مرة أعرف مستوى ابني بدقة.", 5, true),
            ("ahmed", "زياد مجدي", "طالب بالصف الثالث الثانوي",
                "بنك الأسئلة بعد كل درس هو اللي رفع مستويي فعلاً.", 4, false),
            ("center", "أمنية رشاد", "طالبة بالصف الثالث الثانوي",
                "نظام السنتر منظم جداً: مواعيد ثابتة، امتحانات أسبوعية، ونتائج فورية.", 5, true),
            ("center", "حسن صبحي", "طالب بالصف الثاني الثانوي",
                "المجموعات صغيرة فالمدرس بيلاحق كل طالب لوحده.", 5, true),
            ("mariam", "فريدة نبيل", "طالبة بالصف الأول الثانوي",
                "المحاضرات أونلاين بجودة عالية وأقدر أرجع لها في أي وقت.", 4, true)
        ];

        var testimonials = new List<Testimonial>();
        var order = new Dictionary<string, int>();

        foreach (var (tenantKey, author, title, quote, rating, isVisible) in quotes)
        {
            order[tenantKey] = order.GetValueOrDefault(tenantKey) + 1;
            var tenant = _tenants[tenantKey];

            testimonials.Add(new Testimonial
            {
                Id = SeedId.For("demo:testimonial", tenantKey + ":" + order[tenantKey]),
                TenantWebsiteId = SeedId.For("demo:website", tenantKey),
                TenantId = tenant.Id,
                AuthorName = author,
                AuthorTitle = title,
                // A monogram, not a face. These quotes are attributed to invented
                // students, so there is nobody for a photograph to be of.
                AuthorImageUrl = DemoMedia.Monogram(author, "#64748B"),
                Quote = quote,
                Rating = rating,
                IsVisible = isVisible,
                SortOrder = order[tenantKey],
                CreatedAt = tenant.CreatedAt.AddDays(order[tenantKey])
            });
        }

        await AddMissingAsync("Testimonials", testimonials, cancellationToken);
    }
}
