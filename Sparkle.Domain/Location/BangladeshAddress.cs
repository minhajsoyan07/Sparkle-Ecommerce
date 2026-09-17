namespace Sparkle.Domain.Location;

public class Division
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string NameBn { get; set; }
    public ICollection<District> Districts { get; set; } = [];
}

public class District
{
    public int Id { get; set; }
    public int DivisionId { get; set; }
    public required string Name { get; set; }
    public required string NameBn { get; set; }
    public Division Division { get; set; } = null!;
    public ICollection<Upazila> Upazilas { get; set; } = [];
}

public class Upazila
{
    public int Id { get; set; }
    public int DistrictId { get; set; }
    public required string Name { get; set; }
    public required string NameBn { get; set; }
    public District District { get; set; } = null!;
    public ICollection<Union> Unions { get; set; } = [];
}

public class Union
{
    public int Id { get; set; }
    public int UpazilaId { get; set; }
    public required string Name { get; set; }
    public required string NameBn { get; set; }
    public Upazila Upazila { get; set; } = null!;
}
