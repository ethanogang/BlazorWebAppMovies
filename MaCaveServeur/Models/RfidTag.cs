namespace MaCaveServeur.Models
{
    public class RfidTag
    {
        public int Id { get; set; }
        public string Epc { get; set; } = string.Empty;

        public Guid BottleId { get; set; }
        public Bottle? Bottle { get; set; }
    }
}
