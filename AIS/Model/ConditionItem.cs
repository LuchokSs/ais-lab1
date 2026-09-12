using System;
using System.Collections.Generic;
using System.Security.Cryptography.Xml;
using System.Text;

namespace AIS.Model
{
    internal class ConditionItem
    {
        public required string Object { get; init; }
        public required string Value { get; init; }

        public bool IsChecked { get; set; } = false;

        public bool Fit(ExpertSystemToken input)
        {
            if (IsChecked is true) return true;

            return IsChecked = Object.Equals(input.Object, StringComparison.OrdinalIgnoreCase) && Value.Equals(input.Value, StringComparison.OrdinalIgnoreCase);
        }
    }
}
