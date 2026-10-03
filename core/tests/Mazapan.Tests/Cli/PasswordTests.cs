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
