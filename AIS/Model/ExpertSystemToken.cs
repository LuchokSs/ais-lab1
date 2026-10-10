using System;
using System.Collections.Generic;
using System.Text;

namespace AIS.Model
{
    internal class ExpertSystemToken
    {
        public string Object { get; set; }
        public string Value { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj is ExpertSystemToken other)
            {
                return this.Object == other.Object && this.Value == other.Value;
            }

            return base.Equals(obj);
        }
    }
}
