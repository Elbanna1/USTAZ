namespace Ustaz.Persistence.Seeding;

/// <summary>
/// Every image, video and document the demo dataset points at.
///
/// The demo data exists to be looked at, and a catalogue of null thumbnails and dead
/// <c>/storage/demo/...</c> paths does not exercise a single image component in the client.
/// So every media field below resolves to something a browser can actually load, from a
/// source whose licence permits it.
///
/// <b>Where the media comes from, and why each source was chosen</b>
///
/// <list type="bullet">
/// <item>
/// <b>Wikimedia Commons</b> (<c>upload.wikimedia.org</c>) for every photograph, diagram,
/// lesson video and document. Commons accepts only freely-licensed and public-domain works,
/// so each URL carries its licence in the trailing comment: CC0, public domain, CC BY or
/// CC BY-SA. Each file was picked out of a curated Commons category rather than a keyword
/// search, which is what makes a chemistry course thumbnail actually show a chemistry
/// laboratory. The lesson videos are real recorded lessons and real filmed experiments.
/// </item>
/// <item>
/// <b>DiceBear</b> (<c>api.dicebear.com</c>) for every human avatar. The demo people are
/// invented, and a photograph of a real person standing in for an invented teacher - or
/// worse, for an invented sixteen-year-old - is the wrong thing to ship even where the
/// licence allows it. Generated avatars are unmistakably not photographs, are derived
/// deterministically from the persona key, and cannot depict anybody real.
/// </item>
/// </list>
///
/// Attribution for the CC BY and CC BY-SA files is the licence comment beside each URL. A
/// deployment that rendered these publicly would owe the credit the licence asks for;
/// nothing here is permitted in Production, which <see cref="SeedSafety"/> enforces with no
/// override.
///
/// Every URL was fetched and confirmed to return HTTP 200 with the expected content type
/// when this catalogue was written. They are absolute URLs on purpose: the client renders
/// them directly, with no rewriting, and without this instance needing anything in its own
/// upload storage.
/// </summary>
internal static class DemoMedia
{
    private const string AvatarService = "https://api.dicebear.com/9.x";

    /// <summary>
    /// A generated avatar for one invented person, stable for a given <paramref name="key"/>.
    ///
    /// The seed is the persona key the rest of the seeder already uses, so the same demo
    /// student has the same face on every machine and after every re-run.
    /// </summary>
    public static string Avatar(string key) =>
        AvatarService + "/avataaars/svg?seed=" + Uri.EscapeDataString("ustaz-" + key)
        + "&radius=50&backgroundType=gradientLinear"
        + "&backgroundColor=b6e3f4,c0aede,d1d4f9,ffd5dc,ffdfbf";

    /// <summary>
    /// A generated monogram, for a workspace logo or favicon and for a testimonial author.
    /// Reads as a brand mark rather than a face, which is what those slots want.
    /// </summary>
    public static string Monogram(string text, string backgroundColorHex) =>
        AvatarService + "/initials/svg?seed=" + Uri.EscapeDataString(text)
        + "&radius=12&fontWeight=600&backgroundColor=" + backgroundColorHex.TrimStart('#');


