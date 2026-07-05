# Schedule 1 Calculator

A small ASP.NET Core MVC web app that finds the **most profitable ingredient mix** for a product in the game *Schedule 1*. It faithfully simulates the game's mixing rules — effect reactions, the 8‑effect cap, and the sell‑price formula — and searches for the ingredient sequence that maximises profit.

---

## What it does

Pick a **base product** and how many **ingredients** to use, hit **Calculate**, and the app shows the most profitable recipe it can find:

- the exact **order** in which to add the ingredients,
- the resulting **effects** (with each effect's price multiplier),
- the **ingredient cost**, **sell price**, and **profit**.

It's a dark, "toxic‑lab" themed web page — a form with two dropdowns at the top, and a result card below.

Example — the best 4‑ingredient OG Kush mix:

```
Best 4-Ingredient Mix

  Sell Price   $111      Ingredient Cost   $15      Profit   $96

  Mixing Order:  Viagra → Mega Bean → Banana → Cuke

  Effects:  Cyclopean +56%   Glowing +48%   Tropic Thunder +46%
            Thought-Provoking +44%   Energizing +22%
```

### What the effect `%` means

Each effect's percentage is its **price multiplier** (from `effects.json`). They all add together and multiply the product's base price:

```
sellPrice = round( BasePrice × (1 + Σ effect%) )
```

For the mix above: `35 × (1 + 0.56 + 0.48 + 0.46 + 0.44 + 0.22) = 35 × 3.16 = $111`. Effects marked with a `—` (and a red border) are drawback effects with a `0` value — they add no sale value.

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

The repository root *is* the project (single‑project layout):

```
.
├── Program.cs                   # app startup + shared-password gate
├── Schedule 1 Calculator.csproj
├── Schedule 1 Calculator.sln
├── Controllers/
│   └── HomeController.cs        # handles the form (product + count) and runs the search
├── Services/
│   ├── DataService.cs           # loads and resolves the JSON data
│   └── MixCalculator.cs         # the mixing simulation + profit search
├── Models/
│   ├── Product.cs, Ingredient.cs, Effect.cs, ComplexMix.cs
├── ViewModel/
│   └── HomeViewModel.cs         # data passed to the page (form state + best mix)
├── Data/
│   ├── effects.json             # effect ids, abbreviations, price multipliers
│   ├── ingredients.json         # ingredients, costs, base effects
│   ├── complexmixes.json        # reaction rules (indicator + ingredient → new effect)
│   └── products.json            # products, base prices, starting effects
├── Views/                       # Razor views (Home/Index.cshtml, Shared/_Layout.cshtml)
└── wwwroot/css/site.css         # the theme
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
dotnet run
```

Then open <http://localhost:5249/> and use the dropdowns to choose a product and ingredient count, then click **Calculate**.

The form submits a plain `GET` (e.g. `/Home/Mix?product=cocaine&count=6`), so the current selection is reflected in the URL and you can bookmark or share a specific query. `HomeController` validates the product and clamps the count to 1–8.

> Higher ingredient counts search a much larger space. Counts up to ~6 are quick; 7–8 can take a few seconds.

---

## Extending the data

To add ingredients, effects, reactions, or products, edit the JSON files under `Data/`. Ids must match across files (an ingredient's effects and a reaction's effects have to exist in `effects.json`, etc.); `DataService` throws at startup if a reference can't be resolved, so mistakes surface immediately.

> **Note:** the calculator is only as accurate as `complexmixes.json`. If a reaction the game uses is missing from that file, the search can't discover mixes that rely on it.

---

## Disclaimer

This is a companion tool for *Schedule 1*, a satirical video game. It has nothing to do with anything outside the game.
