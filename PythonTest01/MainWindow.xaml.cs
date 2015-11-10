using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using ActiproSoftware.Text;
using ActiproSoftware.Text.Implementation;
using ActiproSoftware.Text.Languages.Python;
using ActiproSoftware.Text.Languages.Python.Implementation;
using ActiproSoftware.Text.Parsing;
using ActiproSoftware.Text.Parsing.Implementation;
using ActiproSoftware.Text.Parsing.LLParser;
using ActiproSoftware.Windows.Controls.SyntaxEditor;
using ActiproSoftware.Windows.Controls.SyntaxEditor.IntelliPrompt.Implementation;
using DevExpress.Xpf.Bars;
using IronPython.Hosting;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using Microsoft.Scripting.Hosting;
using MessageBox = System.Windows.Forms.MessageBox;

namespace PythonTest01
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private int documentNumber;
        private bool hasPendingParseData;
        private TextSnapshotOffset currentStatementSnapshotOffset;
        private readonly PythonStream _stream;
        ObservableCollection<VarValue> _varList = new ObservableCollection<VarValue>();
        ScriptEngine _engine;

        public MainWindow()
        {
            InitializeComponent();
            _stream = new PythonStream(OutputTextBox);
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
                new DisplayItemClassificationTypeProvider().RegisterAll();
                AmbientParseRequestDispatcherProvider.Dispatcher = new ThreadedParseRequestDispatcher();
                //var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                //@"Evget\PythonTest01\Package Repository");
                //AmbientPackageRepositoryProvider.Repository = new FileBasedPackageRepository(appDataPath);
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
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            var dispatcher = AmbientParseRequestDispatcherProvider.Dispatcher;
            if (dispatcher != null)
            {
                AmbientParseRequestDispatcherProvider.Dispatcher = null;
                dispatcher.Dispose();
            }
        }

        private void OnOpenFileButtonClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new System.Windows.Forms.OpenFileDialog();
            if (!BrowserInteropHelper.IsBrowserHosted)
                dialog.CheckFileExists = true;
            dialog.Multiselect = false;
            dialog.Filter = "Python files (*.py)|*.py|All files (*.*)|*.*";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                using (Stream stream = dialog.OpenFile())
                {
                    document.LoadFile(stream, Encoding.UTF8);
                }
            }
        }

        private void OnSaveFileButtonClick(object sender, ItemClickEventArgs e)
        {
            SaveFileDialog dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.FilterIndex = 1;
            dialog.RestoreDirectory = true;
            dialog.Filter = @"Python files (*.py)|*.py|All files (*.*)|*.*";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                document.SaveFile(dialog.FileName, Encoding.UTF8, LineTerminator.CarriageReturnNewline);
            }
        }

        private void OnErrorListViewDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var listBox = (ListBox)sender;
            var error = listBox.SelectedItem as IParseError;
            if (error != null)
            {
                syntaxEditor.ActiveView.Selection.StartPosition = error.PositionRange.StartPosition;
                syntaxEditor.Focus();
            }
        }

        private void OnCodeEditorDocumentParseDataChanged(object sender, EventArgs e)
        {
            hasPendingParseData = true;
        }

        private void OnCodeEditorUserInterfaceUpdate(object sender, RoutedEventArgs e)
        {
            if (hasPendingParseData)
            {
                hasPendingParseData = false;
                ILLParseData parseData = syntaxEditor.Document.ParseData as ILLParseData;
                if (parseData != null)
                {
                   // if (syntaxEditor.Document.CurrentSnapshot.Length < 10000)
                    {
                        //if (parseData.Ast != null)
                        //    syntaxEditor.Document.SetText(parseData.Ast.ToTreeString(0));
                        //else
                        //    syntaxEditor.Document.SetText(null);
                    }
                    errorListView.ItemsSource = parseData.Errors;
                }
                else
                {
                    errorListView.ItemsSource = null;
                }
            }
        }

        private void OnCodeEditorViewSelectionChanged(object sender, EditorViewSelectionEventArgs e)
        {
            if (!e.View.IsActive)
                return;
            linePanel.Content = String.Format("Ln {0}", e.CaretPosition.DisplayLine);
            columnPanel.Content = String.Format("Col {0}", e.CaretDisplayCharacterColumn);
            characterPanel.Content = String.Format("Ch {0}", e.CaretPosition.DisplayCharacter);
        }

        private void OnClearBreakpointsButtonClick(object sender, RoutedEventArgs e)
        {
            syntaxEditor.Document.IndicatorManager.Breakpoints.Clear();
            syntaxEditor.Focus();
        }

        private void OnStartDebuggingButtonClick(object sender, RoutedEventArgs e)
        {
            foreach (var bp in document.IndicatorManager.Breakpoints.GetInstances())
            {
                var line = bp.VersionRange.Translate(document.CurrentSnapshot).StartPosition.Line;
            }
            if (currentStatementSnapshotOffset.IsDeleted)
                currentStatementSnapshotOffset = new TextSnapshotOffset(syntaxEditor.ActiveView.CurrentSnapshot, 0);
            var snapshotOffset = syntaxEditor.ActiveView.Selection.EndSnapshotOffset;
            var context = new PythonContextFactory().CreateContext(snapshotOffset);
            currentStatementSnapshotOffset = DebuggingHelper.SetCurrentStatement(syntaxEditor.Document, currentStatementSnapshotOffset);
            stopDebuggingButton.IsEnabled = !currentStatementSnapshotOffset.IsDeleted;
            syntaxEditor.Focus();
        }

        private void OnStopDebuggingButtonClick(object sender, RoutedEventArgs e)
        {
            currentStatementSnapshotOffset = DebuggingHelper.SetCurrentStatement(syntaxEditor.Document, TextSnapshotOffset.Deleted);
            stopDebuggingButton.IsEnabled = false;
            syntaxEditor.Focus();
        }

        private void OnToggleBreakpointButtonClick(object sender, RoutedEventArgs e)
        {
            DebuggingHelper.ToggleBreakpoint(syntaxEditor.ActiveView.Selection.EndSnapshotOffset, true);
            syntaxEditor.Focus();
        }

        private void OnToggleBreakpointEnabledButtonClick(object sender, RoutedEventArgs e)
        {
            var tagRanges = syntaxEditor.Document.IndicatorManager.Breakpoints.GetInstances(new TextSnapshotRange(syntaxEditor.ActiveView.Selection.EndSnapshotOffset));
            var count = 0;
            foreach (var tagRange in tagRanges)
            {
                if (syntaxEditor.Document.IndicatorManager.Breakpoints.ToggleEnabledState(tagRange.Tag))
                    count++;
            }
            if (count == 0)
                MessageBox.Show("No breakpoints were found at the caret.","Info",MessageBoxButtons.OK);
            syntaxEditor.Focus();
        }

        public ScriptEngine GetEngine()
        {
            if (_engine != null)
                return _engine;
            _engine = Python.CreateEngine();

            ScriptRuntimeSetup setup = new ScriptRuntimeSetup();
            setup.DebugMode = true;
            setup.LanguageSetups.Add(Python.CreateLanguageSetup(null));
            ScriptRuntime runtime = new ScriptRuntime(setup);
            _engine = runtime.GetEngineByTypeName(typeof(PythonContext).AssemblyQualifiedName);

            _engine.Runtime.IO.SetOutput(_stream, Encoding.UTF8);
            _engine.Runtime.IO.SetErrorOutput(_stream, Encoding.UTF8);
            string path = Environment.CurrentDirectory;
            string ironPythonLibPath = string.Format(@"{0}\IronPythonLib.zip", path);
            var paths = _engine.GetSearchPaths() as List<string> ?? new List<string>();
            paths.Add(path);
            paths.Add(ironPythonLibPath);
            path = Environment.GetEnvironmentVariable("IRONPYTHONPATH");
            if (!string.IsNullOrEmpty(path))
            {
                var pathStrings = path.Split(';');
                paths.AddRange(pathStrings.Where(p => p.Length > 0));
            }
            _engine.SetSearchPaths(paths.ToArray());
            return _engine;
        }

        private void GetPythonVarsInfo(ScriptScope scope)
        {
            _varList.Clear();
            var items = scope.GetItems();
            foreach (var item in items)
            {
                _varList.Add(new VarValue
                {
                    VarName = item.Key,
                    Value = item.Value
                });
            }
            valueListView.ItemsSource = _varList;
        }

        private void OnExecuteButtonClick(object sender, ItemClickEventArgs e)
        {
            string outPutString = string.Empty;
            outPutString = "*************************************" +
                "Excute Date: " + DateTime.Now.ToLocalTime().ToString(CultureInfo.InvariantCulture);
            ExeceutePython(document, outPutString);
            TabControl.SelectedIndex = 2;
        }

        private void ExeceutePython(EditorDocument document, string outPutString)
        {
            ScriptEngine engine = GetEngine();
            string script = document.Text;
            ScriptSource source = engine.CreateScriptSourceFromString(script);
            ScriptScope scope = _engine.CreateScope();
            try
            {
                source.Compile();
                OutputTextBox.AppendText(outPutString + Environment.NewLine);
                var result = source.Execute(scope);
                if (result != null)
                {
                    OutputTextBox.AppendText(engine.Operations.Format(result));
                }
                OutputTextBox.AppendText(Environment.NewLine);
                GetPythonVarsInfo(scope);
            }
            catch (Exception ex)
            {
                var eo = engine.GetService<ExceptionOperations>();
                var eoString = eo.FormatException(ex);
                OutputTextBox.AppendText(eoString);
                return;
            }
        }
    }

    public class VarValue
    {
        public string VarName { get; set; }
        public object Value { get; set; }
    }


    internal class PythonStream : MemoryStream
    {
        private readonly System.Windows.Controls.TextBox _output;

        public PythonStream(System.Windows.Controls.TextBox textbox)
        {
            _output = textbox;
        }

        public override void Write(byte[] buffer, int offset,
            int count)
        {
            var text = Encoding.UTF8.GetString(buffer, offset, count);
            _output.AppendText(text);
        }
    }
}
