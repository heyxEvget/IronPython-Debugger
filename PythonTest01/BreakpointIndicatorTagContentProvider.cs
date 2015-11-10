using System;
using ActiproSoftware.Text.Tagging;
using ActiproSoftware.Text.Tagging.Implementation;
using ActiproSoftware.Windows.Controls.SyntaxEditor.IntelliPrompt;
using ActiproSoftware.Windows.Controls.SyntaxEditor.IntelliPrompt.Implementation;

namespace PythonTest01
{
    public class BreakpointIndicatorTagContentProvider : IContentProvider
    {
        private TagVersionRange<BreakpointIndicatorTag> tagRange;

        public BreakpointIndicatorTagContentProvider(TagVersionRange<BreakpointIndicatorTag> tagRange)
        {
            if (tagRange == null)
                throw new ArgumentNullException("tagRange");
            this.tagRange = tagRange;
        }

        public object GetContent()
        {
            var snapshotRange = tagRange.VersionRange.Translate(tagRange.VersionRange.Document.CurrentSnapshot);

            var htmlSnippet = String.Format("At line <b>{0}</b>, character <b>{1}</b>{2}",
                snapshotRange.StartPosition.DisplayLine, snapshotRange.StartPosition.DisplayCharacter,
                (tagRange.Tag.IsEnabled ? String.Empty : " <i>(disabled)</i>"));
            return new HtmlContentProvider(htmlSnippet).GetContent();
        }
    }
}

