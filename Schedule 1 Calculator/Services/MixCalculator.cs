using Schedule_1_Calculator.Models;

namespace Schedule_1_Calculator.Services
{
    public class MixCalculator
    {
        DataService _data;
        public MixCalculator(DataService dataService)
        {
            _data = dataService;
        }

        public (List<Ingredient> bestComboIngredients, double bestProfit, double bestSellPrice, double bestComboCost, List<Effect> bestComboEffects) FindMostProfitableMix(int ingredientCount, Product product)
        {
            var allIngredients = _data.Ingredients;

            if (ingredientCount > 8)
                throw new ArgumentException("Only up to 8 ingredients supported");

            var bestProfit = double.MinValue;
            List<Ingredient> bestComboIngredients = new();
            double bestSellPrice = 0;
            double bestComboCost = 0;
            List<Effect> bestComboEffects = new();

            foreach (var combo in GetCombinations(allIngredients, ingredientCount))
            {
                var comboCost = combo.Sum(i => i.Cost);

                // Try all orderings — mixing order affects which complex effects trigger
                foreach (var ordering in GetPermutations(combo))
                {
                    var effects = SimulateMix(product.Effects.ToList(), ordering);
                    double sellPrice = product.BasePrice * (1 + effects.Sum(e => e.Value));
                    double profit = sellPrice - comboCost;

                    if (profit > bestProfit)
                    {
                        bestProfit = profit;
                        bestComboIngredients = combo.ToList();
                        bestComboEffects = effects;
                        bestSellPrice = Math.Ceiling(sellPrice);
                        bestComboCost = comboCost;
                    }
                }
            }

            return (bestComboIngredients, bestProfit, bestSellPrice, bestComboCost, bestComboEffects);
        }

        // Simulate adding ingredients one at a time, applying complex effect rules after each addition.
        // Rule: if the current effect list contains BaseEffectIndicator and we're adding IngredientToAdd,
        //       the base effect is replaced by ComplexEffect before adding the ingredient's own effects.
        private List<Effect> SimulateMix(List<Effect> productEffects, IEnumerable<Ingredient> ingredients)
        {
            var current = productEffects.ToList();

            foreach (var ingredient in ingredients)
            {
                // Apply complex transforms triggered by this ingredient
                foreach (var rule in _data.ComplexMixes.Where(r => r.IngredientToAdd.Id == ingredient.Id))
                {
                    var baseEffect = current.FirstOrDefault(e => e.Id == rule.BaseEffectIndicator);
                    if (baseEffect != null)
                    {
                        current.Remove(baseEffect);
                        var complexEffect = _data.Effects.Single(e => e.Id == rule.ComplexEffect);
                        if (!current.Any(e => e.Id == complexEffect.Id))
                            current.Add(complexEffect);
                    }
                }

                // Add the ingredient's own effects (skip duplicates)
                foreach (var effect in ingredient.Effects)
                {
                    if (!current.Any(e => e.Id == effect.Id))
                        current.Add(effect);
                }
            }

            return current;
        }

        private IEnumerable<List<Ingredient>> GetCombinations(List<Ingredient> ingredients, int length)
        {
            if (length == 0) yield return new List<Ingredient>();
            else
            {
                for (int i = 0; i < ingredients.Count; i++)
                {
                    var remaining = ingredients.Skip(i + 1).ToList();
                    foreach (var combination in GetCombinations(remaining, length - 1))
                    {
                        var combo = new List<Ingredient> { ingredients[i] };
                        combo.AddRange(combination);
                        yield return combo;
                    }
                }
            }
        }

        private IEnumerable<List<Ingredient>> GetPermutations(List<Ingredient> ingredients)
        {
            if (ingredients.Count <= 1) { yield return ingredients.ToList(); yield break; }
            for (int i = 0; i < ingredients.Count; i++)
            {
                var rest = ingredients.Where((_, idx) => idx != i).ToList();
                foreach (var perm in GetPermutations(rest))
                {
                    var result = new List<Ingredient> { ingredients[i] };
                    result.AddRange(perm);
                    yield return result;
                }
            }
        }
    }
}
