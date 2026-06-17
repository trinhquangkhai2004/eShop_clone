namespace eShop.PaymentProcessor;

public class PaymentOptions
{
    public bool PaymentSucceeded { get; set; }
    public decimal DefaultAmount { get; set; }
    public string DefaultCurrency { get; set; } = "USD";
    public string DefaultPaymentMethod { get; set; } = "Simulated";
}

