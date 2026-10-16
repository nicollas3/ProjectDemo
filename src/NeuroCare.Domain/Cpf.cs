namespace NeuroCare.Domain;

public static class Cpf
{
    public static string Normalize(string? value) =>
        new((value ?? string.Empty).Where(char.IsDigit).ToArray());

    public static bool IsValid(string? value)
    {
        var d = Normalize(value);
        if (d.Length != 11 || d.Distinct().Count() == 1) return false;

        int Check(int length)
        {
            var sum = 0;
            for (var i = 0; i < length; i++) sum += (d[i] - '0') * (length + 1 - i);
            var r = sum * 10 % 11;
            return r == 10 ? 0 : r;
        }

        return Check(9) == d[9] - '0' && Check(10) == d[10] - '0';
    }
}
