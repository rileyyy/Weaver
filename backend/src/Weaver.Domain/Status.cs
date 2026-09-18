namespace Weaver.Domain;

public enum StatusCategory
{
    ToDo,
    Doing,
    Done,
}

public class Status
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public int Order { get; set; }

    public StatusCategory Category { get; set; }
}
