using AIS.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIS.Math
{
    internal static class LogContext
    {
        public static List<LogState> States { get; set; } = new List<LogState>();

        public static List<LogState> ReverseStates { get; set; } = new List<LogState>();

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

        internal static void AddGoalSetState(ExpertSystemToken goal)
        {
            ReverseStates.Add(new LogState() { ExpertSystemToken = goal, LogStateType = LogStateType.GoalSet });
        }

        internal static void AddReverseConditionInspectState(ConditionSequence conditionSequence, int iteration)
        {
            ReverseStates.Add(new LogState() { ConditionSequence = conditionSequence, LogStateType = LogStateType.ReverseSequenceInspect, Iteration = iteration });
        }

        internal static void AddReverseInspectState(ConditionItem inspected, ExpertSystemToken goal)
        {
            ReverseStates.Add(new LogState() { ConditionItem = inspected, InspectedToken = goal, LogStateType = LogStateType.ReverseInspect });
        }

        internal static void AddReverseCommitState(ConditionSequence conditionSequence)
        {
            ReverseStates.Add(new LogState() { ConditionSequence = conditionSequence, LogStateType = LogStateType.ReverseCommit });
        }

        internal static void AddBankAddState(ConditionItem required, ExpertSystemToken token)
        {
            ReverseStates.Add(new LogState() { ConditionItem = required, ExpertSystemToken = token, LogStateType = LogStateType.BankAdd });
        }

        internal static void AddTokenCheckState(ExpertSystemToken token, bool confirmed)
        {
            ReverseStates.Add(new LogState() { ExpertSystemToken = token, LogStateType = LogStateType.TokenCheck, Checked = confirmed });
        }

        internal static void AddPassEndState(string message)
        {
            ReverseStates.Add(new LogState() { LogStateType = LogStateType.PassEnd, Message = message });
        }
    }
}