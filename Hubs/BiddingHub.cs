using Microsoft.AspNet.SignalR;
using System.Threading.Tasks;

namespace CallUp.Hubs
{
    public class BiddingHub : Hub
    {
        public void JoinRequestGroup(int requestId)
        {
            Groups.Add(Context.ConnectionId, $"Request_{requestId}");
        }

        public void SendNewBid(int requestId, decimal amount, string providerName)
        {
            Clients.Group($"Request_{requestId}").newBidReceived(new {
                requestId = requestId,
                amount = amount,
                providerName = providerName
            });
        }
    }
}
