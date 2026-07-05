using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Schedule_1_Calculator.Models;
using Schedule_1_Calculator.Services;
using Schedule_1_Calculator.ViewModel;

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

        public IActionResult Index(string? product = "og_kush", int count = 4)
        {
            // Fall back to sensible defaults if the request asks for something that isn't there.
            Product selectedProduct = _data.Products.FirstOrDefault(p => p.Id == product)
                ?? _data.Products.Single(p => p.Id == "og_kush");
            count = Math.Clamp(count, 1, 8);

            var bestMix = _mixer.FindMostProfitableMix(count, selectedProduct);
            var viewModel = new HomeViewModel
            {
                BaseProducts = _data.Products,
                SelectedProductId = selectedProduct.Id,
                SelectedIngredientCount = count,
                BestComboIngredients = bestMix.bestComboIngredients,
                BestProfit = bestMix.bestProfit,
                BestSellPrice = bestMix.bestSellPrice,
                BestComboCost = bestMix.bestComboCost,
                BestComboEffects = bestMix.bestComboEffects
            };
            // Render the Index view explicitly so the shared Mix action (which delegates here)
            // doesn't look for a non-existent "Mix" view.
            return View("Index", viewModel);
        }

        public IActionResult Mix(string? product = "og_kush", int count = 4)
        {
            return Index(product, count);
        }

        public IActionResult About()
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
    }
}
