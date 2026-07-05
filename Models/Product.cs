namespace Schedule_1_Calculator.Models
{
    public class Product
    {
        public string Id { get; set; } = default!;
        public List<Effect> Effects { get; set; } = new List<Effect>();
        public double BasePrice { get; set; }
    }
}
