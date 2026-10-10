using System;
using System.Collections.Generic;
using System.Text;

namespace AIS.Model
{
    internal class TokenBankTree
    {
        public Dictionary<ExpertSystemToken, TokenBankTree> Inner { get; set; } = new();
        public required TokenBankTree? Parent { get; set; }

        public List<ExpertSystemToken> GetCurrentTokens()
        {
            return GetLeafs().SelectMany(l => l.Inner.Keys).ToList();
        }

        internal void Add(ExpertSystemToken token, ExpertSystemToken required)
        {
            if (Inner.Keys.Any(k => k.Equals(token)))
            {
                Inner[token].AddToken(required);
            }
            else
            {
                foreach (var inner in Inner.Keys)
                {
                    Inner[inner].Add(token, required);
                }
            }
        }

        internal void AddToken(ExpertSystemToken token)
        {
            Inner.Add(token, new() { Parent = this });
        }

        internal IEnumerable<TokenBankTree> GetLeafs()
        {
            if (Inner.Values.All(i => i.Inner.Count == 0)) return [this];

            return Inner.SelectMany(i => i.Value.GetLeafs());
        }
    }
}
