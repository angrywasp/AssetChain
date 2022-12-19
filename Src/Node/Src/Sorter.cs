using System.Collections.Generic;
using System.Linq;
using AngryWasp.Cryptography;

namespace Node
{
    public static class Sorter
    {
        public static List<Transaction> SortTransactions(this List<Transaction> txs)
        {
            //the default sorting mechanism is to sort in order of the numeric value of the address
            //and then sort for each address by the nonce
            var sortedByAddress = new SortedDictionary<EthAddress, List<Transaction>>();

            foreach (var t in txs)
            {
                if (!sortedByAddress.ContainsKey(t.From))
                    sortedByAddress.Add(t.From, new List<Transaction>());

                sortedByAddress[t.From].Add(t);
            }

            var sortedTransactions = new List<Transaction>();

            foreach (var s in sortedByAddress)
                sortedTransactions.AddRange(s.Value.OrderBy(x => x.Nonce));

            return sortedTransactions;
        }
    }
}