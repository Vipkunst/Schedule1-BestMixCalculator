namespace Schedule_1_Calculator.Models
{
    public class Ingredient
    {
        public string Id { get; set; } = default!;
        public int Cost { get; set; }
        public List<Effect> Effects { get; set; } = default!;
    }
}
