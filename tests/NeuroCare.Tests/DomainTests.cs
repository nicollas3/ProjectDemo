using NeuroCare.Domain;

namespace NeuroCare.Tests;

public class CpfTests
{
    [Theory]
    [InlineData("123.456.789-09")]
    [InlineData("11144477735")]
    [InlineData("529.982.247-25")]
    public void Valid_cpf_is_accepted(string cpf) => Assert.True(Cpf.IsValid(cpf));

    [Theory]
    [InlineData("")]
    [InlineData("111.111.111-11")]
    [InlineData("123.456.789-00")]
    [InlineData("123")]
    public void Invalid_cpf_is_rejected(string cpf) => Assert.False(Cpf.IsValid(cpf));

    [Fact]
    public void Masked_cpf_hides_most_digits()
    {
        var p = new Patient { Cpf = "12345678909" };
        Assert.Equal("***.***.789-09", p.MaskedCpf);
    }
}

public class AppointmentDomainTests
{
    [Fact]
    public void Scheduled_can_be_confirmed_then_completed()
    {
        var a = new Appointment();
        a.Confirm();
        a.Complete();
        Assert.Equal(AppointmentStatus.Completed, a.Status);
    }

    [Fact]
    public void Cancelled_appointment_cannot_be_completed()
    {
        var a = new Appointment();
        a.Cancel();
        Assert.Throws<DomainException>(() => a.Complete());
    }

    [Fact]
    public void Completed_appointment_cannot_be_cancelled()
    {
        var a = new Appointment { Status = AppointmentStatus.Completed };
        Assert.Throws<DomainException>(() => a.Cancel());
    }
}
