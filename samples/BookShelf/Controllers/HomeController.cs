using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using BookShelf.Models;

namespace BookShelf.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [ActionName("StatusCode")]
    public IActionResult StatusCodePage(int id)
    {
        if (id == 404)
        {
            return View("NotFound");
        }

        return View("StatusCode", id);
    }
}
