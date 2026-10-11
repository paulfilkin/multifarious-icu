using System;
using System.Collections.Generic;
using Icu.Core;
using Icu.Core.Tree;

namespace multifarious.Icu.BatchTasks.Services
{
    /// <summary>
    /// Reads a target paragraph's message, as the restore task wrote it or as pre-translation
    /// left it, into the content of each form by branch path, so the expansion can hand every
    /// planned segment its existing translation. The message is hoisted first, which makes a
    /// pre-translated message in the developer's shape line up with the plan the same way a
    /// restored one does; the paths are the planner's, <c>count:few</c> and
    /// <c>gender:female/count:many</c>, with the empty path for a message with no selector.
    /// </summary>
    public static class TargetBranches
    {
        /// <summary>
        /// The forms by path, or null with the reason where the text does not parse or cannot
        /// be hoisted. Text and quoted-text nodes carry decoded values, so a target written
        /// with ICU escaping comes back as the plain text the translator typed.
        /// </summary>
        public static Dictionary<string, IReadOnlyList<MessageNode>> Read(string rawTarget, out string failure)
        {
            if (rawTarget == null) throw new ArgumentNullException(nameof(rawTarget));

            IcuMessage message;
            IcuParseException error;
            if (!IcuParser.TryParse(rawTarget, out message, out error))
            {
                failure = "the target does not parse as ICU: " + error.Description;
                return null;
            }

            IcuMessage hoisted;
            try
            {
                hoisted = Hoister.Hoist(message);
            }
            catch (IcuHoistException exception)
            {
                failure = "the target cannot be hoisted: " + exception.Message;
                return null;
            }

            var forms = new Dictionary<string, IReadOnlyList<MessageNode>>(StringComparer.Ordinal);
            Collect(hoisted.Nodes, string.Empty, forms);
            failure = null;
            return forms;
        }

        private static void Collect(IReadOnlyList<MessageNode> nodes, string path,
            Dictionary<string, IReadOnlyList<MessageNode>> forms)
        {
            var selectors = new List<SelectorNode>();
            foreach (var node in nodes)
            {
                var selector = node as SelectorNode;
                if (selector != null) selectors.Add(selector);
            }

            if (selectors.Count == 0)
            {
                forms[path] = nodes;
                return;
            }

            // Hoisted, a sequence holding a selector holds nothing else that matters: the text
            // around it has moved into the branches.
            foreach (var selector in selectors)
            {
                foreach (var branch in selector.Branches)
                {
                    var component = selector.ArgumentName + ":" + branch.Key.Text;
                    Collect(branch.Nodes, path.Length == 0 ? component : path + "/" + component, forms);
                }
            }
        }
    }
}
