using System.Numerics;
using Schedule_1_Calculator.Models;

namespace Schedule_1_Calculator.Services
{
    public class MixCalculator
    {
        // A mix can hold at most 8 effects at once (game rule).
        private const int MaxEffects = 8;

        DataService _data;

        // Effects are indexed 0..N-1 and a set of effects is represented as a bitmask in a ulong
        // (there are far fewer than 64 effects, so one bit each fits). This turns every hot-path
        // operation — "is this effect present?", "does this reaction fire?", comparing two mixes —
        // into a single machine instruction, and lets a whole effect set be used directly as a
        // dictionary key with no string building. That is what makes the search fast at large
        // ingredient counts.
        private readonly Effect[] _effects;              // index -> Effect
        private readonly Ingredient[] _ingredients;      // index -> Ingredient
        private readonly int[] _ingredientCosts;         // index -> cost
        private readonly ulong[] _ingredientBaseBits;    // index -> bits of the ingredient's own effect(s)
        private readonly Reaction[][] _ingredientReactions; // index -> that ingredient's reactions

        public MixCalculator(DataService dataService)
        {
            _data = dataService;

            _effects = _data.Effects.ToArray();
            if (_effects.Length > 64)
                throw new InvalidOperationException("More than 64 effects cannot be packed into a bitmask.");

            var effectBit = new Dictionary<string, ulong>();
            for (int i = 0; i < _effects.Length; i++)
                effectBit[_effects[i].Id] = 1UL << i;

            var rulesByIngredient = _data.ComplexMixes
                .GroupBy(r => r.IngredientToAdd.Id)
                .ToDictionary(g => g.Key, g => g.ToList());

            _ingredients = _data.Ingredients.ToArray();
            _ingredientCosts = new int[_ingredients.Length];
            _ingredientBaseBits = new ulong[_ingredients.Length];
            _ingredientReactions = new Reaction[_ingredients.Length][];

            for (int i = 0; i < _ingredients.Length; i++)
            {
                var ingredient = _ingredients[i];
                _ingredientCosts[i] = ingredient.Cost;

                ulong baseBits = 0;
                foreach (var effect in ingredient.Effects)
                    baseBits |= effectBit[effect.Id];
                _ingredientBaseBits[i] = baseBits;

                _ingredientReactions[i] = rulesByIngredient.TryGetValue(ingredient.Id, out var rules)
                    ? rules.Select(r => new Reaction(effectBit[r.BaseEffectIndicator], effectBit[r.ComplexEffect])).ToArray()
                    : Array.Empty<Reaction>();
            }
        }

        public (List<Ingredient> bestComboIngredients, double bestProfit, double bestSellPrice, double bestComboCost, List<Effect> bestComboEffects) FindMostProfitableMix(int ingredientCount, Product product)
        {
            if (ingredientCount < 1)
                throw new ArgumentException("At least one ingredient is required", nameof(ingredientCount));

            // Breadth-first search over mix states, adding one ingredient per level.
            //
            // Ingredients may REPEAT and order matters: the same ingredient added at a later
            // point reacts with whatever effects happen to be present at that moment, so it can
            // do something completely different the second time (e.g. a reaction whose indicator
            // effect only appeared after an earlier step). This is exactly why the game's best
            // recipes reuse ingredients, so the search has to allow it.
            //
            // There are 16^N such sequences, but many of them reach the SAME set of effects.
            // Because every future reaction depends only on the current effect set, two paths
            // that reach the same effects are interchangeable from then on — so at each level we
            // keep only the cheapest path to each distinct effect set. That collapses the
            // exponential blow-up down to the (small) number of reachable effect sets.
            //
            // Each level maps an effect-set (bitmask) -> how we got there (cheapest cost, plus a
            // back-pointer to the previous set and the ingredient used). We keep every level so
            // the winning ingredient order can be reconstructed at the end.

            ulong startMask = 0;
            foreach (var effect in product.Effects)
            {
                int idx = Array.FindIndex(_effects, e => e.Id == effect.Id);
                if (idx >= 0) startMask |= 1UL << idx;
            }

            var levels = new List<Dictionary<ulong, Node>>
            {
                new() { [startMask] = new Node(cost: 0, prevMask: 0, ingredientIndex: -1) }
            };

            for (int step = 0; step < ingredientCount; step++)
            {
                var prev = levels[step];
                var next = new Dictionary<ulong, Node>();

                foreach (var (mask, node) in prev)
                {
                    for (int ing = 0; ing < _ingredients.Length; ing++)
                    {
                        ulong newMask = ApplyIngredient(mask, ing);
                        double newCost = node.Cost + _ingredientCosts[ing];

                        // Same effect set reached more cheaply dominates — keep only that path.
                        if (!next.TryGetValue(newMask, out var existing) || newCost < existing.Cost)
                            next[newMask] = new Node(newCost, mask, ing);
                    }
                }

                levels.Add(next);
            }

            // Pick the most profitable effect set reachable in exactly N steps.
            var final = levels[ingredientCount];
            double bestProfit = double.MinValue;
            ulong bestMask = startMask;
            double bestSellPrice = 0;
            double bestComboCost = 0;

            foreach (var (mask, node) in final)
            {
                // Sell price = round(BasePrice * (1 + sum of effect multipliers)).
                // The game rounds the displayed price to the nearest whole dollar, so profit
                // must be computed from that same rounded value.
                double sellPrice = Math.Round(
                    product.BasePrice * (1 + SumEffectValues(mask)),
                    MidpointRounding.AwayFromZero);
                double profit = sellPrice - node.Cost;

                if (profit > bestProfit)
                {
                    bestProfit = profit;
                    bestMask = mask;
                    bestSellPrice = sellPrice;
                    bestComboCost = node.Cost;
                }
            }

            // Walk the back-pointers from the winning set to recover the ingredient order.
            var bestComboIngredients = new List<Ingredient>();
            ulong cursor = bestMask;
            for (int step = ingredientCount; step >= 1; step--)
            {
                var node = levels[step][cursor];
                bestComboIngredients.Add(_ingredients[node.IngredientIndex]);
                cursor = node.PrevMask;
            }
            bestComboIngredients.Reverse();

            return (bestComboIngredients, bestProfit, bestSellPrice, bestComboCost, ToEffects(bestMask));
        }

