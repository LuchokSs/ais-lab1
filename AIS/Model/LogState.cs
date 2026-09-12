using System;
using System.Collections.Generic;
using System.Text;

namespace AIS.Model
{
    internal enum LogStateType
    {
        Inspect,
        Partial,
        Commit,
        AddVariable,
        SequenceInspect
    }

    internal class LogState
    {
        public LogStateType LogStateType { get; set; }
        public ConditionItem? ConditionItem { get; set; } = null;
        public ConditionSequence? ConditionSequence { get; set; } = null;
        public ExpertSystemToken? ExpertSystemToken { get; set; } = null;
        public ExpertSystemToken? InspectedToken { get; set; } = null;
    }
}