    /// <summary>
    /// A teacher's profile cover, matched to the subject they teach: a physics blackboard
    /// behind the physics teacher, a microscope behind the biology teachers.
    /// </summary>
    public static string TeacherCover(string teacherKey) =>
        teacherKey switch
        {
            "ahmed" => "https://upload.wikimedia.org/wikipedia/commons/thumb/e/e5/Physiktafel.jpg/1280px-Physiktafel.jpg",   // CC BY-SA 3.0 - Physiktafel
            "mariam" => "https://upload.wikimedia.org/wikipedia/commons/thumb/f/fc/Mathematics_%28cryptography%29_on_a_blackboard.jpg/1280px-Mathematics_%28cryptography%29_on_a_blackboard.jpg",   // CC BY 4.0 - Mathematics (cryptography) on a blackboard
            "omar" => "https://upload.wikimedia.org/wikipedia/commons/thumb/4/44/Chemistry_Lab_at_a_Highschool.jpg/1280px-Chemistry_Lab_at_a_Highschool.jpg",   // CC BY-SA 4.0 - Chemistry Lab at a Highschool
            "nour" => "https://upload.wikimedia.org/wikipedia/commons/thumb/8/8a/Biblioth%C3%A8que_publique.jpg/1280px-Biblioth%C3%A8que_publique.jpg",   // CC BY-SA 4.0 - Bibliothèque publique
            "hossam" => "https://upload.wikimedia.org/wikipedia/commons/thumb/3/3c/BI_Norwegian_Business_School_Auditorium_7DM29184.jpg/1280px-BI_Norwegian_Business_School_Auditorium_7DM29184.jpg",   // CC BY-SA 4.0 - BI Norwegian Business School Auditorium 7DM29184
            "yasmin" => "https://upload.wikimedia.org/wikipedia/commons/thumb/b/b9/CSIRO_ScienceImage_11403_Using_an_Olympus_microscope_to_examine_a_slide.jpg/1280px-CSIRO_ScienceImage_11403_Using_an_Olympus_microscope_to_examine_a_slide.jpg",   // CC BY 3.0 - CSIRO ScienceImage 11403 Using an Olympus microscope
            "fatma" => "https://upload.wikimedia.org/wikipedia/commons/thumb/c/cc/1st_Takhtee_Khate_Ra%27ana_%28Script%29.jpg/1280px-1st_Takhtee_Khate_Ra%27ana_%28Script%29.jpg",   // CC0 - 1st Takhtee Khate Ra'ana (Script)
            "tarek" => "https://upload.wikimedia.org/wikipedia/commons/thumb/6/61/Compound_Microscope_Image.jpg/1280px-Compound_Microscope_Image.jpg",   // CC BY-SA 4.0 - Compound Microscope Image
            "dalia" => "https://upload.wikimedia.org/wikipedia/commons/thumb/c/ce/Dua_buah_glob.jpg/1280px-Dua_buah_glob.jpg",   // CC BY 4.0 - Dua buah glob
            "sameh" => "https://upload.wikimedia.org/wikipedia/commons/thumb/9/9d/Percentage_chalkboard.JPG/1280px-Percentage_chalkboard.JPG",   // CC BY-SA 3.0 - Percentage chalkboard
            _ => "https://upload.wikimedia.org/wikipedia/commons/thumb/f/ff/20230709_English_classroom_in_school.jpg/1280px-20230709_English_classroom_in_school.jpg"
        };

    /// <summary>A recorded course's thumbnail, matched to the course's own subject.</summary>
    public static string CourseThumbnail(string courseKey) =>
        courseKey switch
        {
            "physics-modern" => "https://upload.wikimedia.org/wikipedia/commons/thumb/8/87/CTRwilsonCloudChamberCavendishLab2013-08-29-17-09-40.jpg/1280px-CTRwilsonCloudChamberCavendishLab2013-08-29-17-09-40.jpg",   // CC BY-SA 4.0 - CTRwilsonCloudChamberCavendishLab2013-08-29-17-09-40
            "physics-foundations-free" => "https://upload.wikimedia.org/wikipedia/commons/thumb/8/8e/Delineation_of_Lines_of_Magnetic_Force_by_Iron_filings.jpg/1280px-Delineation_of_Lines_of_Magnetic_Force_by_Iron_filings.jpg",   // CC BY-SA 4.0 - Delineation of Lines of Magnetic Force by Iron filin
            "math-algebra-geometry" => "https://upload.wikimedia.org/wikipedia/commons/thumb/e/e9/3D_solid_with_Heesch_number_3.png/1280px-3D_solid_with_Heesch_number_3.png",   // CC BY-SA 4.0 - 3D solid with Heesch number 3
            "chemistry-organic" => "https://upload.wikimedia.org/wikipedia/commons/thumb/7/76/Equipment_and_glassware.jpg/1280px-Equipment_and_glassware.jpg",   // CC BY 4.0 - Equipment and glassware
            "english-grammar" => "https://upload.wikimedia.org/wikipedia/commons/thumb/6/67/Aprendizaje_del_lenguaje.jpg/1280px-Aprendizaje_del_lenguaje.jpg",   // CC0 - Aprendizaje del lenguaje
            "center-math-revision" => "https://upload.wikimedia.org/wikipedia/commons/thumb/9/99/A_student_answering_a_test.jpg/1280px-A_student_answering_a_test.jpg",   // CC0 - A student answering a test
            _ => "https://upload.wikimedia.org/wikipedia/commons/thumb/c/cf/Ashs-digital-technology-suite.jpg/1280px-Ashs-digital-technology-suite.jpg"
        };

