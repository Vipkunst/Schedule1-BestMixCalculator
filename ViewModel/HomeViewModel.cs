using Schedule_1_Calculator.Models;

namespace Schedule_1_Calculator.ViewModel
{
    public class HomeViewModel
    {
        public List<Product> BaseProducts { get; set; }
        public string SelectedProductId { get; set; }
        public int SelectedIngredientCount { get; set; }
        public List<Ingredient> BestComboIngredients { get; set; }
        public double BestProfit { get; set; }
        public double BestSellPrice { get; set; }
        public double BestComboCost { get; set; }
        public List<Effect> BestComboEffects { get; set; }
    }
}
