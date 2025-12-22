// Services/TransferResult.cs
namespace MaCaveServeur.Services
{
    public class TransferResult
    {
        public Guid BottleId { get; set; }
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public int RequestedQty { get; set; }
        public int MovedQty { get; set; }
        public int FromRemaining { get; set; }
        public int ToNewQty { get; set; }
        public bool DestinationCreated { get; set; }
    }
}
