using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ActiproSoftware.Text;
using ActiproSoftware.Text.Languages.Python;
using ActiproSoftware.Text.Languages.Python.Implementation;
using ActiproSoftware.Text.Parsing;
using ActiproSoftware.Text.Parsing.Implementation;
using ActiproSoftware.Text.Parsing.LLParser;
using ActiproSoftware.Windows.Controls.SyntaxEditor;
using ActiproSoftware.Windows.Controls.SyntaxEditor.IntelliPrompt.Implementation;
using IronPython.Hosting;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using Microsoft.Scripting.Hosting;

namespace PythonTest01
{

    public static class DebugCommands
    {
        public static readonly RoutedUICommand StepIn = new RoutedUICommand("Step In", "StepIn", typeof(DebugWindow));
        public static readonly RoutedUICommand StepOut = new RoutedUICommand("Step Out", "StepOut", typeof(DebugWindow));
        public static readonly RoutedUICommand StepOver = new RoutedUICommand("Step Over", "StepOver", typeof(DebugWindow));
    }

    /// <summary>
    /// DebugWindow.xaml 的交互逻辑
    /// </summary>
    public partial class DebugWindow : Window
    {

        private TextSnapshotOffset currentStatementSnapshotOffset;

        static Thread _debugThread;
        static DebugWindow _debugWindow;
        static ManualResetEvent _debugWindowReady = new ManualResetEvent(false);

        ScriptEngine _engine;
        Paragraph _source;
        AutoResetEvent _dbgContinue = new AutoResetEvent(false);
        Action<TraceBackFrame, string, object> _tracebackAction;
        TraceBackFrame _curFrame;
        FunctionCode _curCode;
        string _curResult;
        object _curPayload;

        public static void InitDebugWindow(ScriptEngine engine)
        {
            _debugThread = new Thread(() =>
            {
                _debugWindow = new DebugWindow(engine);
                _debugWindow.Show();
                System.Windows.Threading.Dispatcher.Run();
            });
            _debugThread.SetApartmentState(ApartmentState.STA);
            _debugThread.Start();

            _debugWindowReady.WaitOne();
            engine.SetTrace(_debugWindow.OnTracebackReceived);
        }

        public static void Shutdown()
        {
            _debugWindow._engine.SetTrace(null);
            _debugWindow.Dispatcher.InvokeShutdown();
        }

        public DebugWindow(ScriptEngine engine)
        {
            InitializeComponent();
            _tracebackAction = new Action<TraceBackFrame, string, object>(this.OnTraceback);
            _engine = engine;
        }

        private void DebugWindow_OnClosed(object sender, EventArgs e)
        {
            this.Dispatcher.InvokeShutdown();
        }

        private void DebugWindow_OnClosing(object sender, CancelEventArgs e)
        {
            breaktrace = false;
            _dbgContinue.Set();
        }

        private void DebugWindow_OnLoaded(object sender, RoutedEventArgs e)
        {

            new DisplayItemClassificationTypeProvider().RegisterAll();
            AmbientParseRequestDispatcherProvider.Dispatcher = new ThreadedParseRequestDispatcher();
            var language = new PythonSyntaxLanguage(PythonVersion.Version3);
            language.RegisterService(new PythonCompletionProvider());
            language.RegisterService(new PythonParameterInfoProvider());
            language.RegisterService(new PythonQuickInfoProvider());
            language.RegisterService(new PythonNavigableSymbolProvider());
            language.RegisterService(new IndicatorQuickInfoProvider());
            language.RegisterService(new DebuggingMouseInputEventSink());
            syntaxEditor.Document.Language = language;
            syntaxEditor.Document.TabSize = 4;
            syntaxEditor.Document.AutoConvertTabsToSpaces = true;
            syntaxEditor.Document.LoadFile("GetThings.py", Encoding.UTF8);

            _debugWindowReady.Set();
        }

        private void TracebackCall()
        {
            dbgStatus.Text = string.Format("Call {0}", _curCode.co_name);
            //HighlightLine((int)_curFrame.f_lineno, Brushes.LightGreen, Brushes.Black);
        }

        private void TracebackReturn()
        {
            dbgStatus.Text = string.Format("Return {0}", _curCode.co_name);
           // HighlightLine(_curCode.co_firstlineno, Brushes.LightPink, Brushes.Black);
        }

