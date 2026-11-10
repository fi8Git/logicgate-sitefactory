using Xunit;

namespace LogicGate.SiteFactory.Domain.Tests;

public sealed class IdentifierTests
{
    [Theory]
    [InlineData("acme-services")]
    [InlineData(" Future-Value ")]
    public void IdentifiersHaveCaseSensitiveValueEqualityWithoutNormalization(string value)
    {
        ClientSlug client = new(value);
        ProductId product = new(value);
        TemplateId template = new(value);

        Assert.Equal(value, client.Value);
        Assert.Equal(value, product.Value);
        Assert.Equal(value, template.Value);
        Assert.Equal(value, client.ToString());
        Assert.Equal(value, product.ToString());
        Assert.Equal(value, template.ToString());
        Assert.Equal(client, new ClientSlug(value));
        Assert.Equal(product, new ProductId(value));
        Assert.Equal(template, new TemplateId(value));
        Assert.Equal(client.GetHashCode(), new ClientSlug(value).GetHashCode());
        Assert.Equal(product.GetHashCode(), new ProductId(value).GetHashCode());
        Assert.Equal(template.GetHashCode(), new TemplateId(value).GetHashCode());
        Assert.NotEqual(client, new ClientSlug(value.ToUpperInvariant()));
        Assert.NotEqual(product, new ProductId(value.ToUpperInvariant()));
        Assert.NotEqual(template, new TemplateId(value.ToUpperInvariant()));
        Assert.NotEqual<object>(client, product);
        Assert.NotEqual<object>(product, template);
        Assert.NotEqual<object>(client, template);
    }

    [Fact]
    public void NullIdentifiersAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new ClientSlug(null!));
        Assert.Throws<ArgumentNullException>(() => new ProductId(null!));
        Assert.Throws<ArgumentNullException>(() => new TemplateId(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void BlankIdentifiersAreRejected(string value)
    {
        Assert.Throws<ArgumentException>(() => new ClientSlug(value));
        Assert.Throws<ArgumentException>(() => new ProductId(value));
        Assert.Throws<ArgumentException>(() => new TemplateId(value));
    }
}
