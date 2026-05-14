using CodexBar.Core.Refresh;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexBar.Tests;

[TestClass]
public class AdaptiveBackoffTests
{
    [TestMethod]
    public void ZeroFailures_ReturnsZero()
    {
        Assert.AreEqual(TimeSpan.Zero, AdaptiveBackoff.Delay(0));
    }

    [TestMethod]
    public void Failures_ExponentialWithJitter_Cap()
    {
        var oneFail = AdaptiveBackoff.Delay(1);
        var threeFails = AdaptiveBackoff.Delay(3);
        var manyFails = AdaptiveBackoff.Delay(20);

        Assert.IsTrue(oneFail >= TimeSpan.FromSeconds(15) && oneFail <= TimeSpan.FromSeconds(45));
        Assert.IsTrue(threeFails >= TimeSpan.FromMinutes(1));
        Assert.IsTrue(manyFails <= TimeSpan.FromMinutes(30), "cap at 30 minutes");
    }

    [TestMethod]
    public void RateLimitHint_HonorsRetryAfter()
    {
        var delay = AdaptiveBackoff.Delay(1, retryAfter: TimeSpan.FromSeconds(120));
        Assert.IsTrue(delay >= TimeSpan.FromSeconds(120));
    }
}
