namespace RpcClient
{
    public class AccountBalance
    {
        public ulong Current { get; set; } = 0;
        public ulong Available { get; set; } = 0;

        public override string ToString()
        {
            return $"Current: {Current}, Available: {Available}";
        }
    }
}