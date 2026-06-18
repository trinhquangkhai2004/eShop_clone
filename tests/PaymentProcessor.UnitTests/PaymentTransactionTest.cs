namespace eShop.PaymentProcessor.UnitTests;

[TestClass]
public class PaymentTransactionTest
{
    [TestMethod]
    public void MarkSucceeded_FromPublishedNeedReview_ReopensResultEventPublishing()
    {
        var transaction = new PaymentTransaction(
            orderId: 200,
            userId: "user-200",
            amount: 100,
            currency: "USD",
            paymentMethod: "Simulated",
            idempotencyKey: "order:200:payment");
        transaction.MarkNeedReview();
        transaction.MarkResultPublished();

        transaction.MarkSucceeded("manual-200");

        Assert.AreEqual(PaymentTransactionStatus.Succeeded, transaction.Status);
        Assert.IsFalse(transaction.ResultEventPublished);
    }
}