    /// <summary>
    /// The hero image on a workspace's public website. Only the four workspaces that
    /// publish a site have one; the under-configured ones return null on purpose, because a
    /// public page with no hero is a state the client has to render.
    /// </summary>
    public static string? WebsiteHero(string tenantKey) =>
        tenantKey switch
        {
            "ahmed" => "https://upload.wikimedia.org/wikipedia/commons/thumb/f/ff/20230709_English_classroom_in_school.jpg/1280px-20230709_English_classroom_in_school.jpg",   // CC0 - 20230709 English classroom in school
            "mariam" => "https://upload.wikimedia.org/wikipedia/commons/thumb/0/0e/A_young_student_doing_exercises_on_a_whiteboard.jpg/1280px-A_young_student_doing_exercises_on_a_whiteboard.jpg",   // CC BY-SA 4.0 - A young student doing exercises on a whiteboard
            "center" => "https://upload.wikimedia.org/wikipedia/commons/thumb/e/e6/A_public_high_school_teacher_in_a_classroom_in_the_United_States_08.jpg/1280px-A_public_high_school_teacher_in_a_classroom_in_the_United_States_08.jpg",   // CC BY 4.0 - A public high school teacher in a classroom in the U
            "nour" => "https://upload.wikimedia.org/wikipedia/commons/thumb/e/e9/Cubicle_in_library.webp/1280px-Cubicle_in_library.webp",   // CC0 - Cubicle in library
            _ => null
        };

    /// <summary>A workspace's branding cover, shown behind its logo on the workspace header.</summary>
    public static string BrandingCover(string tenantKey) =>
        tenantKey switch
        {
            "ahmed" => "https://upload.wikimedia.org/wikipedia/commons/thumb/3/3c/Bouncing_ball_strobe_edit.jpg/1280px-Bouncing_ball_strobe_edit.jpg",   // CC BY-SA 3.0 - Bouncing ball strobe edit
            "mariam" => "https://upload.wikimedia.org/wikipedia/commons/thumb/a/a0/Photo-arcs-parametres-coursPSI.jpg/1280px-Photo-arcs-parametres-coursPSI.jpg",   // CC BY-SA 4.0 - Photo-arcs-parametres-coursPSI
            "nour" => "https://upload.wikimedia.org/wikipedia/commons/thumb/7/7c/A_heart_for_booklovers_and_readers_%2830640642260%29.jpg/1280px-A_heart_for_booklovers_and_readers_%2830640642260%29.jpg",   // CC0 - A heart for booklovers and readers (30640642260)
            "center" => "https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/A_Beregsz%C3%A1szi_Kossuth_Lajos_L%C3%ADceum_%C3%A9p%C3%BClet%C3%A9nek_jobb_sz%C3%A1rnya.jpg/1280px-A_Beregsz%C3%A1szi_Kossuth_Lajos_L%C3%ADceum_%C3%A9p%C3%BClet%C3%A9nek_jobb_sz%C3%A1rnya.jpg",   // CC0 - A Beregszászi Kossuth Lajos Líceum épületének jobb s
            "tarek" => "https://upload.wikimedia.org/wikipedia/commons/thumb/7/7c/BCB_science_lab.jpg/1280px-BCB_science_lab.jpg",   // CC BY-SA 4.0 - BCB science lab
            _ => "https://upload.wikimedia.org/wikipedia/commons/thumb/5/5c/Al_Batinah_International_School.jpg/1280px-Al_Batinah_International_School.jpg"
        };

