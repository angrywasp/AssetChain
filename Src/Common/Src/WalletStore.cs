using System;
using System.Collections.Generic;
using System.Linq;
using AngryWasp.Cryptography;
using AngryWasp.Logger;
using Nethereum.Signer;
using Newtonsoft.Json;
using Nethereum.Hex.HexTypes;
using System.Threading.Tasks;
using System.Text;

namespace Common
{
    public class CurrentWallet
    {
        private string name;
        private EthECKey key;
        private WalletDataStore data;
        private EthAddress address;
        private string password;

        public string Name => name;
        public EthECKey EcKey => key;
        public WalletDataStore Data => data;
        public EthAddress Address => address;
        public string Password => password;

        public CurrentWallet(string name, EthECKey key, string password, WalletDataStore data)
        {
            this.name = name;
            this.key = key;
            this.data = data;
            this.address = key.GetPublicAddress();
            this.password = password;
        }

        public byte[] Sign(byte[] data, bool isTransaction, HexBigInteger chainId = null)
        {
            if (isTransaction && chainId == null)
                throw new Exception("Chain ID must be set when signing a transaction");

            EthECDSASignature sig = null;

            if (!isTransaction)
                sig = key.SignAndCalculateV(Keccak.Hash256(data));
            else
                sig = key.SignAndCalculateV(Keccak.Hash256(data), chainId.Value);

            byte[] s = new byte[32];
            byte[] r = new byte[32];

            Buffer.BlockCopy(sig.S, 0, s, 32 - sig.S.Length, sig.S.Length);
            Buffer.BlockCopy(sig.R, 0, r, 32 - sig.R.Length, sig.R.Length);

            return r.Concat(s).Concat(sig.V).ToArray();
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class WalletStore
    {
        [JsonProperty("d")]
        private Dictionary<EthAddress, (string Name, byte[] EncryptedSeed, int Index, WalletDataStore Store)> stores { get; set; } =
            new Dictionary<EthAddress, (string Name, byte[] EncryptedSeed, int Index, WalletDataStore Store)>();
        private static CurrentWallet current;

        public static CurrentWallet Current => current;

        public static Action Save { get; set; }

        public Dictionary<EthAddress, (string Name, byte[] EncryptedSeed, int Index, WalletDataStore Store)> Stores => stores;

        public int Count => stores.Count;

        public List<(string Name, byte[] EncryptedSeed, int Index, EthAddress Address)> Accounts =>
            stores.Select(x => (x.Value.Name, x.Value.EncryptedSeed, x.Value.Index, x.Key)).ToList();

        public (string Name, byte[] EncryptedSeed, int Index, WalletDataStore Store) this[EthAddress key]
        {
            get
            {
                if (!stores.ContainsKey(key))
                    return default;

                return stores[key];
            }
        }

        public bool ContainsKey(EthAddress key) => stores.ContainsKey(key);

        public EthAddress AddFromSeed(string name, string seed, int index, string password)
        {
            try
            {
                var encryptedSeed = Aes.Encrypt(Encoding.Unicode.GetBytes(seed), password.ToAesKey());
                var key = new Mnemonic().CreateWalletFromSeed(seed, index);
                var address = key.GetPublicAddress();
                stores.Add(address, (name, encryptedSeed, index, new WalletDataStore()));
                return address;
            }
            catch (Exception ex)
            {
                Log.Instance.WriteError(ex.Message);
#if DEBUG
                Log.Instance.WriteError(ex.StackTrace);
#endif
                return EthAddress.Empty;
            }
        }

        public void Remove(EthECKey key)
        {
            var address = (EthAddress)key.GetPublicAddress();

            if (stores.ContainsKey(address))
                stores.Remove(address);
        }

        public EthAddress FirstWalletAddress => stores.Count == 0 ? EthAddress.Empty : stores.First().Key;
        public EthAddress LastWalletAddress => stores.Count == 0 ? EthAddress.Empty : stores.Last().Key;

        public CurrentWallet GetFirst(string password)
        {
            if (stores.Count == 0)
                return null;

            var first = stores.First().Value;

            try
            {
                if (password == null)
                    password = string.Empty;

                var key = DecryptWalletData(first.EncryptedSeed, first.Index, password);
                return new CurrentWallet(first.Name, key, password, first.Store);
            }
            catch (Exception ex)
            {
                Log.Instance.WriteError(ex.Message);
#if DEBUG
                Log.Instance.WriteError(ex.StackTrace);
#endif 
                return null;
            }
        }

        public bool CheckPassword(string password)
        {
            var firstWallet = stores.First();

            byte[] decryptedData = null;
            EthECKey key;
            if (!Aes.Decrypt(firstWallet.Value.EncryptedSeed, password.ToAesKey(), out decryptedData))
                return false;

            string seed = Encoding.Unicode.GetString(decryptedData);
            key = new Mnemonic().CreateWalletFromSeed(seed, firstWallet.Value.Index);

            return key.GetPublicAddress() == firstWallet.Key;
        }

#pragma warning disable CS1998

        public async Task<bool> MakeCurrentWallet(EthAddress address, string password)
        {
            current = null;

            try
            {
                if (password == null) password = string.Empty;

                if (!stores.ContainsKey(address))
                    return false;

                var store = stores[address];
                var oldWallet = current;

                current = new CurrentWallet(store.Name, DecryptWalletData(store.EncryptedSeed, store.Index, password), password, store.Store);

                return true;
            }
            catch { return false; }
        }

#pragma warning restore CS1998

        public static void ClearCurrentWallet() => current = null;

        private static EthECKey DecryptWalletData(byte[] encryptedSeed, int index, string password)
        {
            byte[] decryptedData = null;
            EthECKey key = null;

            if (!Aes.Decrypt(encryptedSeed, password.ToAesKey(), out decryptedData))
                return null;

            string seed = Encoding.Unicode.GetString(decryptedData);
            key = new Mnemonic().CreateWalletFromSeed(seed, index);

            return key;
        }
    }
}
