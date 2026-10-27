using System.Configuration;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using Text_Grab;
using Text_Grab.Properties;
using Text_Grab.Utilities;

namespace Tests;

public class RenderingUtilitiesTests
{
    [Fact]
    public void TestHost_UsesAutomaticRenderingPolicy()
    {
        RenderMode expected = RenderingUtilities.ShouldUseSoftwareRendering(RenderingMode.Auto, RuntimeInformation.OSArchitecture)
            ? RenderMode.SoftwareOnly
            : RenderMode.Default;

        Assert.Equal(expected, RenderOptions.ProcessRenderMode);
    }

    [Theory]
    [InlineData(Architecture.Arm64, true)]
    [InlineData(Architecture.X64, false)]
    [InlineData(Architecture.X86, false)]
    public void Auto_UsesSoftwareRenderingOnlyOnArm64(Architecture deviceArchitecture, bool expected)
    {
        Assert.Equal(expected, RenderingUtilities.ShouldUseSoftwareRendering(RenderingMode.Auto, deviceArchitecture));
    }

    [Theory]
    [InlineData(Architecture.Arm64)]
    [InlineData(Architecture.X64)]
    [InlineData(Architecture.X86)]
    public void Software_AlwaysUsesSoftwareRendering(Architecture deviceArchitecture)
    {
        Assert.True(RenderingUtilities.ShouldUseSoftwareRendering(RenderingMode.Software, deviceArchitecture));
    }

    [Theory]
    [InlineData(Architecture.Arm64)]
    [InlineData(Architecture.X64)]
    [InlineData(Architecture.X86)]
    public void Hardware_NeverUsesSoftwareRendering(Architecture deviceArchitecture)
    {
        Assert.False(RenderingUtilities.ShouldUseSoftwareRendering(RenderingMode.Hardware, deviceArchitecture));
    }

    [Theory]
    [InlineData("Auto", RenderingMode.Auto)]
    [InlineData("Hardware", RenderingMode.Hardware)]
    [InlineData("Software", RenderingMode.Software)]
    [InlineData("software", RenderingMode.Software)]
    public void ParseRenderingMode_ReadsKnownValues(string value, RenderingMode expected)
    {
        Assert.Equal(expected, RenderingUtilities.ParseRenderingMode(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Gpu")]
    [InlineData("7")]
    [InlineData("-1")]
    public void ParseRenderingMode_FallsBackToAutoForMissingOrInvalidValues(string? value)
    {
        Assert.Equal(RenderingMode.Auto, RenderingUtilities.ParseRenderingMode(value));
    }

    [Fact]
    public void RenderingModeSetting_DefaultsToAuto()
    {
        PropertyInfo property = typeof(Settings).GetProperty(nameof(Settings.RenderingMode))!;
        DefaultSettingValueAttribute defaultValue = property.GetCustomAttribute<DefaultSettingValueAttribute>()!;

        Assert.Equal(RenderingMode.Auto.ToString(), defaultValue.Value);
    }
}