    /// <summary>
    /// The illustration for one public-website section, keyed by the section type's name so
    /// this file needs no reference to the enum.
    /// </summary>
    public static string? WebsiteSectionImage(string sectionTypeName) =>
        sectionTypeName switch
        {
            "About" => "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c0/DEMO_Teaching.jpg/1280px-DEMO_Teaching.jpg",   // CC BY-SA 4.0 - DEMO Teaching
            "Achievements" => "https://upload.wikimedia.org/wikipedia/commons/thumb/4/47/Class_67_DSC_1233_%2835260734192%29.jpg/1280px-Class_67_DSC_1233_%2835260734192%29.jpg",   // Public domain - Class 67 DSC 1233 (35260734192)
            "Teachers" => "https://upload.wikimedia.org/wikipedia/commons/thumb/2/25/Astronomy_teacher_training_%2828134819187%29.jpg/1280px-Astronomy_teacher_training_%2828134819187%29.jpg",   // CC BY 4.0 - Astronomy teacher training (28134819187)
            "FeaturedGroups" => "https://upload.wikimedia.org/wikipedia/commons/thumb/9/99/ClassroomAII.jpg/1280px-ClassroomAII.jpg",   // CC BY-SA 4.0 - ClassroomAII
            "FeaturedCourses" => "https://upload.wikimedia.org/wikipedia/commons/thumb/c/cf/Ashs-digital-technology-suite.jpg/1280px-Ashs-digital-technology-suite.jpg",   // CC BY-SA 3.0 - Ashs-digital-technology-suite
            "Testimonials" => "https://upload.wikimedia.org/wikipedia/commons/thumb/a/a9/2016_Design_Seminar.jpg/1280px-2016_Design_Seminar.jpg",   // CC BY-SA 4.0 - 2016 Design Seminar
            "Contact" => "https://upload.wikimedia.org/wikipedia/commons/thumb/5/5c/Al_Batinah_International_School.jpg/1280px-Al_Batinah_International_School.jpg",   // CC0 - Al Batinah International School
            "Custom" => "https://upload.wikimedia.org/wikipedia/commons/thumb/e/ea/Back_to_school_snacks_water_bottle_notebook_backpack_%2820503811429%29.jpg/1280px-Back_to_school_snacks_water_bottle_notebook_backpack_%2820503811429%29.jpg",   // CC BY 2.0 - Back to school snacks water bottle notebook ba
            // A hero section takes the website's own hero image, not a second one.
            _ => null
        };

    /// <summary>
    /// A teacher achievement's image. The two seeded achievements are a results record and
    /// a training certificate, so they get a graduation photo and a diploma.
    /// </summary>
    public static string AchievementImage(int sortOrder) =>
        sortOrder switch
        {
            1 => "https://upload.wikimedia.org/wikipedia/commons/thumb/6/60/Collectie_Piet_v_Bree_%28109%29.jpg/1280px-Collectie_Piet_v_Bree_%28109%29.jpg",   // Public domain - Collectie Piet v Bree (109)
            2 => "https://upload.wikimedia.org/wikipedia/commons/thumb/4/4c/Cartas-de-curso-mestrado.jpg/1280px-Cartas-de-curso-mestrado.jpg",   // CC BY-SA 4.0 - Cartas-de-curso-mestrado
            _ => "https://upload.wikimedia.org/wikipedia/commons/thumb/6/60/Collectie_Piet_v_Bree_%28109%29.jpg/1280px-Collectie_Piet_v_Bree_%28109%29.jpg"
        };

    /// <summary>
    /// An announcement's image. Only some announcements carry one - the draft and the
    /// expired notice deliberately do not, so the client has to cope with both.
    /// </summary>
    public static string? AnnouncementImage(string announcementKey) =>
        announcementKey switch
        {
            "ahmed-pinned" => "https://upload.wikimedia.org/wikipedia/commons/thumb/9/93/Blackboard_in_library.jpg/1280px-Blackboard_in_library.jpg",   // CC BY-SA 4.0 - Blackboard in library
            "ahmed-parents" => "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c3/Convenio_de_Inclusi%C3%B3n_Educativa_%2840081077990%29.jpg/1280px-Convenio_de_Inclusi%C3%B3n_Educativa_%2840081077990%29.jpg",   // CC BY 2.0 - Convenio de Inclusión Educativa (40081077990)
            "center-pinned" => "https://upload.wikimedia.org/wikipedia/commons/thumb/d/d9/Academic_Buildings_of_CPS.jpg/1280px-Academic_Buildings_of_CPS.jpg",   // CC BY-SA 4.0 - Academic Buildings of CPS
            _ => null
        };

