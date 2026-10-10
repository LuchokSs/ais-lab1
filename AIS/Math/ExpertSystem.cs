using AIS.Model;
using System;
using System.Collections.Generic;
using System.Security.RightsManagement;
using System.Text;
using System.Windows.Markup.Localizer;
using static System.Net.Mime.MediaTypeNames;

namespace AIS.Math
{
    internal class ExpertSystemVerdict
    {
        public bool IsSuccess { get; set; }
        public required string Result { get; init; }
    }

    internal class ExpertSystem
    {
        public string Subsymbols { get; set; } = ",.;!?1234567890+:-/";

        private List<ExpertSystemToken> _tokens = new List<ExpertSystemToken>();

        public ExpertSystem()
        {

        }

        public ExpertSystemToken ParseToken(string text)
        {
            text = text.Trim();

            foreach (var sym in Subsymbols.Select(c => c.ToString()))
            {
                text = text.Replace(sym, "");
            }

            ExpertSystemToken newToken = new ExpertSystemToken()
            {
                Object = text
                    .Split("=", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .First(),
                Value = text
                    .Split("=", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .ElementAtOrDefault(1)
                    ?? "Да",
            };

            return newToken;
        }

        public void AddToken(string text)
        {
            _tokens.Add(ParseToken(text));
        }

        public ExpertSystemVerdict GetResult()
        {
            ConditionsContext context = ConditionsContext.Get();

            bool flag = true;
            List<ExpertSystemToken> tokens = [.._tokens];
            List<ConditionSequence> conditions = [..context.ConditionSequences];

            while (flag) 
            {
                flag = false;
                for (int i = 0; i < conditions.Count; i++)
                {
                    LogContext.AddConditionInspectState(conditions[i]);
                    if (conditions[i].TryFit(tokens, out ConditionItem newItem))
                    {
                        LogContext.AddCommitState(conditions[i]);
                        conditions.RemoveAt(i);
                        tokens.Add(new ExpertSystemToken() { Object = newItem.Object, Value =  newItem.Value });
                        i--;
                        flag = true;
                        LogContext.AddVariableState(new ExpertSystemToken() { Object = newItem.Object, Value = newItem.Value });
                    }
                }
            }

            if (tokens.FirstOrDefault(t => t.Object.Equals("специальность", StringComparison.OrdinalIgnoreCase)) is not null and var result)
            {
                return new ExpertSystemVerdict() { Result = result.Value, IsSuccess = true };
            }
            else
            {
                return new ExpertSystemVerdict() { Result = "Предоставьте дополнительные факты.", IsSuccess = false };
            }
        }

        public ExpertSystemVerdict GetRevesedResult(ExpertSystemToken result)
        {
            ConditionsContext context = ConditionsContext.Get();

            bool flag = true;
            List<ExpertSystemToken> tokens = [.. _tokens];

            TokenBankTree bank = new() { Parent = null };
            bank.AddToken(result);

            List<ConditionSequence> conditions = [.. context.ConditionSequences];

            LogContext.AddGoalSetState(result);

            int iteration = 0;

            while (flag)
            {
                flag = false;
                iteration++;

                for (int i = 0; i < conditions.Count; i++)
                {
                    LogContext.AddReverseConditionInspectState(conditions[i], iteration);

                    foreach (var token in bank.GetCurrentTokens())
                    {
                        if (conditions[i].ReverseFit(token, out List<ConditionItem> items))
                        {
                            LogContext.AddReverseCommitState(conditions[i]);

                            foreach (var item in items)
                            {
                                var required = new ExpertSystemToken() { Object = item.Object, Value = item.Value };
                                LogContext.AddBankAddState(item, required);
                                bank.Add(token, required);
                            }

                            flag = true;
                            conditions.RemoveAt(i);
                            i--;
                        }
                    }
                }
            }

            bool verified = false;

            if (bank.GetLeafs().Count() == 0)
            {
                verified = false;
            }

            var bankState = bank.GetLeafs().ToList();

            while (bankState.Any(bs => bs != null))
            {
                for (int i = 0; i < bankState.Count(); i++)
                {
                    if (bankState[i] == null) continue;

                    if (bankState[i].Inner.Keys.All(k => tokens.Any(t => t.Equals(k))))
                    {
                        verified = true;
                        goto ver;
                    }
                    else
                    {
                        bankState[i] = bankState[i].Parent;
                    }
                }
            }

        ver: if (verified)
            {
                return new ExpertSystemVerdict() { Result = "Ситуация возможна", IsSuccess = true };
            }
            else
            {
                return new ExpertSystemVerdict() { Result = "Ситуация недостижима", IsSuccess = true };
            }
        }
    }
}
