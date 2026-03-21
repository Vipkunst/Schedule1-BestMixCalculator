using Schedule_1_Calculator.Models;
using System.Text.Json;

namespace Schedule_1_Calculator.Services
{
    public class DataService
    {
        public List<Ingredient> Ingredients { get; private set; } = new();
        public List<Effect> Effects { get; private set; } = new();
        public List<ComplexMix> ComplexMixes { get; private set; } = new();
        public List<Product> Products { get; private set; } = new();

        public DataService(IWebHostEnvironment env)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // Load Effects
            var effectsData = File.ReadAllText(Path.Combine(env.ContentRootPath, "Data", "effects.json"));
            Effects = JsonSerializer.Deserialize<List<Effect>>(effectsData, options) ?? new List<Effect>();

            // Load Ingredients and resolve Effect references
            var ingredientsData = File.ReadAllText(Path.Combine(env.ContentRootPath, "Data", "ingredients.json"));
            var rawIngredients = JsonSerializer.Deserialize<List<RawIngredient>>(ingredientsData, options) ?? new List<RawIngredient>();
            Ingredients = rawIngredients.Select(ri => new Ingredient
            {
                Id = ri.Id,
                Cost = ri.Cost,
                Effects = ri.Effects.Select(effectId => Effects.FirstOrDefault(e => e.Id == effectId) 
                    ?? throw new Exception($"Effect '{effectId}' not found in effects.json")).ToList()
            }).ToList();

            // Load ComplexMixes
            var complexMixesData = File.ReadAllText(Path.Combine(env.ContentRootPath, "Data", "complexmixes.json"));
            var rawComplexMixes = JsonSerializer.Deserialize<List<RawComplexMix>>(complexMixesData, options) ?? new List<RawComplexMix>();
            ComplexMixes = rawComplexMixes.Select(rcm => new ComplexMix
            {
                ComplexEffect = rcm.ComplexEffect,
                BaseEffectIndicator = rcm.BaseEffectIndicator,
                IngredientToAdd = Ingredients.FirstOrDefault(i => i.Id == rcm.IngredientToAdd)
                    ?? throw new Exception($"Ingredient '{rcm.IngredientToAdd}' not found in ingredients.json")
            }).ToList();

            // Load Products and resolve Effect references
            var productsData = File.ReadAllText(Path.Combine(env.ContentRootPath, "Data", "products.json"));
            var rawProducts = JsonSerializer.Deserialize<List<RawProduct>>(productsData, options) ?? new List<RawProduct>();
            Products = rawProducts.Select(rp => new Product
            {
                Id = rp.Id,
                BasePrice = rp.BasePrice,
                Effects = rp.Effects.Select(effectId => Effects.FirstOrDefault(e => e.Id == effectId) 
                    ?? throw new Exception($"Effect '{effectId}' not found in effects.json")).ToList()
            }).ToList();
        }

        private class RawIngredient
        {
            public string Id { get; set; } = default!;
            public int Cost { get; set; }
            public List<string> Effects { get; set; } = new();
        }

        private class RawComplexMix
        {
            public string ComplexEffect { get; set; } = string.Empty;
            public string BaseEffectIndicator { get; set; } = string.Empty;
            public string IngredientToAdd { get; set; } = string.Empty;
        }

        private class RawProduct
        {
            public string Id { get; set; } = default!;
            public double BasePrice { get; set; }
            public List<string> Effects { get; set; } = new();
        }
    }
}
