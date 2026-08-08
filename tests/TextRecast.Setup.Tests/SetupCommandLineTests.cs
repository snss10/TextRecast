namespace TextRecast.Setup.Tests;

[TestClass]
public sealed class SetupCommandLineTests
{
    [TestMethod]
    public void NoArgumentsSelectsInteractiveSetup()
    {
        var options = SetupCommandLine.Parse([]);

        Assert.AreEqual(SetupOperation.Interactive, options.Operation);
        Assert.IsFalse(options.Quiet);
        Assert.IsNull(options.InstallDirectory);
    }

    [TestMethod]
    public void QuietInstallPreservesExplicitDirectory()
    {
        var options = SetupCommandLine.Parse(
            ["--install", "--quiet", "--install-directory", @"C:\Users\tester\AppData\Local\Programs\TextRecast"]);

        Assert.AreEqual(SetupOperation.Install, options.Operation);
        Assert.IsTrue(options.Quiet);
        Assert.AreEqual(
            @"C:\Users\tester\AppData\Local\Programs\TextRecast",
            options.InstallDirectory);
    }

    [TestMethod]
    public void ConflictingOperationsAreRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            SetupCommandLine.Parse(["--install", "--uninstall", "--quiet"]));
    }

    [TestMethod]
    public void OptionsWithoutAnOperationAreRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            SetupCommandLine.Parse(["--quiet"]));
    }

    [TestMethod]
    public void QuietIntentIsDetectedBeforeMalformedArgumentsAreParsed()
    {
        var arguments = new[] { "--quiet", "--instal" };

        Assert.IsTrue(SetupCommandLine.IsQuietRequested(arguments));
        Assert.ThrowsExactly<ArgumentException>(() =>
            SetupCommandLine.Parse(arguments));
    }
}
