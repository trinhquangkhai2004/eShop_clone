using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace eShop.PaymentProcessor.UnitTests;

[TestClass]
public class PaymentOptionsValidatorTest
{
    [TestMethod]
    public void Validate_EmptyDefaultCurrency_ReturnsFailure()
    {
        var result = new PaymentOptionsValidator().Validate(
            Options.DefaultName,
            new PaymentOptions
            {
                DefaultCurrency = "",
                DefaultPaymentMethod = "Simulated",
                WebhookTimestampToleranceSeconds = 300
            });

        Assert.IsTrue(result.Failed);
        Assert.Contains(
            string.Join(";", result.Failures),
            "PaymentOptions:DefaultCurrency must not be empty.");
    }

    [TestMethod]
    public void IsSensitiveEndpointEnabled_StagingOverride_ReturnsTrue()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Staging);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                [PaymentEndpointAccess.EnableSensitiveEndpointsKey] = "true"
            })
            .Build();

        var enabled = PaymentEndpointAccess.IsSensitiveEndpointEnabled(environment, configuration);

        Assert.IsTrue(enabled);
    }
}
