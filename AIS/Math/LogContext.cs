using AIS.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIS.Math
{
    internal static class LogContext
    {
        public static List<LogState> States { get; set; } = new List<LogState>();

        internal static void AddInspectState(ConditionItem inspected, ExpertSystemToken token)
        {
            States.Add(new LogState() { ConditionItem = inspected, LogStateType = LogStateType.Inspect, InspectedToken = token });
        }

        internal static void AddPartialState(ConditionItem inspected)
        {
            States.Add(new LogState() { ConditionItem = inspected, LogStateType = LogStateType.Partial });
        }

        internal static void AddCommitState(ConditionSequence inspected)
        {
            States.Add(new LogState() { ConditionSequence = inspected, LogStateType = LogStateType.Commit });
        }

        internal static void AddVariableState(ExpertSystemToken token)
        {
            States.Add(new LogState() { ExpertSystemToken = token, LogStateType = LogStateType.AddVariable });
        }

        internal static void AddConditionInspectState(ConditionSequence conditionSequence)
        {
            States.Add(new LogState() { ConditionSequence = conditionSequence, LogStateType = LogStateType.SequenceInspect });
        }
    }
}
