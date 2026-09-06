using System.Drawing;
using System.Resources;
using multifarious.Icu.BatchTasks;
using multifarious.Icu.BatchTasks.Services;
using multifarious.Icu.BatchTasks.Verification;
using multifarious.Icu.Expansion;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.NativeApi;
using Sdl.Verification.Api;

namespace multifarious.Icu.Tests.BatchTasks;

/// <summary>
/// The ICU verifier over real expanded paragraph units, with a reporter that records what F8
/// would list, and the attributes and interfaces Studio discovers a verifier by.
/// </summary>
public class IcuVerifierTests
{
    private const string UnreadCount =
        "Hello {name}, you have {count, plural, one {# unread message} other {# unread messages}}!";

    private sealed record Reported(ErrorLevel Level, string Message, string SegmentId);

    private sealed class RecordingReporter : IBilingualContentMessageReporter
    {
        public List<Reported> Messages { get; } = [];

        public void ReportMessage(object source, string origin, ErrorLevel level, string message, TextLocation fromLocation, TextLocation uptoLocation)
        {
            var segment = fromLocation?.Location?.ItemAtLocation as ISegment ?? fromLocation?.Location?.BottomLevel?.Parent as ISegment;
            Messages.Add(new Reported(level, message, segment?.Properties.Id.Id ?? ""));
        }

        public void ReportMessage(object source, string origin, ErrorLevel level, string message, string locationDescription)
        {
            Messages.Add(new Reported(level, message, locationDescription));
        }
    }

    private sealed class SharedObjects : ISharedObjects
    {
        private readonly Dictionary<string, object> _objects = new(StringComparer.Ordinal);

        public SharedObjects With(string id, object value)
        {
            _objects[id] = value;
            return this;
        }

        public IEnumerable<object> SharedObjects_ => _objects.Values;
        IEnumerable<object> ISharedObjects.SharedObjects => _objects.Values;
        public IEnumerable<string> SharedObjectIds => _objects.Keys;
        public IEnumerable<KeyValuePair<string, object>> SharedObjectsWithIds => _objects;
#pragma warning disable CS0067
        public event EventHandler<SharedObjectPublishedEventArgs>? SharedObjectPublished;
#pragma warning restore CS0067
        public T GetSharedObject<T>(string id) => _objects.TryGetValue(id, out var value) && value is T typed ? typed : default!;
        public void PublishSharedObject(string id, object toBeShared, IdConflictResolution conflictingIdResolution) => _objects[id] = toBeShared;
    }

    private static IParagraphUnit Expanded(string value, string language = "ru-RU")
    {
        var unit = ParagraphUnits.Json(value, "['inbox.unreadCount']");
        new IcuExpandProcessor("en-GB", language, ExpansionOptions.Default, "1.0.0")
        {
            ItemFactory = ParagraphUnits.ItemFactory,
        }.ProcessParagraphUnit(unit);
        return unit;
    }

    private static List<ISegment> TargetSegments(IParagraphUnit unit) =>
        ParagraphUnits.ItemsOf(unit.Target).OfType<ISegment>().ToList();

