using Bogus.DataSets;

namespace OutboxWorker.Benchmark;

public class User
{
    public Guid Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string FullName { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public string Avatar { get; set; }
    public Guid CartId { get; set; }
    public Name.Gender Gender { get; set; }
}