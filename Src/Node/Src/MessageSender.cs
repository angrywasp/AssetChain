using System.Collections.Generic;
using System.Threading.Tasks;
using AngryWasp.Net;

namespace Node
{
    public static class MessageSender
    {
        public static async Task BroadcastAsync(byte[] request, Connection from = null)
        {
            List<Connection> disconnected = new List<Connection>();

            await ConnectionManager.ForEach(Direction.Incoming | Direction.Outgoing, async (c) =>
            {
                if (from != null && c.PeerId == from.PeerId)
                    return; //don't return to the sender

                if (!await c.WriteAsync(request).ConfigureAwait(false))
                    disconnected.Add(c);

            }).ConfigureAwait(false);

            foreach (var c in disconnected)
                await ConnectionManager.RemoveAsync(c, "Not responding").ConfigureAwait(false);
        }
    }
}