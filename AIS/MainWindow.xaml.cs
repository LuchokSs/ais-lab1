using AIS.Math;
using AIS.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace AIS
{
    public class InvertedBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => value is true ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => value is Visibility.Collapsed;
    }

    public class TraceConditionItem : INotifyPropertyChanged
    {
        private Brush? _background;

        public string Text { get; init; } = "";

        public Brush? Background
        {
            get => _background;
            set { _background = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RuleRow : INotifyPropertyChanged
    {
        private string _outputText = "";
        private string _inputText = "";
        private bool _isTraceMode;

        public string OutputText
        {
            get => _outputText;
            set { _outputText = value; OnPropertyChanged(); }
        }

        public string InputText
        {
            get => _inputText;
            set { _inputText = value; OnPropertyChanged(); }
        }

        public bool IsTraceMode
        {
            get => _isTraceMode;
            set { _isTraceMode = value; OnPropertyChanged(); }
        }

        public ObservableCollection<TraceConditionItem> InputConditions { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<RuleRow> _rules = new();
        private Dictionary<ConditionSequence, int> _seqToRuleIndex = new();
        private Dictionary<ConditionItem, int> _itemToRuleIndex = new();
        private Dictionary<ConditionItem, int> _itemToPosition = new();
        private string _originalTokensText = "";
        private bool _isTraceRunning;
        private CancellationTokenSource? _traceCts;

        public MainWindow()
        {
            InitializeComponent();
            RulesList.ItemsSource = _rules;
            LoadRules();
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T match) return match;
                var result = FindVisualChild<T>(child);
                if (result is not null) return result;
            }
            return null;
        }

        private static List<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            var results = new List<T>();
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T match) results.Add(match);
                results.AddRange(FindVisualChildren<T>(child));
            }
            return results;
        }

        private ConditionsContext GetContext()
            => ConditionsContext.Get("./db.txt");

        private void LoadRules()
        {
            _rules.Clear();

            foreach (var seq in GetContext().ConditionSequences)
            {
                _rules.Add(new RuleRow
                {
                    OutputText = $"{seq.Output.Object} = {seq.Output.Value}",
                    InputText = string.Join(Environment.NewLine, seq.Input.Select(i => $"{i.Object} = {i.Value}"))
                });
            }
        }

        private List<ConditionSequence> CollectSequences()
        {
            var sequences = new List<ConditionSequence>();

            foreach (var rule in _rules)
            {
                var output = ParseConditionItem(rule.OutputText);
                if (output is null) continue;

                var inputs = new List<ConditionItem>();
                foreach (var line in rule.InputText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var item = ParseConditionItem(line);
                    if (item is not null)
                        inputs.Add(item);
                }

                if (inputs.Count == 0) continue;

                sequences.Add(new ConditionSequence { Output = output, Input = inputs });
            }

            return sequences;
        }

        private ConditionItem? ParseConditionItem(string text)
        {
            var eqIndex = text.IndexOf('=');
            if (eqIndex < 0) return null;

            var obj = text[..eqIndex].Trim();
            var val = text[(eqIndex + 1)..].Trim();

            if (obj.Length == 0 || val.Length == 0) return null;

            return new ConditionItem { Object = obj, Value = val };
        }

        private void SaveThroughContext()
        {
            var context = GetContext();
            context.ConditionSequences = CollectSequences();
        }

        private void AddRule_Click(object sender, RoutedEventArgs e)
        {
            _rules.Add(new RuleRow());
        }

        private void DeleteRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is RuleRow row)
            {
                _rules.Remove(row);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveThroughContext();
                var context = GetContext();
                context.SaveConditionSequences();
                MessageBox.Show("Файл сохранён.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось сохранить файл: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            LoadRules();
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveThroughContext();
                LogContext.States.Clear();

                var expert = new ExpertSystem();
                var tokensText = new TextRange(TokensRichTextBox.Document.ContentStart,
                    TokensRichTextBox.Document.ContentEnd).Text;
                foreach (var line in tokensText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    expert.AddToken(line);
                }

                var verdict = expert.GetResult();

                if (verdict.IsSuccess)
                {
                    ResultTextBox.Text = $"Результат: {verdict.Result}";
                }
                else
                {
                    ResultTextBox.Text = $"Недостаточно данных: {verdict.Result}";
                }
            }
            catch (Exception ex)
            {
                ResultTextBox.Text = $"Ошибка: {ex.Message}";
            }
        }

        private async void ShowTrace_Click(object sender, RoutedEventArgs e)
        {
            if (_isTraceRunning || LogContext.States.Count == 0)
                return;

            _isTraceRunning = true;
            _traceCts = new CancellationTokenSource();
            var context = GetContext();

            if (context.ConditionSequences.Count != _rules.Count)
            {
                _traceCts.Dispose();
                _traceCts = null;
                _isTraceRunning = false;
                MessageBox.Show("Правила были изменены после запуска. Запустите систему заново.",
                    "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _seqToRuleIndex = new Dictionary<ConditionSequence, int>();
            _itemToRuleIndex = new Dictionary<ConditionItem, int>();
            _itemToPosition = new Dictionary<ConditionItem, int>();

            for (int i = 0; i < context.ConditionSequences.Count; i++)
            {
                var seq = context.ConditionSequences[i];
                _seqToRuleIndex[seq] = i;

                var row = _rules[i];
                row.InputConditions.Clear();
                for (int j = 0; j < seq.Input.Count; j++)
                {
                    var item = seq.Input[j];
                    _itemToRuleIndex[item] = i;
                    _itemToPosition[item] = j;
                    row.InputConditions.Add(new TraceConditionItem { Text = $"{item.Object} = {item.Value}" });
                }
                row.IsTraceMode = true;
            }

            _originalTokensText = new TextRange(TokensRichTextBox.Document.ContentStart,
                TokensRichTextBox.Document.ContentEnd).Text;
            PopulateTokens(_originalTokensText);

            ShowTraceButton.IsEnabled = false;
            StopTraceButton.IsEnabled = true;
            SetEditControlsEnabled(false);

            var states = LogContext.States;
            for (int i = 0; i < states.Count && !_traceCts.IsCancellationRequested; i++)
            {
                var state = states[i];
                ClearTokenHighlight();

                switch (state.LogStateType)
                {
                    case LogStateType.Inspect:
                        if (state.ConditionItem is not null)
                            await ApplyInspectHighlight(state.ConditionItem, state.InspectedToken);
                        break;
                    case LogStateType.Partial:
                        if (state.ConditionItem is not null)
                            await ApplyPartialHighlight(state.ConditionItem);
                        break;
                    case LogStateType.SequenceInspect:
                        if (state.ConditionSequence is not null)
                            await ApplySequenceHighlight(state.ConditionSequence, Brushes.Yellow);
                        break;
                    case LogStateType.Commit:
                        if (state.ConditionSequence is not null)
                            await ApplySequenceHighlight(state.ConditionSequence, Brushes.LightGreen);
                        break;
                    case LogStateType.AddVariable:
                        if (state.ExpertSystemToken is not null)
                            AddVariableToTokens(state.ExpertSystemToken);
                        break;
                }

                try
                {
                    await Task.Delay(1000, _traceCts.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            var wasCancelled = _traceCts.IsCancellationRequested;
            _traceCts.Dispose();
            _traceCts = null;

            if (wasCancelled)
            {
                ResetTrace();
                MessageBox.Show("Трассировка остановлена.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBox.Show("Трассировка завершена.", "Информация",
                MessageBoxButton.OK, MessageBoxImage.Information);

            StopTraceButton.IsEnabled = false;
            ResetTraceButton.IsEnabled = true;
        }

        private async Task ApplyInspectHighlight(ConditionItem item, ExpertSystemToken? token)
        {
            ClearInspectHighlights();

            if (!_itemToRuleIndex.TryGetValue(item, out var idx) ||
                !_itemToPosition.TryGetValue(item, out var pos) ||
                idx < 0 || idx >= _rules.Count)
                return;

            _rules[idx].InputConditions[pos].Background = Brushes.Yellow;

            if (token is not null)
                HighlightToken(token);

            var container = RulesList.ItemContainerGenerator.ContainerFromIndex(idx) as ContentPresenter;
            container?.BringIntoView();
            await Task.CompletedTask;
        }

        private async Task ApplyPartialHighlight(ConditionItem item)
        {
            if (!_itemToRuleIndex.TryGetValue(item, out var idx) ||
                !_itemToPosition.TryGetValue(item, out var pos) ||
                idx < 0 || idx >= _rules.Count)
                return;

            _rules[idx].InputConditions[pos].Background = Brushes.LightGreen;

            var container = RulesList.ItemContainerGenerator.ContainerFromIndex(idx) as ContentPresenter;
            container?.BringIntoView();
            await Task.CompletedTask;
        }

        private async Task ApplySequenceHighlight(ConditionSequence seq, Brush brush)
        {
            if (!_seqToRuleIndex.TryGetValue(seq, out var idx) ||
                idx < 0 || idx >= _rules.Count)
                return;

            var container = RulesList.ItemContainerGenerator.ContainerFromIndex(idx) as ContentPresenter;
            if (container is null) return;

            var border = FindVisualChild<Border>(container);
            if (border is not null)
            {
                border.BorderBrush = brush;
                border.BorderThickness = new Thickness(2);
            }

            container.BringIntoView();
            await Task.CompletedTask;
        }

        private void AddVariableToTokens(ExpertSystemToken token)
        {
            var run = new Run($"{token.Object} = {token.Value}")
            {
                Foreground = Brushes.Blue,
                FontWeight = FontWeights.SemiBold,
                Tag = $"{token.Object}\u0001{token.Value}"
            };
            TokensRichTextBox.Document.Blocks.Add(new Paragraph(run));
            TokensRichTextBox.ScrollToEnd();
        }

        private static (string Object, string Value) NormalizeTokenLine(string line)
        {
            line = line.Trim();
            foreach (var sym in ",.;!?1234567890+:-/".Select(c => c.ToString()))
                line = line.Replace(sym, "");
            var parts = line.Split("=", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var obj = parts.Length > 0 ? parts[0] : "";
            var val = parts.Length > 1 ? parts[1] : "Да";
            return (obj, val);
        }

        private void PopulateTokens(string text)
        {
            TokensRichTextBox.Document.Blocks.Clear();
            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var (obj, val) = NormalizeTokenLine(line);
                TokensRichTextBox.Document.Blocks.Add(new Paragraph(new Run(line) { Tag = $"{obj}\u0001{val}" }));
            }
        }

        private static IEnumerable<Run> EnumerateRuns(FlowDocument doc)
        {
            foreach (var block in doc.Blocks)
            {
                if (block is not Paragraph para) continue;
                foreach (var inline in para.Inlines)
                {
                    if (inline is Run run)
                        yield return run;
                }
            }
        }

        private void ClearTokenHighlight()
        {
            foreach (var run in EnumerateRuns(TokensRichTextBox.Document))
            {
                if (run.Background == Brushes.Yellow)
                    run.Background = null;
            }
        }

        private void HighlightToken(ExpertSystemToken token)
        {
            var key = $"{token.Object}\u0001{token.Value}";
            foreach (var run in EnumerateRuns(TokensRichTextBox.Document))
            {
                if (run.Tag as string == key)
                {
                    run.Background = Brushes.Yellow;
                    break;
                }
            }
        }

        private void ClearInspectHighlights()
        {
            foreach (var cond in _rules.SelectMany(r => r.InputConditions))
                if (cond.Background == Brushes.Yellow)
                    cond.Background = null;
        }

        private void SetEditControlsEnabled(bool enabled)
        {
            foreach (var btn in FindVisualChildren<Button>(RulesList))
                if (btn.Content as string == "Удалить")
                    btn.IsEnabled = enabled;
            foreach (var tb in FindVisualChildren<TextBox>(RulesList))
                tb.IsEnabled = enabled;

            AddRuleButton.IsEnabled = enabled;
            SaveButton.IsEnabled = enabled;
            ReloadButton.IsEnabled = enabled;
            RunButton.IsEnabled = enabled;
            ShowTraceButton.IsEnabled = enabled;
            TokensRichTextBox.IsReadOnly = !enabled;
        }

        private void ResetTrace_Click(object sender, RoutedEventArgs e)
        {
            ResetTrace();
        }

        private void StopTrace_Click(object sender, RoutedEventArgs e)
        {
            _traceCts?.Cancel();
        }

        private void ResetTrace()
        {
            PopulateTokens(_originalTokensText);

            foreach (var rule in _rules)
            {
                foreach (var cond in rule.InputConditions)
                    cond.Background = null;

                rule.IsTraceMode = false;
                rule.InputConditions.Clear();
            }

            foreach (var border in FindVisualChildren<Border>(RulesList))
            {
                if (border.CornerRadius == new CornerRadius(3))
                {
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
                    border.BorderThickness = new Thickness(1);
                }
            }

            StopTraceButton.IsEnabled = false;
            SetEditControlsEnabled(true);
            ResetTraceButton.IsEnabled = false;
            _isTraceRunning = false;
        }
    }
}
