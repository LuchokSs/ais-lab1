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
        private string _traceStatus = "";
        private bool _isTraceMode;
        private TraceConditionItem? _outputCondition;

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

        public string TraceStatus
        {
            get => _traceStatus;
            set { _traceStatus = value; OnPropertyChanged(); }
        }

        public bool IsTraceMode
        {
            get => _isTraceMode;
            set { _isTraceMode = value; OnPropertyChanged(); }
        }

        public TraceConditionItem? OutputCondition
        {
            get => _outputCondition;
            set { _outputCondition = value; OnPropertyChanged(); }
        }

        public ObservableCollection<TraceConditionItem> InputConditions { get; } = new();

        public IEnumerable<TraceConditionItem> AllConditions
        {
            get
            {
                foreach (var condition in InputConditions)
                    yield return condition;

                if (OutputCondition is not null)
                    yield return OutputCondition;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class MainWindow : Window
    {
        private static readonly TimeSpan ForwardStepDelay = TimeSpan.FromMilliseconds(1000);
        private static readonly TimeSpan ReverseStepDelay = TimeSpan.FromMilliseconds(400);

        private static readonly Brush BrushConditionInspect = Brushes.Yellow;
        private static readonly Brush BrushConditionRequired = Brushes.LightSkyBlue;
        private static readonly Brush BrushRuleInspect = Brushes.Yellow;
        private static readonly Brush BrushRuleCommit = Brushes.LightGreen;
        private static readonly Brush BrushGoalActive = Brushes.Orange;
        private static readonly Brush BrushFactConfirmed = Brushes.LightGreen;
        private static readonly Brush BrushFactMissing = Brushes.MistyRose;

        private readonly ObservableCollection<RuleRow> _rules = new();
        private Dictionary<ConditionSequence, int> _seqToRuleIndex = new();
        private Dictionary<ConditionItem, int> _itemToRuleIndex = new();
        private Dictionary<ConditionItem, int> _itemToPosition = new();
        private Dictionary<ConditionItem, int> _outputToRuleIndex = new();
        private string _originalTokensText = "";
        private bool _bankIsEmpty = true;
        private bool _isTraceRunning;

        public MainWindow()
        {
            InitializeComponent();
            RulesList.ItemsSource = _rules;
            LoadRules();
            ClearBank();
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
                ClearBank();

                var expert = new ExpertSystem();
                foreach (var line in ReadFacts())
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

        private void CheckButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveThroughContext();
                LogContext.ReverseStates.Clear();
                ClearBank();

                var expert = new ExpertSystem();
                foreach (var line in ReadFacts())
                {
                    expert.AddToken(line);
                }

                var goalText = GoalTextBox.Text.Trim();
                if (goalText.Length == 0)
                {
                    ResultTextBox.Text = "Укажите предполагаемый результат в поле «Предполагаемый результат».";
                    return;
                }

                var verdict = expert.GetRevesedResult(expert.ParseToken(goalText));

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

        private IEnumerable<string> ReadFacts()
        {
            var text = new TextRange(TokensRichTextBox.Document.ContentStart,
                TokensRichTextBox.Document.ContentEnd).Text;

            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                yield return line;
        }

        private async void ShowTrace_Click(object sender, RoutedEventArgs e)
        {
            if (!BeginTrace(LogContext.States, reverse: false))
                return;

            var states = LogContext.States;

            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                SetStepCaption(i + 1, states.Count, state);

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
                            await ApplySequenceHighlight(state.ConditionSequence, BrushRuleInspect);
                        break;
                    case LogStateType.Commit:
                        if (state.ConditionSequence is not null)
                            await ApplySequenceHighlight(state.ConditionSequence, BrushRuleCommit);
                        break;
                    case LogStateType.AddVariable:
                        if (state.ExpertSystemToken is not null)
                            AddVariableToBank(state.ExpertSystemToken);
                        break;
                }

                await Task.Delay(ForwardStepDelay);
            }

            EndTrace("Трассировка прямого прохода завершена.");
        }

        private async void ShowReverseTrace_Click(object sender, RoutedEventArgs e)
        {
            if (!BeginTrace(LogContext.ReverseStates, reverse: true))
                return;

            var states = LogContext.ReverseStates;

            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                SetStepCaption(i + 1, states.Count, state);

                ClearTokenHighlight();
                ClearConditionHighlights();

                switch (state.LogStateType)
                {
                    case LogStateType.GoalSet:
                        ApplyGoalHighlight();
                        break;
                    case LogStateType.ReverseSequenceInspect:
                        if (state.ConditionSequence is not null)
                            await ApplyReverseSequenceHighlight(state.ConditionSequence);
                        break;
                    case LogStateType.ReverseInspect:
                        if (state.ConditionItem is not null && state.InspectedToken is not null)
                            await ApplyReverseInspectHighlight(state.ConditionItem, state.InspectedToken);
                        break;
                    case LogStateType.ReverseCommit:
                        if (state.ConditionSequence is not null)
                            await ApplyReverseCommitHighlight(state.ConditionSequence);
                        break;
                    case LogStateType.BankAdd:
                        if (state.ConditionItem is not null && state.ExpertSystemToken is not null)
                            await ApplyBankAddHighlight(state.ConditionItem, state.ExpertSystemToken);
                        break;
                    case LogStateType.TokenCheck:
                        if (state.ExpertSystemToken is not null)
                            await ApplyTokenCheckHighlight(state.ExpertSystemToken, state.Checked ?? false);
                        break;
                    case LogStateType.PassEnd:
                        await ApplyPassEndHighlight();
                        break;
                }

                await Task.Delay(ReverseStepDelay);
            }

            EndTrace("Трассировка обратного прохода завершена.");
        }

        private bool BeginTrace(IReadOnlyList<LogState> states, bool reverse)
        {
            if (_isTraceRunning)
                return false;

            if (states.Count == 0)
            {
                MessageBox.Show(reverse
                        ? "Нет данных обратного прохода. Сначала нажмите «Проверить результат»."
                        : "Нет данных прямого прохода. Сначала нажмите «Получить результат».",
                    "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            _isTraceRunning = true;

            if (!PrepareTraceView())
            {
                _isTraceRunning = false;
                return false;
            }

            _originalTokensText = new TextRange(TokensRichTextBox.Document.ContentStart,
                TokensRichTextBox.Document.ContentEnd).Text;
            PopulateTokens(_originalTokensText);
            ClearBank();

            GoalTextBox.Background = null;
            GoalTextBox.IsEnabled = !reverse;
            TraceStepText.Text = "";
            ShowTraceButton.IsEnabled = false;
            ShowReverseTraceButton.IsEnabled = false;
            BlockOverlay.Visibility = Visibility.Visible;

            return true;
        }

        private void EndTrace(string message)
        {
            MessageBox.Show(message, "Информация", MessageBoxButton.OK, MessageBoxImage.Information);

            BlockOverlay.Visibility = Visibility.Collapsed;
            SetEditControlsEnabled(false);
            ResetTraceButton.IsEnabled = true;
        }

        private bool PrepareTraceView()
        {
            var context = GetContext();

            if (context.ConditionSequences.Count != _rules.Count)
            {
                MessageBox.Show("Правила были изменены после запуска. Запустите систему заново.",
                    "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            _seqToRuleIndex = new Dictionary<ConditionSequence, int>();
            _itemToRuleIndex = new Dictionary<ConditionItem, int>();
            _itemToPosition = new Dictionary<ConditionItem, int>();
            _outputToRuleIndex = new Dictionary<ConditionItem, int>();

            for (int i = 0; i < context.ConditionSequences.Count; i++)
            {
                var seq = context.ConditionSequences[i];
                _seqToRuleIndex[seq] = i;
                _outputToRuleIndex[seq.Output] = i;

                var row = _rules[i];
                row.InputConditions.Clear();
                row.TraceStatus = "";

                for (int j = 0; j < seq.Input.Count; j++)
                {
                    var item = seq.Input[j];
                    _itemToRuleIndex[item] = i;
                    _itemToPosition[item] = j;
                    row.InputConditions.Add(new TraceConditionItem { Text = $"{item.Object} = {item.Value}" });
                }

                row.OutputCondition = new TraceConditionItem { Text = $"{seq.Output.Object} = {seq.Output.Value}" };
                row.IsTraceMode = true;
            }

            return true;
        }

        private void SetStepCaption(int step, int total, LogState state)
        {
            TraceStepText.Text = $"Шаг {step}/{total}. {Describe(state)}";
        }

        private static string Describe(LogState state)
        {
            switch (state.LogStateType)
            {
                case LogStateType.Inspect:
                    return $"Проверка «{Format(state.ConditionItem)}» на факте «{Format(state.InspectedToken)}».";
                case LogStateType.Partial:
                    return $"Условие «{Format(state.ConditionItem)}» выполнено.";
                case LogStateType.SequenceInspect:
                    return $"Проверка правила «{Format(state.ConditionSequence?.Output)}».";
                case LogStateType.Commit:
                    return $"Правило «{Format(state.ConditionSequence?.Output)}» сработало.";
                case LogStateType.AddVariable:
                    return $"Новая переменная «{Format(state.ExpertSystemToken)}» помещена в банк.";
                case LogStateType.GoalSet:
                    return $"Цель обратного прохода: «{Format(state.ExpertSystemToken)}».";
                case LogStateType.ReverseSequenceInspect:
                    return $"Итерация {state.Iteration}: проверка правила «{Format(state.ConditionSequence?.Output)}».";
                case LogStateType.ReverseInspect:
                    return $"Сравнение «{Format(state.ConditionItem)}» с целью «{Format(state.InspectedToken)}».";
                case LogStateType.ReverseCommit:
                    return $"Правило «{Format(state.ConditionSequence?.Output)}» подходит, его посылки становятся требованиями.";
                case LogStateType.BankAdd:
                    return $"Требование «{Format(state.ExpertSystemToken)}» добавлено в банк.";
                case LogStateType.TokenCheck:
                    return state.Checked == true
                        ? $"Факт «{Format(state.ExpertSystemToken)}» подтверждён исходными данными."
                        : $"Факт «{Format(state.ExpertSystemToken)}» не подтверждён исходными данными.";
                case LogStateType.PassEnd:
                    return $"Обратный проход завершён: {state.Message}.";
                default:
                    return state.LogStateType.ToString();
            }
        }

        private static string Format(ConditionItem? item)
            => item is null ? "?" : $"{item.Object} = {item.Value}";

        private static string Format(ExpertSystemToken? token)
            => token is null ? "?" : $"{token.Object} = {token.Value}";

        private async Task ApplyInspectHighlight(ConditionItem item, ExpertSystemToken? token)
        {
            ClearConditionHighlights();

            if (!_itemToRuleIndex.TryGetValue(item, out var idx) ||
                !_itemToPosition.TryGetValue(item, out var pos) ||
                idx < 0 || idx >= _rules.Count)
                return;

            _rules[idx].InputConditions[pos].Background = BrushConditionInspect;

            if (token is not null)
                HighlightToken(token, BrushConditionInspect);

            BringRuleIntoView(idx);
            await Task.CompletedTask;
        }

        private async Task ApplyPartialHighlight(ConditionItem item)
        {
            if (!_itemToRuleIndex.TryGetValue(item, out var idx) ||
                !_itemToPosition.TryGetValue(item, out var pos) ||
                idx < 0 || idx >= _rules.Count)
                return;

            _rules[idx].InputConditions[pos].Background = BrushRuleCommit;

            BringRuleIntoView(idx);
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

        private void ApplyGoalHighlight()
        {
            GoalTextBox.Background = BrushGoalActive;
        }

        private async Task ApplyReverseSequenceHighlight(ConditionSequence seq)
        {
            SetRuleStatus(seq, "Проверяем вывод правила");
            await ApplySequenceHighlight(seq, BrushRuleInspect);
        }

        private async Task ApplyReverseInspectHighlight(ConditionItem output, ExpertSystemToken goal)
        {
            if (_outputToRuleIndex.TryGetValue(output, out var idx) && idx < _rules.Count)
            {
                var row = _rules[idx];
                if (row.OutputCondition is not null)
                    row.OutputCondition.Background = BrushConditionInspect;

                BringRuleIntoView(idx);
            }

            ApplyGoalHighlight();
            await Task.CompletedTask;
        }

        private async Task ApplyReverseCommitHighlight(ConditionSequence seq)
        {
            SetRuleStatus(seq, "Найдено соответствие ТО проверяемому конечному элементу");

            if (_seqToRuleIndex.TryGetValue(seq, out var idx) && idx < _rules.Count &&
                _rules[idx].OutputCondition is TraceConditionItem output)
            {
                output.Background = BrushRuleCommit;
            }

            await ApplySequenceHighlight(seq, BrushRuleCommit);
        }

        private async Task ApplyBankAddHighlight(ConditionItem required, ExpertSystemToken token)
        {
            SetRuleStatus(required, "Данные ЕСЛИ добавлены в банк");

            if (_itemToRuleIndex.TryGetValue(required, out var idx) &&
                _itemToPosition.TryGetValue(required, out var pos) &&
                idx < _rules.Count)
            {
                _rules[idx].InputConditions[pos].Background = BrushConditionRequired;
                BringRuleIntoView(idx);
            }

            AddVariableToBank(token);
            await Task.CompletedTask;
        }

        private async Task ApplyTokenCheckHighlight(ExpertSystemToken token, bool confirmed)
        {
            foreach (var run in FindBankRuns(token))
            {
                run.Background = confirmed ? BrushFactConfirmed : BrushFactMissing;
                run.Foreground = confirmed ? Brushes.SeaGreen : Brushes.Firebrick;
            }

            if (confirmed)
                HighlightToken(token, BrushFactConfirmed);

            await Task.CompletedTask;
        }

        private async Task ApplyPassEndHighlight()
        {
            GoalTextBox.Background = null;
            TraceStepText.FontWeight = FontWeights.Bold;
            await Task.Delay(ReverseStepDelay);
        }

        private void SetRuleStatus(ConditionSequence seq, string text)
        {
            if (_seqToRuleIndex.TryGetValue(seq, out var idx) && idx < _rules.Count)
                _rules[idx].TraceStatus = text;
        }

        private void SetRuleStatus(ConditionItem item, string text)
        {
            if (_itemToRuleIndex.TryGetValue(item, out var idx) && idx < _rules.Count)
                _rules[idx].TraceStatus = text;
        }

        private void BringRuleIntoView(int index)
        {
            if (index < 0 || index >= _rules.Count)
                return;

            var container = RulesList.ItemContainerGenerator.ContainerFromIndex(index) as ContentPresenter;
            container?.BringIntoView();
        }

        private static string TokenKey(ExpertSystemToken token)
            => TokenKey(token.Object, token.Value);

        private static string TokenKey(string obj, string value)
            => $"{obj}\u0001{value}";

        private void AddVariableToBank(ExpertSystemToken token)
        {
            if (_bankIsEmpty)
            {
                BankRichTextBox.Document.Blocks.Clear();
                _bankIsEmpty = false;
            }

            var run = new Run(Format(token))
            {
                Foreground = Brushes.DarkOrange,
                FontWeight = FontWeights.SemiBold,
                Tag = TokenKey(token)
            };
            BankRichTextBox.Document.Blocks.Add(new Paragraph(run));
            BankRichTextBox.ScrollToEnd();
        }

        private void ClearBank()
        {
            BankRichTextBox.Document.Blocks.Clear();
            _bankIsEmpty = true;
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
                TokensRichTextBox.Document.Blocks.Add(new Paragraph(new Run(line) { Tag = TokenKey(obj, val) }));
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

        private IEnumerable<Run> FindBankRuns(ExpertSystemToken token)
        {
            var key = TokenKey(token);
            foreach (var run in EnumerateRuns(BankRichTextBox.Document))
            {
                if (run.Tag as string == key)
                    yield return run;
            }
        }

        private void ClearTokenHighlight()
        {
            foreach (var run in EnumerateRuns(TokensRichTextBox.Document))
            {
                if (run.Background == BrushConditionInspect || run.Background == BrushFactConfirmed)
                    run.Background = null;
            }
        }

        private void HighlightToken(ExpertSystemToken token, Brush brush)
        {
            var key = TokenKey(token);
            foreach (var run in EnumerateRuns(TokensRichTextBox.Document))
            {
                if (run.Tag as string == key)
                {
                    run.Background = brush;
                    break;
                }
            }
        }

        private void ClearConditionHighlights()
        {
            foreach (var rule in _rules)
            {
                foreach (var condition in rule.AllConditions)
                {
                    if (condition.Background == BrushConditionInspect)
                        condition.Background = null;
                }
            }
        }

        private void SetEditControlsEnabled(bool enabled)
        {
            foreach (var btn in FindVisualChildren<Button>(RulesList))
                if (btn.Content as string == "Удалить") btn.IsEnabled = enabled;
            foreach (var tb in FindVisualChildren<TextBox>(RulesList))
                tb.IsEnabled = enabled;

            AddRuleButton.IsEnabled = enabled;
            SaveButton.IsEnabled = enabled;
            ReloadButton.IsEnabled = enabled;
            RunButton.IsEnabled = enabled;
            CheckButton.IsEnabled = enabled;
            ShowTraceButton.IsEnabled = enabled;
            ShowReverseTraceButton.IsEnabled = enabled;
            GoalTextBox.IsEnabled = enabled;
            TokensRichTextBox.IsReadOnly = !enabled;
        }

        private void ResetTrace_Click(object sender, RoutedEventArgs e)
        {
            PopulateTokens(_originalTokensText);
            ClearBank();
            GoalTextBox.Background = null;

            foreach (var rule in _rules)
            {
                foreach (var condition in rule.AllConditions)
                    condition.Background = null;

                rule.TraceStatus = "";
                rule.OutputCondition = null;
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

            TraceStepText.Text = "";
            TraceStepText.FontWeight = FontWeights.Normal;
            BlockOverlay.Visibility = Visibility.Collapsed;
            SetEditControlsEnabled(true);
            ResetTraceButton.IsEnabled = false;
            _isTraceRunning = false;
        }
    }
}