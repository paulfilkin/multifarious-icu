using System;
using System.Collections.Generic;
using System.Resources;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.NativeApi;

namespace multifarious.Icu.BatchTasks.Services
{
    /// <summary>
    /// Puts a task's warnings in front of the user in Studio's Task Results window.
    ///
    /// The tasks do their work through a converter of their own (see
    /// <see cref="BilingualFileUpdater"/>), and nothing reported there reaches Studio. The
    /// converter Studio hands the task in ConfigureConverter does run its processors, and a
    /// message reported through one of them lands in the task's results, so a finalise run with
    /// a placeholder mismatch says so in the dialog and not only in the report and on the unit
    /// (Paul, 6 September 2026, Project 56). This processor is added to Studio's converter after
    /// the file has been rewritten, with the warnings the rewrite collected, and reports each
    /// one when the converter starts on the file.
    /// </summary>
    public sealed class TaskMessageRelay : AbstractBilingualContentProcessor
    {
        private static readonly ResourceManager PluginResources =
            new ResourceManager("multifarious.Icu.BatchTasks.PluginResources", typeof(Constants).Assembly);

        private readonly string _origin;
        private readonly string _fileName;
        private readonly IReadOnlyList<string> _messages;
        private readonly ErrorLevel _level;

        public TaskMessageRelay(string origin, string fileName, IReadOnlyList<string> messages, ErrorLevel level)
        {
            if (messages == null) throw new ArgumentNullException(nameof(messages));
            _origin = origin ?? string.Empty;
            _fileName = fileName ?? string.Empty;
            _messages = messages;
            _level = level;
        }

        /// <summary>The task's name as Studio shows it, for the Origin column of the results.</summary>
        public static string OriginFor(string resourceKey)
        {
            try
            {
                return PluginResources.GetString(resourceKey) ?? resourceKey;
            }
            catch (MissingManifestResourceException)
            {
                return resourceKey;
            }
        }

        public override void Initialize(IDocumentProperties documentInfo)
        {
            base.Initialize(documentInfo);
            foreach (var message in _messages)
            {
                ReportMessage(this, _origin, _level, message, _fileName);
            }
            Diagnostics.Write("relay: " + _messages.Count + " message(s) for " + _fileName
                + (MessageReporter == null ? " (no reporter)" : string.Empty));
        }
    }
}
