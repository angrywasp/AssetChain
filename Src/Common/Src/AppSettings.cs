using System;
using System.IO;
using System.Threading.Tasks;
using AngryWasp.Cryptography;
using AngryWasp.Logger;
using Newtonsoft.Json;

namespace Common
{
    [JsonObject(MemberSerialization.OptIn)]
    public partial class AppSettings<T>
    {
        public delegate void CurrentWalletChangedEventHandler();
        public event CurrentWalletChangedEventHandler CurrentWalletChanged;

        private EthAddress lastWallet = EthAddress.Empty;

        public static string DefaultPath { get; set; }

        [JsonProperty("appData")]
        public T AppData { get; set; } = default;
        
        [JsonProperty("lastWallet")]
        public EthAddress LastWallet
        {
            get
            {
                if (WalletStore.Count == 0)
                    return EthAddress.Empty;

                if (!WalletStore.ContainsKey(lastWallet))
                    lastWallet = EthAddress.Empty;

                if (lastWallet == EthAddress.Empty)
                    lastWallet = WalletStore.FirstWalletAddress;

                return lastWallet;
            }
            set => lastWallet = value;
        }

        [JsonProperty("walletStore")]
        public WalletStore WalletStore { get; set; } = new WalletStore();

        public static (AppSettings<T>, bool) Load(string walletFileName)
        {
            try
            {
                DefaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), $"{walletFileName}.settings");
                var settings = new AppSettings<T>();
                var isNew = true;
                if (File.Exists(DefaultPath))
                {
                    string json = File.ReadAllText(DefaultPath);
                    settings = JsonConvert.DeserializeObject<AppSettings<T>>(json);
                    isNew = false;
                }

                WalletStore.Save = () => { settings.Save(); };
                return (settings, isNew);
            }
            catch (Exception ex)
            {
                Log.Instance.WriteError(ex.Message);
#if DEBUG
                Log.Instance.WriteError(ex.StackTrace);
#endif
                return (null, false);
            }
        }

        public void Save()
        {
            try
            {
                var json = JsonConvert.SerializeObject(this);

                StreamWriter sw;

                if (File.Exists(DefaultPath))
                    sw = new StreamWriter(File.Open(DefaultPath, FileMode.Truncate, FileAccess.Write));
                else
                    sw = new StreamWriter(File.Open(DefaultPath, FileMode.OpenOrCreate, FileAccess.Write));

                sw.Write(json);
                sw.Flush();
                sw.Close();
            }
            catch (Exception ex)
            {
                Log.Instance.WriteError(ex.Message);
#if DEBUG
                Log.Instance.WriteError(ex.StackTrace);
#endif
            }
        }

        public bool CreateWallet(string name, string password, string seed)
        {
            try
            {
                WalletStore.AddFromSeed(name, seed, 0, password);
                Save();
                return true;
            }
            catch (Exception ex)
            {
                Log.Instance.WriteError(ex.Message);
#if DEBUG
                Log.Instance.WriteError(ex.StackTrace);
#endif
                return false;
            }
        }

        public async Task<bool> SwitchWallets(EthAddress newWallet, string password)
        {
            Save();
            LastWallet = newWallet;
            var madeCurrent = await WalletStore.MakeCurrentWallet(newWallet, password).ConfigureAwait(false);
            if (!madeCurrent)
                return false;

            Save();
            CurrentWalletChanged?.Invoke();
            return true;
        }
    }
}