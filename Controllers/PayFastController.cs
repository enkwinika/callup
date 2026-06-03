using System.Web.Mvc;

namespace CallUp.Controllers
{
    public class PayFastController : Controller
    {
        // GET: PayFast/Checkout?bidId=X&amount=Y
        public ActionResult Checkout(int bidId, decimal amount)
        {
            ViewBag.BidId = bidId;
            ViewBag.Amount = amount;
            return View();
        }

        // Post: PayFast/Notify (Simulated ITN)
        [HttpPost]
        public ActionResult Notify(int bidId)
        {
            // In a real app, this would be an API call from PayFast
            // We'll redirect back to Dashboard/ProcessPayment
            return RedirectToAction("ProcessPayment", "Dashboard", new { bidId = bidId });
        }
    }
}