    private static void SetTarget(ISegment target, params object[] items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item is string text
                ? ParagraphUnits.ItemFactory.CreateText(ParagraphUnits.PropertiesFactory.CreateTextProperties(text))
                : (IAbstractMarkupData)item);
        }
    }

    private static ILockedContent Locked(string syntax)
    {
        var locked = ParagraphUnits.ItemFactory.CreateLockedContent(
            ParagraphUnits.PropertiesFactory.CreateLockedContentProperties(LockTypeFlags.Manual));
        locked.Content.Add(ParagraphUnits.ItemFactory.CreateText(ParagraphUnits.PropertiesFactory.CreateTextProperties(syntax)));
        return locked;
    }

    private static (IcuVerifier Verifier, RecordingReporter Reporter) Verifier(ISharedObjects? shared = null)
    {
        var reporter = new RecordingReporter();
        var verifier = new IcuVerifier { ItemFactory = ParagraphUnits.ItemFactory, MessageReporter = reporter };
        verifier.SetSharedObjects(shared ?? new SharedObjects());
        verifier.Initialize(null!);
        verifier.SetFileProperties(null!);
        return (verifier, reporter);
    }

    [Fact]
    public void The_verifier_carries_the_attribute_and_interfaces_studio_discovers_it_by()
    {
        var attribute = typeof(IcuVerifier).GetCustomAttributes(typeof(GlobalVerifierAttribute), false).Cast<GlobalVerifierAttribute>().Single();
        Assert.Equal(Constants.VerifierId, attribute.Id);

        var resources = new ResourceManager("multifarious.Icu.BatchTasks.PluginResources", typeof(Constants).Assembly);
        Assert.False(string.IsNullOrEmpty(resources.GetString(attribute.Name)));
        Assert.False(string.IsNullOrEmpty(resources.GetString(attribute.Description)));

        Assert.True(typeof(IGlobalVerifier).IsAssignableFrom(typeof(IcuVerifier)));
        Assert.True(typeof(IBilingualVerifier).IsAssignableFrom(typeof(IcuVerifier)));
        Assert.True(typeof(ISharedObjectsAware).IsAssignableFrom(typeof(IcuVerifier)));
        Assert.NotNull(typeof(IcuVerifier).GetConstructor(Type.EmptyTypes));

        var verifier = new IcuVerifier();
        Assert.Equal("ICU Verifier", verifier.Name);
        Assert.IsType<Icon>(verifier.Icon);
        Assert.Equal(Constants.VerifierSettingsId, verifier.SettingsId);
        Assert.Equal([Constants.VerifierSettingsPageId], verifier.GetSettingsPageExtensionIds());

        // The page Studio lists under Verification, found through the attribute and the id above.
        var page = typeof(IcuVerifierSettingsPage).GetCustomAttributes(typeof(GlobalVerifierSettingsPageAttribute), false)
            .Cast<GlobalVerifierSettingsPageAttribute>().Single();
        Assert.Equal(Constants.VerifierSettingsPageId, page.Id);
        Assert.False(string.IsNullOrEmpty(resources.GetString(page.Name)));
        Assert.False(string.IsNullOrEmpty(resources.GetString(page.Description)));
        Assert.NotNull(typeof(IcuVerifierSettingsPage).GetConstructor(Type.EmptyTypes));
    }

    [Fact]
    public void The_severities_on_the_page_decide_what_is_reported_and_ignore_drops_a_check()
    {
        var bundle = Sdl.Core.Settings.SettingsUtil.CreateSettingsBundle(null);
        var settings = bundle.GetSettingsGroup<IcuVerifierSettings>();
        settings.EmptyForm = CheckSeverity.Note;
        settings.Placeholders = CheckSeverity.Ignore;
        var (verifier, reporter) = Verifier(new SharedObjects().With("SettingsBundle", bundle));

        var unit = Expanded(UnreadCount);
        var targets = TargetSegments(unit);
        SetTarget(targets[0], "Привет, у вас ", Locked("#"), " сообщение!");
        verifier.ProcessParagraphUnit(unit);

        // The placeholder mismatch on the first form is dropped; the three empty forms are notes.
        Assert.Equal(3, reporter.Messages.Count);
        Assert.All(reporter.Messages, m => Assert.Equal(ErrorLevel.Note, m.Level));
        Assert.All(reporter.Messages, m => Assert.Contains("no translation", m.Message));
    }

    [Fact]
    public void Untranslated_forms_are_warned_about_once_each()
    {
        var (verifier, reporter) = Verifier();

        verifier.ProcessParagraphUnit(Expanded(UnreadCount));

        Assert.Equal(4, reporter.Messages.Count);
        Assert.All(reporter.Messages, m => Assert.Equal(ErrorLevel.Warning, m.Level));
        Assert.Contains("count:few", reporter.Messages[1].Message);
        Assert.Equal(["1", "2", "3", "4"], reporter.Messages.Select(m => m.SegmentId));
    }

    [Fact]
    public void A_placeholder_mismatch_is_an_error_and_a_typed_hash_a_warning()
    {
        var unit = Expanded(UnreadCount);
        var targets = TargetSegments(unit);
        SetTarget(targets[0], "Привет, у вас ", Locked("#"), " сообщение!");
        SetTarget(targets[1], "Привет, ", Locked("{name}"), ", у вас # сообщения!");
        SetTarget(targets[2], "Привет, ", Locked("{name}"), ", у вас ", Locked("#"), " сообщений!");
        SetTarget(targets[3], "Привет, ", Locked("{name}"), ", у вас ", Locked("#"), " сообщения!");
        var (verifier, reporter) = Verifier();

        verifier.ProcessParagraphUnit(unit);

        Assert.Equal(3, reporter.Messages.Count);
        Assert.Equal(ErrorLevel.Error, reporter.Messages[0].Level);
        Assert.Contains("missing {name}", reporter.Messages[0].Message);
        Assert.Equal("1", reporter.Messages[0].SegmentId);

        // The second form lost its '#' placeholder and typed one as text: both are said.
        Assert.Equal(ErrorLevel.Error, reporter.Messages[1].Level);
        Assert.Contains("missing #", reporter.Messages[1].Message);
        Assert.Equal(ErrorLevel.Warning, reporter.Messages[2].Level);
        Assert.Contains("typed as text", reporter.Messages[2].Message);
        Assert.Equal("2", reporter.Messages[2].SegmentId);
    }

    [Fact]
    public void A_message_that_will_not_parse_is_an_error_said_once()
    {
        var unit = Expanded(UnreadCount);
        var targets = TargetSegments(unit);
        foreach (var target in targets) SetTarget(target, "x ", Locked("{name}"), " ", Locked("#"));
        SetTarget(targets[0], "x ", Locked("{name}"), " ", Locked("#"), Locked("{count, plural,"));
        var (verifier, reporter) = Verifier();

        verifier.ProcessParagraphUnit(unit);

        var invalid = reporter.Messages.Where(m => m.Message.Contains("not valid ICU")).ToList();
        Assert.Single(invalid);
        Assert.Equal(ErrorLevel.Error, invalid[0].Level);
    }

    [Fact]
    public void Verifying_one_segment_reports_only_that_segment()
    {
        var unit = Expanded(UnreadCount);
        var second = TargetSegments(unit)[1].Properties.Id;
        var (verifier, reporter) = Verifier(new SharedObjects().With("CurrentlyVerifyingSegmentId", second));

        verifier.ProcessParagraphUnit(unit);

        var message = Assert.Single(reporter.Messages);
        Assert.Equal("2", message.SegmentId);
    }

    [Fact]
    public void A_plain_unit_and_a_disabled_verifier_report_nothing()
    {
        var (verifier, reporter) = Verifier();
        verifier.ProcessParagraphUnit(ParagraphUnits.Json("Message Centre", "['app.title']"));
        Assert.Empty(reporter.Messages);

        var bundle = Sdl.Core.Settings.SettingsUtil.CreateSettingsBundle(null);
        bundle.GetSettingsGroup(Constants.VerifierSettingsId).GetSetting("Enabled", true).Value = false;
        var (disabled, quiet) = Verifier(new SharedObjects().With("SettingsBundle", bundle));
        disabled.ProcessParagraphUnit(Expanded(UnreadCount));
        Assert.Empty(quiet.Messages);
    }
}