        // Apply a single ingredient to an effect set (bitmask), reproducing the game's rules:
        //   1. Every reaction for this ingredient is evaluated against a SNAPSHOT of the effects
        //      present before it was added, so reactions apply simultaneously — a freshly
        //      produced effect never triggers another rule in the same addition (no cascading).
        //   2. A reaction fires only if its resulting effect isn't already present; when it
        //      doesn't fire, the indicator effect is left untouched.
        //   3. The ingredient's own base effect is added last, only if it isn't already present
        //      and the mix is still below the 8-effect cap.
        private ulong ApplyIngredient(ulong snapshot, int ingredientIndex)
        {
            ulong result = snapshot;

            foreach (var reaction in _ingredientReactions[ingredientIndex])
            {
                bool indicatorPresent = (snapshot & reaction.IndicatorBit) != 0;
                bool resultBlocked = ((snapshot | result) & reaction.ComplexBit) != 0;
                bool indicatorStillThere = (result & reaction.IndicatorBit) != 0;

                if (indicatorPresent && !resultBlocked && indicatorStillThere)
                    result = (result & ~reaction.IndicatorBit) | reaction.ComplexBit;
            }

            foreach (var baseBit in EnumerateBits(_ingredientBaseBits[ingredientIndex]))
            {
                if ((result & baseBit) != 0) continue;          // already present — no slot used
                if (BitOperations.PopCount(result) >= MaxEffects) break; // no room to add a new effect
                result |= baseBit;
            }

            return result;
        }

        private double SumEffectValues(ulong mask)
        {
            double sum = 0;
            foreach (var bit in EnumerateBits(mask))
                sum += _effects[BitOperations.TrailingZeroCount(bit)].Value;
            return sum;
        }

        private List<Effect> ToEffects(ulong mask)
        {
            var list = new List<Effect>();
            foreach (var bit in EnumerateBits(mask))
                list.Add(_effects[BitOperations.TrailingZeroCount(bit)]);
            return list;
        }

        private static IEnumerable<ulong> EnumerateBits(ulong mask)
        {
            while (mask != 0)
            {
                ulong lowest = mask & (ulong)(-(long)mask); // isolate lowest set bit
                yield return lowest;
                mask &= mask - 1;                           // clear lowest set bit
            }
        }

        // A reaction this ingredient can cause: if IndicatorBit is present, it becomes ComplexBit.
        private readonly struct Reaction
        {
            public readonly ulong IndicatorBit;
            public readonly ulong ComplexBit;
            public Reaction(ulong indicatorBit, ulong complexBit)
            {
                IndicatorBit = indicatorBit;
                ComplexBit = complexBit;
            }
        }

        // The cheapest known way to reach an effect set at a given level, with a back-pointer to
        // the previous set and the ingredient added to get here.
        private readonly struct Node
        {
            public readonly double Cost;
            public readonly ulong PrevMask;
            public readonly int IngredientIndex;
            public Node(double cost, ulong prevMask, int ingredientIndex)
            {
                Cost = cost;
                PrevMask = prevMask;
                IngredientIndex = ingredientIndex;
            }
        }
    }
}
