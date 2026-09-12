using AIS.Model;
using System;
using System.Collections.Generic;
using System.Security.RightsManagement;
using System.Text;

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

        public void AddToken(string text)
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

            _tokens.Add(newToken);
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
    }
}
