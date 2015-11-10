using System.Windows;
using ActiproSoftware.Text;
using ActiproSoftware.Text.Languages.Python.Ast.Implementation;
using ActiproSoftware.Text.Parsing;
using ActiproSoftware.Text.Parsing.LLParser;
using ActiproSoftware.Text.Tagging.Implementation;

namespace PythonTest01
{
    public static class DebuggingHelper
    {
        private static IAstNode FindContainingStatement(ILLParseData parseData, TextSnapshotOffset snapshotOffset)
        {
            var offset = snapshotOffset.Offset;
            if (parseData.Snapshot != null)
                offset = snapshotOffset.TranslateTo(parseData.Snapshot, TextOffsetTrackingMode.Negative);
            var node = parseData.Ast.FindDescendantNode(offset);
            Statement statmentNode = null;
            while (node != null)
            {
                statmentNode = node as Statement;
                if (statmentNode != null)
                    return statmentNode;

                node = node.Parent;
            }
            return null;
        }

        public static TextSnapshotOffset SetCurrentStatement(IEditorDocument document,
            TextSnapshotOffset startSnapshotOffset)
        {
            if (!startSnapshotOffset.IsDeleted)
            {
                var options = new TagSearchOptions<BreakpointIndicatorTag>();
                options.Filter = (tr => tr.Tag.IsEnabled);
                var tagRange = document.IndicatorManager.Breakpoints.FindNext(startSnapshotOffset, options);
                if (tagRange != null)
                {
                    var snapshotRange = tagRange.VersionRange.Translate(startSnapshotOffset.Snapshot);
                    var currentStatementSnapshotOffset = new TextSnapshotOffset(snapshotRange.Snapshot,
                        snapshotRange.EndOffset);
                    document.IndicatorManager.CurrentStatement.SetInstance(snapshotRange);
                    return currentStatementSnapshotOffset;
                }
            }
            document.IndicatorManager.CurrentStatement.Clear();
            return TextSnapshotOffset.Deleted;
        }

        public static void ToggleBreakpoint(TextSnapshotOffset snapshotOffset, bool isEnabled)
        {
            var document = snapshotOffset.Snapshot.Document as IEditorDocument;
            if (document == null)
                return;

            var parseData = document.ParseData as ILLParseData;
            if (parseData == null)
                return;

            var node = FindContainingStatement(parseData, snapshotOffset);
            if ((node == null) || (!node.StartOffset.HasValue) || (!node.EndOffset.HasValue))
            {
                MessageBox.Show("Please move the caret inside of a valid Python statement.", "Toggle Breakpoint",
                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            var snapshotRange = new TextSnapshotRange(parseData.Snapshot ?? snapshotOffset.Snapshot,
                node.StartOffset.Value,
                node.EndOffset.Value);

            var tag = new BreakpointIndicatorTag();
            tag.IsEnabled = isEnabled;

            var tagRange = document.IndicatorManager.Breakpoints.Toggle(snapshotRange, tag);
            if (tagRange != null)
                tag.ContentProvider = new BreakpointIndicatorTagContentProvider(tagRange);
        }

    }


}

