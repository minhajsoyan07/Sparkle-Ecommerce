using Microsoft.AspNetCore.Mvc;

namespace Sparkle.Api.Controllers;

public class InfoController : Controller
{
    [HttpGet("about")]
    public IActionResult About()
    {
        return View();
    }

    [HttpGet("help")]
    public IActionResult Help()
    {
        return View();
    }

    [HttpGet("returns")]
    public IActionResult Returns()
    {
        return View();
    }

    [HttpGet("shipping")]
    public IActionResult Shipping()
    {
        return View();
    }

    [HttpGet("privacy")]
    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet("terms")]
    public IActionResult Terms()
    {
        return View();
    }
}
