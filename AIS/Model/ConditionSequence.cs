using AIS.Math;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIS.Model
{

    internal class ConditionSequence
    {
        public List<ConditionItem> Input { get; set; } = [];
        public required ConditionItem Output { get; init; }

        public bool TryFit(List<ExpertSystemToken> input, out ConditionItem newItem)
        {
            for (int begin = 0; begin < input.Count; begin++)
            {
                foreach (var item in Input.Where(i => !i.IsChecked))
                {
                    LogContext.AddInspectState(item, input[begin]);
                    if (item.Fit(input[begin]))
                    {
                        LogContext.AddPartialState(item);
                    }
                }
            }

            bool result = Input.All(i => i.IsChecked);

            if (result)
            {
                newItem = Output;
            }
            else
            {
                newItem = null!;
            }

            return result;
        }
    }
}
