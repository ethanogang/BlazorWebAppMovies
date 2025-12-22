namespace MaCaveServeur.Models
{
    public class Location
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public ICollection<Bottle> Bottles { get; set; } = new List<Bottle>();
    }
}
