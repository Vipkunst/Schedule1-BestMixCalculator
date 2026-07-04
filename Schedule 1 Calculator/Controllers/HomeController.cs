using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Schedule_1_Calculator.Models;
using Schedule_1_Calculator.Services;

namespace Schedule_1_Calculator.Controllers
{
    public class HomeController : Controller
    {
        MixCalculator _mixer;
        DataService _data;
        public HomeController(MixCalculator mixCalculator, DataService dataService)
        {
            _mixer = mixCalculator;
            _data = dataService;
        }

        public IActionResult Index()
        {
            Product product = _data.Products.Single(p => p.Id == "og_kush");

            var bestMix = _mixer.FindMostProfitableMix(4, product);
            return View(bestMix);
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
    }
}