        private void TracebackLine()
        {
            dbgStatus.Text = string.Format("Line {0}", _curFrame.f_lineno);
            //HighlightLine((int)_curFrame.f_lineno, Brushes.Yellow, Brushes.Black);
        }

        private void OnTraceback(TraceBackFrame frame, string result, object payload)
        {
            var code = (FunctionCode)frame.f_code;
            if (_curCode == null || _curCode.co_filename != code.co_filename)
            {
                _source.Inlines.Clear();
                foreach (var line in System.IO.File.ReadAllLines(code.co_filename))
                {
                    _source.Inlines.Add(new Run(line + "\r\n"));
                }
            }
            _curFrame = frame;
            _curCode = code;
            _curResult = result;
            _curPayload = payload;

            switch (result)
            {
                case "call":
                    TracebackCall();
                    break;

                case "line":
                    TracebackLine();
                    break;

                case "return":
                    TracebackReturn();
                    break;

                default:
                    MessageBox.Show(string.Format("{0} not supported!", result));
                    break;
            }
        }

        bool breaktrace = true;
        private TracebackDelegate OnTracebackReceived(TraceBackFrame frame, string result, object payload)
        {
            if (breaktrace)
            {
                this.Dispatcher.BeginInvoke(_tracebackAction, frame, result, payload);
                _dbgContinue.WaitOne();
                return _traceback;
            }
            else
                return null;
        }

        TracebackDelegate _traceback;

        private void StepInExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            _traceback = this.OnTracebackReceived;
            ExecuteStep();
        }

        private void ExecuteStep()
        {
            //dbgStatus.Text = "Running";

            //foreach (var i in _source.Inlines)
            //{
            //    i.Background = Brushes.Black;
            //    i.Foreground = Brushes.White;
            //}

            _dbgContinue.Set();


            //foreach (var bp in document.IndicatorManager.Breakpoints.GetInstances())
            //{
            //    var line = bp.VersionRange.Translate(document.CurrentSnapshot).StartPosition.Line;
            //}
            //if (currentStatementSnapshotOffset.IsDeleted)
            //    currentStatementSnapshotOffset = new TextSnapshotOffset(syntaxEditor.ActiveView.CurrentSnapshot, 0);
            //var snapshotOffset = syntaxEditor.ActiveView.Selection.EndSnapshotOffset;
            //var context = new PythonContextFactory().CreateContext(snapshotOffset);
            //currentStatementSnapshotOffset = DebuggingHelper.SetCurrentStatement(syntaxEditor.Document, currentStatementSnapshotOffset);
            ////stopDebuggingButton.IsEnabled = !currentStatementSnapshotOffset.IsDeleted;
            //syntaxEditor.Focus();
        }

        private void StepOutExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            _traceback = null;
            ExecuteStep();
        }

        #region
        //private void OnCodeEditorDocumentParseDataChanged(object sender, EventArgs e)
        //{
        //    hasPendingParseData = true;
        //}

        //private void OnCodeEditorUserInterfaceUpdate(object sender, RoutedEventArgs e)
        //{
        //    if (hasPendingParseData)
        //    {
        //        hasPendingParseData = false;
        //        ILLParseData parseData = syntaxEditor.Document.ParseData as ILLParseData;
        //        if (parseData != null)
        //        {
        //            // if (syntaxEditor.Document.CurrentSnapshot.Length < 10000)
        //            {
        //                //if (parseData.Ast != null)
        //                //    syntaxEditor.Document.SetText(parseData.Ast.ToTreeString(0));
        //                //else
        //                //    syntaxEditor.Document.SetText(null);
        //            }
        //            //errorListView.ItemsSource = parseData.Errors;
        //        }
        //        else
        //        {
        //            //errorListView.ItemsSource = null;
        //        }
        //    }
        //}

        //private void OnCodeEditorViewSelectionChanged(object sender, EditorViewSelectionEventArgs e)
        //{
        //    if (!e.View.IsActive)
        //        return;
        //    //linePanel.Content = String.Format("Ln {0}", e.CaretPosition.DisplayLine);
        //    //columnPanel.Content = String.Format("Col {0}", e.CaretDisplayCharacterColumn);
        //    //characterPanel.Content = String.Format("Ch {0}", e.CaretPosition.DisplayCharacter);
        //}
        #endregion
    }
}
