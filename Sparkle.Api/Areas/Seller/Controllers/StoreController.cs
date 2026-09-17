using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sparkle.Api.Areas.Seller.Controllers
{
    [Area("Seller")]
    [Authorize(Roles = "Seller")]
    public class StoreController : Controller
    {
        public IActionResult Settings()
        {
            return View();
        }
        
        public IActionResult Profile() 
        {
            return View();
        }
    }
}