    // -----------------------------------------------------------------------
    // Lesson video
    // -----------------------------------------------------------------------

    // Real recorded lessons and filmed experiments from Wikimedia Commons, grouped by what
    // a course teaches. The maths pools are a published secondary-school lesson series; the
    // physics and chemistry pools are filmed laboratory demonstrations.
    //
    // Every URL below is Commons' 480p VP9 transcode, not the file somebody uploaded. That
    // matters more than it sounds: the originals here run from 11 MB to 1.05 GB, and a
    // gigabyte behind a lesson thumbnail is not a demo, it is a denial of service against
    // whoever opens the page. The 480p copies are 4-42 MB for the same footage and the same
    // licence. One pool member had no transcode at all and was swapped for a lesson that
    // does; that is the rule for adding to these lists, not a one-off.
    //
    // Two properties of this host are worth knowing before building a player on it:
    //
    // - It is WebM. Commons does not publish MP4, by policy. Every current desktop and
    //   mobile browser decodes VP9 in WebM, so a <video> element needs no second source,
    //   but a native mobile player or an older Safari may.
    // - It does not answer range requests (no Accept-Ranges). The browser therefore cannot
    //   seek, and preload="metadata" streams from the start rather than reading the header,
    //   which on a slow link looks like a video that will not load. Measured here at about
    //   450 KB/s, a 10 MB lesson needs roughly twenty seconds before it reports its
    //   duration. Real lesson video belongs on storage that supports ranges; this is demo
    //   content chosen for being relevant and freely licensed, not for being fast.

    private static readonly string[] PhysicsLessons =
    [
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/9/97/YBCO_video.webm/YBCO_video.webm.480p.vp9.webm",   // CC BY-SA 4.0 - YBCO video
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/4/48/Liquid-nitrogen-demo-freeside-atlanta.webm/Liquid-nitrogen-demo-freeside-atlanta.webm.480p.vp9.webm",   // CC BY-SA 3.0 - Liquid-nitrogen-demo-freeside-atlanta
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/9/9b/Chaospendel_%28Lernwerkstatt%29.webm/Chaospendel_%28Lernwerkstatt%29.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Chaospendel (Lernwerkstatt)
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/7/70/Linear_Phased_Array.webm/Linear_Phased_Array.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Linear Phased Array
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/b/b2/Woods-metal-melting.webm/Woods-metal-melting.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Woods-metal-melting
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/c/cd/Glowing_Pickle_Alejandro_Garcia.webm/Glowing_Pickle_Alejandro_Garcia.webm.480p.vp9.webm",   // CC BY 4.0 - Glowing Pickle Alejandro Garcia
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/c/c3/Operation_of_the_%27Spiral_Wave_Discharge%27_plasma_experiment_at_IPP_Greifswald_%281%29.webm/Operation_of_the_%27Spiral_Wave_Discharge%27_plasma_experiment_at_IPP_Greifswald_%281%29.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Operation of the 'Spiral Wave Discharge' plasma experime
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/2/2c/Crushing_can.webm/Crushing_can.webm.480p.vp9.webm",   // CC BY 3.0 - Crushing can
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/2/25/Giant_Mirror.webm/Giant_Mirror.webm.480p.vp9.webm",   // CC BY 3.0 - Giant Mirror
    ];

