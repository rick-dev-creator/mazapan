using Mazapan.Cli;

namespace Mazapan.Tests.Cli;

public class PasswordTests
{
    [Fact]
    public void ANewPasswordIsChecked()
    {
        Assert.NotNull(Program.PasswordProblem("", false));
        Assert.NotNull(Program.PasswordProblem("a\nb", false));
        Assert.Null(Program.PasswordProblem("contraseña", false));
        // Typed as an encrypted computer starts: what any console keyboard has.
        Assert.NotNull(Program.PasswordProblem("contraseña", true));
        Assert.Null(Program.PasswordProblem("p4ss word!", true));
    }
}

public class FirmwareTests
{
    [Fact]
    public void FwupdsUpdatesAreRead()
    {
        const string json = """
            {"Devices":[{"Name":"System Firmware","Version":"1.10","Releases":[{"Version":"1.12","Summary":"UEFI update"}]},
                        {"Name":"SSD","Version":"3","Releases":[]}]}
            """;
        var got = Program.ParseFirmware(json);
        Assert.Equal([new Program.Firmware("System Firmware", "1.10", "1.12", "UEFI update")], got);
        Assert.Empty(Program.ParseFirmware("not json"));
    }
}
