# Schedule 1 Calculator

A small ASP.NET Core MVC web app that finds the **most profitable ingredient mix** for a product in the game *Schedule 1*. It faithfully simulates the game's mixing rules — effect reactions, the 8‑effect cap, and the sell‑price formula — and searches for the ingredient sequence that maximises profit.

---

## What it does

Given a product (e.g. OG Kush) and a number of ingredients, the calculator returns:

- the exact **order** in which to add the ingredients,
- the resulting **effects**,
- the **total ingredient cost**, and
- the **selling price** and profit.

Example output for the best 4‑ingredient OG Kush mix:

```
Best 4 Ingredient Mix
  viagra → mega_bean → banana → cuke   (mixing order)

  TotalCost:    15
  SellingCost:  111

  Effects: cyclopean, glowing, tropic_thunder, thought_provoking, energizing
```

---

## How the mixing model works

The engine reproduces the game's rules exactly. When an ingredient is added to the current mix:

1. **Reactions apply simultaneously.** Every reaction the ingredient can trigger is evaluated against a *snapshot* of the effects present **before** the ingredient was added. A freshly produced effect never triggers another reaction in the same step (no cascading). For example, adding Cuke to a mix containing both `foggy` and `gingeritis` turns them into `cyclopean` **and** `thought_provoking` at once.
2. **A reaction only fires if its result isn't already present.** If it doesn't fire, the indicator effect is left untouched.
3. **The ingredient's own base effect is added last** — only if it isn't already present and the mix is still below the **8‑effect cap**.

**Sell price** uses the game's formula, rounded to the nearest whole dollar:

```
sellPrice = round( BasePrice × (1 + Σ effectMultipliers) )
profit    = sellPrice − totalIngredientCost
```

### Ingredients can (and should) repeat

The same ingredient added at a *different* point reacts with whatever effects are present at that moment, so it can do something completely different the second time. This is why the game's best recipes reuse ingredients, and the search allows it — e.g. the best 8‑ingredient OG Kush mix uses Banana and Cuke twice each.

---

## The search algorithm

A brute force over every ordered ingredient sequence would be `16^N` combinations — far too many. Instead the calculator runs a **breadth‑first search over mix states**:

- One ingredient is added per level.
- Many different sequences reach the **same set of effects**. Because every future reaction depends only on the current effect set, two paths that reach the same effects are interchangeable from then on — so at each level the search keeps **only the cheapest path to each distinct effect set**.

This collapses the exponential blow‑up down to the (small) number of *reachable* effect sets, while still finding the true optimum.

### Performance

Effect sets are packed into a **64‑bit bitmask** (one bit per effect), so "is this effect present?", "does this reaction fire?", and effect‑set comparison are all single machine instructions, and a whole effect set is used directly as a dictionary key with no string building. Ingredient paths are stored as back‑pointers rather than copied lists.

| Ingredients | Result       | Time (incl. cold start) |
|-------------|--------------|--------------------------|
| 4           | $111 profit  | instant                  |
| 8           | $165 profit  | ~7 s                     |

---

## Project layout

```
Schedule 1 Calculator/
├── Controllers/
│   └── HomeController.cs        # entry point — runs a sample query on Index
├── Services/
│   ├── DataService.cs           # loads and resolves the JSON data
│   └── MixCalculator.cs         # the mixing simulation + profit search
├── Models/
│   ├── Product.cs, Ingredient.cs, Effect.cs, ComplexMix.cs
├── Data/
│   ├── effects.json             # effect ids, abbreviations, price multipliers
│   ├── ingredients.json         # ingredients, costs, base effects
│   ├── complexmixes.json        # reaction rules (indicator + ingredient → new effect)
│   └── products.json            # products, base prices, starting effects
└── Views/                       # Razor views
```

### Data model

- **Effect** — an id, short abbreviation, and price `value` (multiplier).
- **Ingredient** — an id, `cost`, and the base effect(s) it contributes.
- **ComplexMix** — a reaction rule: *if* `BaseEffectIndicator` is present *and* you add `IngredientToAdd`, it becomes `ComplexEffect`.
- **Product** — an id, `BasePrice`, and starting effect(s).

All four JSON files are loaded once at startup by `DataService` and cross‑referenced by id.

---

## Running it

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project "Schedule 1 Calculator/Schedule 1 Calculator.csproj"
```

Then open <http://localhost:5249/>.

The home page runs a sample query defined in `HomeController.Index` (best 4‑ingredient mix for OG Kush). Change the product or ingredient count there:

```csharp
Product product = _data.Products.Single(p => p.Id == "og_kush");
var bestMix = _mixer.FindMostProfitableMix(4, product);
```

---

## Extending the data

To add ingredients, effects, reactions, or products, edit the JSON files under `Data/`. Ids must match across files (an ingredient's effects and a reaction's effects have to exist in `effects.json`, etc.); `DataService` throws at startup if a reference can't be resolved, so mistakes surface immediately.

> **Note:** the calculator is only as accurate as `complexmixes.json`. If a reaction the game uses is missing from that file, the search can't discover mixes that rely on it.

---

## Disclaimer

This is a companion tool for *Schedule 1*, a satirical video game. It has nothing to do with anything outside the game.