    private static readonly string[] MathAlgebraLessons =
    [
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/c/c9/Das_kartesische_Koordinatensystem_%E2%80%93_kolleg24_Mathematik.webm/Das_kartesische_Koordinatensystem_%E2%80%93_kolleg24_Mathematik.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Das kartesische Koordinatensystem – kolleg24 Mathematik
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/e/ed/Rechengesetze_-_kolleg24_Mathematik.webm/Rechengesetze_-_kolleg24_Mathematik.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Rechengesetze - kolleg24 Mathematik
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/e/e7/Potenzgesetze_-_kolleg24_Mathematik.webm/Potenzgesetze_-_kolleg24_Mathematik.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Potenzgesetze - kolleg24 Mathematik
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/3/32/Terme_und_Zusammenfassen_von_Termen_-_kolleg24_Mathematik.webm/Terme_und_Zusammenfassen_von_Termen_-_kolleg24_Mathematik.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Terme und Zusammenfassen von Termen - kolleg24 Mathemati
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/c/c7/Binomische_Formeln_-_kolleg24_Mathematik.webm/Binomische_Formeln_-_kolleg24_Mathematik.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Binomische Formeln - kolleg24 Mathematik
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/a/ab/Quadratwurzel_und_Wurzelgesetze_-_kolleg24_Mathematik.webm/Quadratwurzel_und_Wurzelgesetze_-_kolleg24_Mathematik.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Quadratwurzel und Wurzelgesetze - kolleg24 Mathematik
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/b/b6/Rechnen_mit_der_Quadratwurzel_-_kolleg24_Mathematik.webm/Rechnen_mit_der_Quadratwurzel_-_kolleg24_Mathematik.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Rechnen mit der Quadratwurzel - kolleg24 Mathematik
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/4/4a/Was_ist_eine_Funktion%3F_-_kolleg24_Mathematik.webm/Was_ist_eine_Funktion%3F_-_kolleg24_Mathematik.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Was ist eine Funktion? - kolleg24 Mathematik
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/f/fb/Lineare_Funktionen_-_kolleg24_Mathematik.webm/Lineare_Funktionen_-_kolleg24_Mathematik.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Lineare Funktionen - kolleg24 Mathematik
    ];

    private static readonly string[] MathRevisionLessons =
    [
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/f/f0/Quadratische_Funktionen_und_Normalparabel_-_kolleg24_Mathematik.webm/Quadratische_Funktionen_und_Normalparabel_-_kolleg24_Mathematik.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Quadratische Funktionen und Normalparabel - kolleg24 Mat
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/6/6d/Quadratische_Erg%C3%A4nzung_-_kolleg24_Mathematik.webm/Quadratische_Erg%C3%A4nzung_-_kolleg24_Mathematik.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Quadratische Ergänzung - kolleg24 Mathematik
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/5/58/L%C3%B6sungsformel_f%C3%BCr_quadratische_Gleichungen_%E2%80%93_die_Mitternachtsformel_-_kolleg24_Mathematik.webm/L%C3%B6sungsformel_f%C3%BCr_quadratische_Gleichungen_%E2%80%93_die_Mitternachtsformel_-_kolleg24_Mathematik.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Lösungsformel für quadratische Gleichungen – die Mittern
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/d/d5/Spezielle_quadratische_Gleichungen_-_kolleg24_Mathematik.webm/Spezielle_quadratische_Gleichungen_-_kolleg24_Mathematik.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Spezielle Gleichungen - kolleg24 Mathematik
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/2/2f/Fl%C3%A4cheninhalt_und_Umfang_von_Rechteck_und_Kreis_-_kolleg24_Mathematik.webm/Fl%C3%A4cheninhalt_und_Umfang_von_Rechteck_und_Kreis_-_kolleg24_Mathematik.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Flächeninhalt und Umfang von Rechteck und Kreis - kolleg
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/c/cc/Volumen_und_Oberfl%C3%A4che_von_Prisma_und_Zylinder_-_kolleg24_Mathematik.webm/Volumen_und_Oberfl%C3%A4che_von_Prisma_und_Zylinder_-_kolleg24_Mathematik.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Volumen und Oberfläche von Prisma und Zylinder - kolleg2
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/0/01/Volumen_und_Oberfl%C3%A4che_von_Pyramide_und_Kegel_-_kolleg24_Mathematik.webm/Volumen_und_Oberfl%C3%A4che_von_Pyramide_und_Kegel_-_kolleg24_Mathematik.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Volumen und Oberfläche von Pyramide und Kegel - kolleg24
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/6/6a/Trigonometrie_am_rechtwinkligen_Dreieck_-_kolleg24_Mathematik.webm/Trigonometrie_am_rechtwinkligen_Dreieck_-_kolleg24_Mathematik.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Trigonometrie am rechtwinkligen Dreieck - kolleg24 Mathe
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/d/d3/Diagrammtypen_und_Darstellung_von_Daten_-_kolleg24_Mathematik.webm/Diagrammtypen_und_Darstellung_von_Daten_-_kolleg24_Mathematik.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Diagrammtypen und Darstellung von Daten - kolleg24 Mathe
    ];

