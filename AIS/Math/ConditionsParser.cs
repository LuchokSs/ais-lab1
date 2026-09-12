using AIS.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIS.Math
{
    internal static class ConditionsParser
    {
        public static List<ConditionSequence> UnpackSequences(string file)
        {
            var sequences = new List<ConditionSequence>();
            var lines = file.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var line in lines)
            {
                var arrowIndex = line.IndexOf("=>");
                if (arrowIndex < 0) continue;

                var outputStr = line[..arrowIndex].Trim();
                var inputStr = line[(arrowIndex + 2)..].Trim();

                var output = ParseConditionItem(outputStr);
                if (output is null) continue;

                var inputs = new List<ConditionItem>();
                var inputParts = inputStr.Split('И', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var part in inputParts)
                {
                    var item = ParseConditionItem(part.Trim());
                    if (item is not null)
                        inputs.Add(item);
                }

                sequences.Add(new ConditionSequence
                {
                    Output = output,
                    Input = inputs
                });
            }

            return sequences;
        }

        public static string PackSequences(List<ConditionSequence> sequences)
        {
            var sb = new StringBuilder();

            foreach (var seq in sequences)
            {
                sb.Append($"({seq.Output.Object} = {seq.Output.Value}) => ");

                for (int i = 0; i < seq.Input.Count; i++)
                {
                    if (i > 0) sb.Append(" И ");
                    var condition = seq.Input[i];
                    sb.Append($"({condition.Object} = {condition.Value})");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static ConditionItem? ParseConditionItem(string str)
        {
            str = str.Trim();
            if (str.Length >= 2 && str[0] == '(' && str[^1] == ')')
                str = str[1..^1].Trim();

            var eqIndex = str.IndexOf('=');
            if (eqIndex < 0) return null;

            var obj = str[..eqIndex].Trim();
            var val = str[(eqIndex + 1)..].Trim();

            if (obj.Length == 0 || val.Length == 0) return null;

            return new ConditionItem { Object = obj, Value = val };
        }
    }
}
