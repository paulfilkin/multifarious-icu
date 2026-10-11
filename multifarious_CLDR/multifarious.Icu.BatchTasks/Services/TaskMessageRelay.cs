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
    /// A message reported through a processor in the converter Studio hands the task lands in
    /// the task's results, so a restore run with a placeholder mismatch says so in the dialog
    /// and not only in the report and on the unit (Paul, 6 September 2026, Project 56). The
    /// task's own processor runs in that converter too, ahead of this one, so the warnings are
    /// complete by the time the document ends; this processor reads them then and reports each
    /// one.
    /// </summary>
    public sealed class TaskMessageRelay : AbstractBilingualContentProcessor
    {
        private static readonly ResourceManager PluginResources =
            new ResourceManager("multifarious.Icu.BatchTasks.PluginResources", typeof(Constants).Assembly);

        private readonly string _origin;
        private readonly string _fileName;
        private readonly Func<IEnumerable<string>> _messages;
        private readonly ErrorLevel _level;

        /// <param name="messages">Read when the document ends, so a list the task's processor is still filling is fine.</param>
        public TaskMessageRelay(string origin, string fileName, Func<IEnumerable<string>> messages, ErrorLevel level)
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

        public override void Complete()
        {
            var count = 0;
            foreach (var message in _messages())
            {
                ReportMessage(this, _origin, _level, message, _fileName);
                count++;
            }
            Diagnostics.Write("relay: " + count + " message(s) for " + _fileName
                + (MessageReporter == null ? " (no reporter)" : string.Empty));
            base.Complete();
        }
    }
}
