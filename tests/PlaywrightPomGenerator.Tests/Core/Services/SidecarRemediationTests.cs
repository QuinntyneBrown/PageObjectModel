using FluentAssertions;
using PlaywrightPomGenerator.Core.Services;

namespace PlaywrightPomGenerator.Tests.Core.Services;

/// <summary>
/// Every sidecar failure category must carry an actionable fix, and the exception
/// must expose the same text so the banner and the hard-error path agree.
/// </summary>
public sealed class SidecarRemediationTests
{
    [Theory]
    [InlineData(SidecarUnavailableReason.NodeMissing, "Node.js")]
    [InlineData(SidecarUnavailableReason.TypeScriptMissing, "npm install")]
    [InlineData(SidecarUnavailableReason.SidecarMissing, "POMGEN_SIDECAR")]
    [InlineData(SidecarUnavailableReason.Timeout, "SidecarTimeoutSeconds")]
    [InlineData(SidecarUnavailableReason.ProtocolError, "--debug")]
    public void For_EveryReason_ShouldNameTheFix(SidecarUnavailableReason reason, string expectedFragment)
    {
        SidecarRemediation.For(reason).Should().Contain(expectedFragment);
        SidecarRemediation.Cause(reason).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void For_ShouldCoverEveryEnumMember()
    {
        foreach (var reason in Enum.GetValues<SidecarUnavailableReason>())
        {
            SidecarRemediation.For(reason).Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Exception_Remediation_ShouldMatchTheReason()
    {
        var ex = new SidecarUnavailableException(SidecarUnavailableReason.TypeScriptMissing, "no typescript");

        ex.Remediation.Should().Be(SidecarRemediation.For(SidecarUnavailableReason.TypeScriptMissing));
    }
}
