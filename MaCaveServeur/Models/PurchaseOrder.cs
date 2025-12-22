// Models/PurchaseOrder.cs
namespace MaCaveServeur.Models;

public enum OrderStatus { Draft, Sent, Received, Cancelled }

public class PurchaseOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Number { get; set; } = "";                 // ex: PO-2025-0001
    public string Site { get; set; } = "";                   // Brutus, Bacchus, ...
    public OrderStatus Status { get; set; } = OrderStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? SentAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public string Supplier { get; set; } = "";               // optionnel : par défaut Producer
    public string Notes { get; set; } = "";
    public List<PurchaseOrderItem> Items { get; set; } = new();
}

public class PurchaseOrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? BottleId { get; set; }                      // si existante dans la cave
    public string Name { get; set; } = "";
    public string Producer { get; set; } = "";
    public string Appellation { get; set; } = "";
    public string Region { get; set; } = "";
    public string Color { get; set; } = "";
    public int Vintage { get; set; }
    public decimal Price { get; set; }                       // prix indicatif
    public int QtyOrdered { get; set; }                      // quantité à commander
}
