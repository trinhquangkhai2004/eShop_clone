namespace eShop.PaymentProcessor.Domain;

public enum PaymentTransactionStatus
{
    Pending = 0,
    Processing = 1,
    Succeeded = 2,
    Failed = 3,
    Expired = 4,
    Cancelled = 5,
    NeedReview = 6
}