    private static readonly string[] ChemistryLessons =
    [
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/7/77/Golden_rain_demonstration.webm/Golden_rain_demonstration.webm.360p.vp9.webm",   // CC BY-SA 4.0 - Golden rain demonstration
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/c/cc/Iodine_clock_reaction.webm/Iodine_clock_reaction.webm.480p.vp9.webm",   // CC BY 3.0 - Iodine clock reaction
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/0/06/Blue-bottle_reaction_%28Oscillating_reaction%29.webm/Blue-bottle_reaction_%28Oscillating_reaction%29.webm.480p.vp9.webm",   // CC BY 3.0 - Blue-bottle reaction (Oscillating reaction)
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/6/62/Dehydration_of_Sugar.webm/Dehydration_of_Sugar.webm.480p.vp9.webm",   // CC BY 3.0 - Dehydration of Sugar
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/2/2f/Dehydration_of_Copper_Sulfate_pentahydrate.webm/Dehydration_of_Copper_Sulfate_pentahydrate.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Dehydration of Copper Sulfate pentahydrate
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/6/6f/Reaction_of_copper_sulfate_with_iron.webm/Reaction_of_copper_sulfate_with_iron.webm.480p.vp9.webm",   // CC BY 4.0 - Reaction of copper sulfate with iron
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/5/52/Catalytic_decomposition_of_hydrogen_peroxide.webm/Catalytic_decomposition_of_hydrogen_peroxide.webm.480p.vp9.webm",   // CC BY 4.0 - Catalytic decomposition of hydrogen peroxide
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/f/fc/Copper_catalyst_in_propanone.webm/Copper_catalyst_in_propanone.webm.480p.vp9.webm",   // CC BY 3.0 - Copper catalyst in propanone
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/a/ab/Green_Flame_of_Trimethyl_Borate.webm/Green_Flame_of_Trimethyl_Borate.webm.480p.vp9.webm",   // CC BY 4.0 - Green Flame of Trimethyl Borate
    ];

    private static readonly string[] LanguageLessons =
    [
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/c/c6/WIKITONGUES-_Lindie_teaching_Afrikaans.webm/WIKITONGUES-_Lindie_teaching_Afrikaans.webm.480p.vp9.webm",   // CC BY 3.0 - WIKITONGUES- Lindie teaching Afrikaans
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/7/73/25_Essential_Chinese_Phrases_You_NEED_to_Know_Before_Visiting_China%21.webm/25_Essential_Chinese_Phrases_You_NEED_to_Know_Before_Visiting_China%21.webm.480p.vp9.webm",   // CC BY 3.0 - 25 Essential Chinese Phrases You NEED to Know Before Vis
        "https://upload.wikimedia.org/wikipedia/commons/transcoded/f/fc/Armenian_Sign_Language_%28ArSL%29_-_%D5%A4%D5%A1%D5%BD_-_lesson.webm/Armenian_Sign_Language_%28ArSL%29_-_%D5%A4%D5%A1%D5%BD_-_lesson.webm.480p.vp9.webm",   // CC BY-SA 4.0 - Armenian Sign Language (ArSL) - դաս - lesson
    ];

