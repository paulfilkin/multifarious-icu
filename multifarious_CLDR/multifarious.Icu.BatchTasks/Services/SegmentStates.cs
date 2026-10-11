using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Sdl.Core.Globalization;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.NativeApi;

namespace multifarious.Icu.BatchTasks.Services
{
    /// <summary>
    /// What Studio records on a segment pair besides its content: the confirmation level, the
    /// lock, and where the translation came from. Captured by branch path when the restore task
    /// collapses a message, so the next expansion can hand each form back its own state, and
    /// read off a pre-translated whole message so its forms start with its match.
    /// </summary>
    public sealed class SegmentState
    {
        public SegmentState(string path, ConfirmationLevel confirmationLevel, bool isLocked,
            string originType, string originSystem, byte matchPercent, TextContextMatchLevel textContextMatchLevel)
        {
            Path = path ?? string.Empty;
            ConfirmationLevel = confirmationLevel;
            IsLocked = isLocked;
            OriginType = originType ?? string.Empty;
            OriginSystem = originSystem ?? string.Empty;
            MatchPercent = matchPercent;
            TextContextMatchLevel = textContextMatchLevel;
        }

        /// <summary>The branch path of the form, such as <c>count:few</c>; empty for a protected argument-only message.</summary>
        public string Path { get; }

        public ConfirmationLevel ConfirmationLevel { get; }

        public bool IsLocked { get; }

        /// <summary>Studio's origin type string, such as <c>tm</c> or <c>document-match</c>; empty where the segment has no origin.</summary>
        public string OriginType { get; }

        public string OriginSystem { get; }

        public byte MatchPercent { get; }

        public TextContextMatchLevel TextContextMatchLevel { get; }

        public bool HasOrigin { get { return OriginType.Length > 0; } }

        /// <summary>
        /// The match the segment counts as: 100 for a Perfect Match (Studio's document match)
        /// or a Context Match whatever its recorded percentage, else the recorded percentage,
        /// and zero where there is no origin at all.
        /// </summary>
        public int EffectiveMatch
        {
            get
            {
                if (!HasOrigin) return 0;
                if (OriginType == "document-match" || TextContextMatchLevel == TextContextMatchLevel.SourceAndTarget) return 100;
                return MatchPercent;
            }
        }

        public static SegmentState Capture(string path, ISegmentPairProperties properties)
        {
            if (properties == null) throw new ArgumentNullException(nameof(properties));

            var origin = properties.TranslationOrigin;
            return new SegmentState(path, properties.ConfirmationLevel, properties.IsLocked,
                origin == null ? null : origin.OriginType,
                origin == null ? null : origin.OriginSystem,
                origin == null ? (byte)0 : origin.MatchPercent,
                origin == null ? TextContextMatchLevel.None : origin.TextContextMatchLevel);
        }

        /// <summary>The same state on another path: a whole message's state handed to each of its forms.</summary>
        public SegmentState At(string path)
        {
            return new SegmentState(path, ConfirmationLevel, IsLocked, OriginType, OriginSystem, MatchPercent, TextContextMatchLevel);
        }

        public void Apply(ISegmentPairProperties properties, IDocumentItemFactory factory)
        {
            if (properties == null) throw new ArgumentNullException(nameof(properties));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            properties.ConfirmationLevel = ConfirmationLevel;
            properties.IsLocked = IsLocked;
            if (!HasOrigin)
            {
                properties.TranslationOrigin = null;
                return;
            }

            var origin = factory.CreateTranslationOrigin();
            origin.OriginType = OriginType;
            origin.OriginSystem = OriginSystem.Length == 0 ? null : OriginSystem;
            origin.MatchPercent = MatchPercent;
            origin.TextContextMatchLevel = TextContextMatchLevel;
            properties.TranslationOrigin = origin;
        }
    }

    /// <summary>
    /// The record of segment states the restore task leaves on the unit context, under
    /// <see cref="Constants.SegmentStatesKey"/>: one entry per form, entries separated by ';',
    /// fields by '|', in the order path, confirmation level, locked, origin type, origin system,
    /// match percent, text context match level. Field values are escaped so a memory name
    /// with either separator survives.
    /// </summary>
    public static class SegmentStates
    {
        public static string Serialise(IEnumerable<SegmentState> states)
        {
            if (states == null) throw new ArgumentNullException(nameof(states));

            var builder = new StringBuilder();
            foreach (var state in states)
            {
                if (builder.Length > 0) builder.Append(';');
                builder.Append(Escape(state.Path)).Append('|')
                    .Append(state.ConfirmationLevel.ToString()).Append('|')
                    .Append(state.IsLocked ? "1" : "0").Append('|')
                    .Append(Escape(state.OriginType)).Append('|')
                    .Append(Escape(state.OriginSystem)).Append('|')
                    .Append(state.MatchPercent.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(state.TextContextMatchLevel.ToString());
            }
            return builder.ToString();
        }

        /// <summary>The states by path. An entry that does not read is skipped; a file edited by hand is not a reason to fail the task.</summary>
        public static Dictionary<string, SegmentState> Parse(string serialised)
        {
            var states = new Dictionary<string, SegmentState>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(serialised)) return states;

            foreach (var entry in serialised.Split(';'))
            {
                var fields = entry.Split('|');
                if (fields.Length != 7) continue;

                ConfirmationLevel level;
                TextContextMatchLevel context;
                byte percent;
                if (!Enum.TryParse(fields[1], out level)) continue;
                if (!byte.TryParse(fields[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out percent)) continue;
                if (!Enum.TryParse(fields[6], out context)) continue;

                var path = Unescape(fields[0]);
                states[path] = new SegmentState(path, level, fields[2] == "1", Unescape(fields[3]), Unescape(fields[4]), percent, context);
            }
            return states;
        }

        /// <summary>
        /// The confirmation level a message collapsed from several forms takes: the common
        /// level where every form agrees, else Draft, since a message with one unconfirmed
        /// form is not confirmed. No forms at all is Unspecified.
        /// </summary>
        public static ConfirmationLevel Combined(IEnumerable<ConfirmationLevel> levels)
        {
            if (levels == null) throw new ArgumentNullException(nameof(levels));

            ConfirmationLevel? common = null;
            foreach (var level in levels)
            {
                if (common == null) common = level;
                else if (common.Value != level) return ConfirmationLevel.Draft;
            }
            return common ?? ConfirmationLevel.Unspecified;
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("%", "%25").Replace("|", "%7C").Replace(";", "%3B");
        }

        private static string Unescape(string value)
        {
            return (value ?? string.Empty).Replace("%7C", "|").Replace("%3B", ";").Replace("%25", "%");
        }
    }
}
