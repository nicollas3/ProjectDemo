namespace NeuroCare.Domain;

public class Organization : BaseEntity
{
    public string Name { get; set; } = "";
    public string? LegalName { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool Active { get; set; } = true;
}