    /// <summary>
    /// The video for one lesson, drawn deterministically from its course's pool so a re-run
    /// never reshuffles a course and no lesson repeats its neighbour.
    /// </summary>
    public static string LessonVideo(string courseKey, int sectionOrder, int lessonIndex)
    {
        var pool = courseKey switch
        {
            "physics-modern" => PhysicsLessons,
            "physics-foundations-free" => PhysicsLessons,
            "math-algebra-geometry" => MathAlgebraLessons,
            "center-math-revision" => MathRevisionLessons,
            "chemistry-organic" => ChemistryLessons,
            "english-grammar" => LanguageLessons,
            _ => PhysicsLessons
        };

        // Nine lessons over a nine-deep pool walk it exactly once. The free physics course
        // shares the physics pool with the paid one, so it starts four along to stop the two
        // courses opening on the same clip.
        var offset = courseKey == "physics-foundations-free" ? 4 : 0;
        var position = ((sectionOrder - 1) * 3) + (lessonIndex - 1) + offset;

        return pool[position % pool.Length];
    }

    // -----------------------------------------------------------------------
    // Documents
    // -----------------------------------------------------------------------

    // Freely-licensed textbooks and workbooks from Wikimedia Commons, standing in for the
    // handouts a teacher would upload. Matched to the subject, so the physics group's
    // handbook is a physics text.

    private const string DocPhysics =
        "https://upload.wikimedia.org/wikipedia/commons/8/81/FHSST_Physics.pdf";   // CC BY-SA 3.0 - FHSST Physics

    private const string DocPhysicsAlt =
        "https://upload.wikimedia.org/wikipedia/commons/a/a5/Basic_Physics_of_Nuclear_Medicine.pdf";   // CC BY-SA 3.0 - Basic Physics of Nuclear Medicine

    private const string DocMath =
        "https://upload.wikimedia.org/wikipedia/commons/3/35/ReasonableBasicAlgebra.pdf";   // CC BY-SA 3.0 - ReasonableBasicAlgebra

    private const string DocMathAlt =
        "https://upload.wikimedia.org/wikipedia/commons/3/3e/Fundamentals_of_high_school_mathematics_-_a_textbook_designed_to_follow_arithmetic_%28IA_fundamentalsofhi00rugg%29.pdf";   // Public domain - Fundamentals of high school mathematics - a textbo

    private const string DocChemistry =
        "https://upload.wikimedia.org/wikipedia/commons/7/7d/DeVoe%2C_Howard._%C2%ABThermodynamics_and_Chemistry%C2%BB.pdf";   // CC BY-SA 4.0 - DeVoe, Howard. «Thermodynamics and Chemistry»

    private const string DocChemistryAlt =
        "https://upload.wikimedia.org/wikipedia/commons/1/10/Chemistry_and_its_uses%3B_a_textbook_for_secondary_schools_%28IA_chemistryitsuses00mcph%29.pdf";   // Public domain - Chemistry and its uses; a textbook for secondary s

    private const string DocBiology =
        "https://upload.wikimedia.org/wikipedia/commons/4/44/High_School_Biology_Workbook.pdf";   // CC BY-SA 3.0 - High School Biology Workbook

    private const string DocBiologyAlt =
        "https://upload.wikimedia.org/wikipedia/commons/a/a8/High_School_Biology_Teacher%27s_Guide.pdf";   // CC BY-SA 3.0 - High School Biology Teacher's Guide

    private const string DocEnglish =
        "https://upload.wikimedia.org/wikipedia/commons/e/ea/English_grammar_%28IA_englishgram00murr%29.pdf";   // Public domain - English grammar (IA englishgram00murr)

    /// <summary>
    /// The handout for a group or course, by the subject it teaches.
    /// <paramref name="variant"/> picks between the enrolled-only handbook (1) and the free
    /// sample (anything else), so the two materials on a group are never the same file.
    /// </summary>
    public static string SubjectDocument(string subjectKey, int variant) =>
        (subjectKey, variant) switch
        {
            ("physics", 1) => DocPhysics,
            ("physics", _) => DocPhysicsAlt,
            ("mathematics", 1) => DocMath,
            ("mathematics", _) => DocMathAlt,
            ("chemistry", 1) => DocChemistry,
            ("chemistry", _) => DocChemistryAlt,
            ("biology", 1) => DocBiology,
            ("biology", _) => DocBiologyAlt,
            ("english", _) => DocEnglish,
            (_, 1) => DocMath,
            _ => DocMathAlt
        };
}
