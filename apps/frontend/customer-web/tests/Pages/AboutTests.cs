using Bunit;
using CustomerWeb.Pages;
using FluentAssertions;
using Xunit;

namespace CustomerWeb.Tests.Pages;

public sealed class AboutTests : BunitContext
{
    [Fact]
    public void About_RendersHeading()
    {
        var component = Render<About>();

        component.Find("h1").TextContent.Should().Contain("About This Template");
    }

    [Fact]
    public void About_RendersBackToHomeLink()
    {
        var component = Render<About>();

        component.Find("a[href='/']").Should().NotBeNull();
    }
}
