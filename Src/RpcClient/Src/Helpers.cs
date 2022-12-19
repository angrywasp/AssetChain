using AngryWasp.Helpers;
using Common;

namespace RpcClient
{
    public static class Helpers
    {
        public static BigDecimal FromAtomicUnits(this ulong value) => BigDecimal.Create(value, Constants.DECIMALS);

        public static ulong ToAtomicUnits(this BigDecimal value) => (ulong)value.Mantissa;
    }
}