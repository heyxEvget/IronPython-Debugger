using System.Windows.Input;
using ActiproSoftware.Text;
using ActiproSoftware.Windows.Controls.SyntaxEditor;
using ActiproSoftware.Windows.Controls.SyntaxEditor.Margins;

namespace PythonTest01
{


    public class DebuggingMouseInputEventSink : IEditorViewMouseInputEventSink
    {
        void IEditorViewMouseInputEventSink.NotifyMouseDown(IEditorView view, MouseButtonEventArgs e)
        {
            this.OnViewMouseDown(view, e);
        }

        void IEditorViewMouseInputEventSink.NotifyMouseEnter(IEditorView view, MouseEventArgs e)
        {
        }


        void IEditorViewMouseInputEventSink.NotifyMouseHover(IEditorView view, MouseEventArgs e)
        {
        }


        void IEditorViewMouseInputEventSink.NotifyMouseLeave(IEditorView view, MouseEventArgs e)
        {
        }

        void IEditorViewMouseInputEventSink.NotifyMouseMove(IEditorView view, MouseEventArgs e)
        {
        }

        void IEditorViewMouseInputEventSink.NotifyMouseUp(IEditorView view, MouseButtonEventArgs e)
        {
        }

        void IEditorViewMouseInputEventSink.NotifyMouseWheel(IEditorView view, MouseWheelEventArgs e)
        {
        }

        protected virtual void OnViewMouseDown(IEditorView view, MouseButtonEventArgs e)
        {
            if ((e != null) && (!e.Handled))
            {
                var hitTestResult = view.SyntaxEditor.HitTest(e.GetPosition(view.VisualElement));
                if ((hitTestResult.Type == HitTestResultType.ViewMargin)
                    && (hitTestResult.ViewMargin.Key == EditorViewMarginKeys.Indicator)
                    && (hitTestResult.ViewLine != null))
                {

                    if (view.SyntaxEditor.Document.IndicatorManager.Breakpoints.RemoveAll(tr =>
                            hitTestResult.ViewLine.TextRange.IntersectsWith(tr.VersionRange.Translate(view.CurrentSnapshot).StartOffset)) == 0)
                    {

                        int currentOffset = hitTestResult.Offset + hitTestResult.ViewLine.TabStopLevel * 4;
                        DebuggingHelper.ToggleBreakpoint(new TextSnapshotOffset(hitTestResult.Snapshot, currentOffset), true);
                    }
                }
            }
        }
    }

}

