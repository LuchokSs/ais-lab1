using AIS.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AIS.Math
{
    internal class ConditionsContext
    {
        private string _path;

        private ConditionsContext(string path) 
        {
            _path = path;
            string data = File.ReadAllText(path);
            ConditionSequences = ConditionsParser.UnpackSequences(data);
        }

        private static ConditionsContext? _conditionContext = null;

        public static ConditionsContext Get(string? path = null)
        {
            if (_conditionContext is null)
            {
                _conditionContext = new ConditionsContext(path ?? "./db.txt");
            }

            return _conditionContext;
        }

        public List<ConditionSequence> ConditionSequences { get; set; } = new List<ConditionSequence>();

        public void AddConditionSequence(ConditionSequence sequence)
        {
            ConditionSequences.Add(sequence);
        }

        public void RemoveConditionSequence(ConditionSequence sequence) 
        { 
            ConditionSequences.Remove(sequence); 
        }

        public void SaveConditionSequences()
        {
            string data = ConditionsParser.PackSequences(ConditionSequences);
            File.WriteAllText(_path, data);
        }
    }
}
