using AngryWasp.Cryptography;
using AngryWasp.Helpers;

namespace Common
{
    public static class Constants
    {
        public const int TX_THRESHOLD = 1;
        public const ushort DEFAULT_P2P_PORT = 1000;
        public const ushort DEFAULT_RPC_PORT = 10001;
        public const ulong VALIDATOR_STAKE = 10000ul * 1000000ul;
        public const int MINIMUM_PEERS = 3;
        public const int DECIMALS = 6;
        public const ulong TOTAL_SUPPLY = 50000000ul * 1000000ul;
        public const ulong MINIMUM_VALIDATOR_AGE = 1;
        public const int MAX_VALIDATOR_SLOTS = 8;
        public const byte CHAIN_ID = 128;

        public static readonly EthAddress[] GENESIS_VALIDATORS = new EthAddress[]
        {
            "0xa0736ed3C150868842c4c7F4b85Fe73eCF42AaF9",
            "0x9890b5466C04E8e7F52e35d3114aB6FFf45CaA99",
            "0xDcc9511b5D45B3B973646B7511EdcB351F9045a8",
            "0xe075ec53d0237659A2EB3190717DF87b5C935F9a"
        };

        public static readonly BigDecimal ValidatorStake = BigDecimal.Create(10000, DECIMALS);
    }
}