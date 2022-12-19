using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AngryWasp.Cryptography;
using AngryWasp.Helpers;
using AngryWasp.Logger;
using AngryWasp.Net;

namespace Node.NetworkMessages
{
    public class SyncTransactionPoolNetworkCommand
    {
        public const byte CODE = 17;

        public static async Task<byte[]> GenerateRequest()
        {
            List<byte> existing = new List<byte>();
            var txPool = await Blockchain.GetTransactionPoolAsync().ConfigureAwait(false);
            foreach (var tx in txPool.Values)
                existing.AddRange(tx.Hash);

            var request = Header.Create(CODE, true, (ushort)existing.Count);
            request.AddRange(existing);

            return request.ToArray();
        }

        public static async Task GenerateResponse(Connection c, Header h, byte[] d)
        {
            if (h.IsRequest)
            {
                var hashSet = new HashSet<HashKey32>();
                for (int i = 0; i < d.Length; i += 32)
                    hashSet.Add(d.Skip(i).Take(32).ToArray());

                var txPool = await Blockchain.GetTransactionPoolAsync().ConfigureAwait(false);
                var txs = txPool.Where(x => !hashSet.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);

                List<byte> responseData = new List<byte>();
                responseData.AddRange(txs.Count.ToByte());

                foreach (var tx in txs.Values)
                    responseData.AddRange(tx.ToBinary());

                var response = Header.Create(CODE, false, (ushort)responseData.Count);
                response.AddRange(responseData);

#pragma warning disable CS4014
                c.WriteAsync(response.ToArray());
#pragma warning restore CS4014
            }
            else
            {
                int offset = 0;
                int count = d.ToInt(ref offset);

                var verificationTasks = new List<Task<(bool, Transaction)>>();

                for (int i = 0; i < count; i++)
                {
                    verificationTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            Transaction tx;
                            if (!Transaction.FromBinary(d, ref offset, out tx))
                                return (false, null);

                            var txVerified = await tx.VerifyAsync().ConfigureAwait(false);

                            if (!txVerified)
                                return (false, null);

                            return (true, tx);
                        }
                        catch (Exception ex)
                        {
                            Log.Instance.WriteException(ex);
                            Debugger.Break();
                            return (false, null);
                        }
                    }));
                }

                await Task.WhenAll(verificationTasks).ConfigureAwait(false);

                foreach (var task in verificationTasks)
                {
                    var result = await task.ConfigureAwait(false);
                    if (result.Item1)
                        await Blockchain.AddToTxPoolAsync(result.Item2).ConfigureAwait(false);
                    else
                    {
                        await ConnectionManager.RemoveAsync(c, "Invalid transaction").ConfigureAwait(false);
                        return;
                    }
                }
            }
        }
    }
}