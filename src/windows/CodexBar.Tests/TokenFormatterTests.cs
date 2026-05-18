using CodexBar.Core.Providers.Claude;

namespace CodexBar.Tests;

[TestClass]
public sealed class TokenFormatterTests
{
    [TestMethod]
    public void FormatsSmallNumbersLiterally()
    {
        Assert.AreEqual("0", TokenFormatter.Format(0));
        Assert.AreEqual("1", TokenFormatter.Format(1));
        Assert.AreEqual("850", TokenFormatter.Format(850));
        Assert.AreEqual("999", TokenFormatter.Format(999));
    }

    [TestMethod]
    public void FormatsThousandsWithKSuffix()
    {
        Assert.AreEqual("1K", TokenFormatter.Format(1_000));
        Assert.AreEqual("1K", TokenFormatter.Format(1_049));
        Assert.AreEqual("1.5K", TokenFormatter.Format(1_500));
        Assert.AreEqual("847K", TokenFormatter.Format(847_321));
    }

    [TestMethod]
    public void FormatsMillionsWithMSuffix()
    {
        Assert.AreEqual("1M", TokenFormatter.Format(1_000_000));
        Assert.AreEqual("1.5M", TokenFormatter.Format(1_500_000));
        Assert.AreEqual("1.2M", TokenFormatter.Format(1_234_567));
        Assert.AreEqual("12M", TokenFormatter.Format(12_000_000));
    }

    [TestMethod]
    public void NegativesReturnZero()
    {
        Assert.AreEqual("0", TokenFormatter.Format(-1));
        Assert.AreEqual("0", TokenFormatter.Format(-9999));
    }
}
