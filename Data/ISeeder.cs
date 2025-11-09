namespace Data.Seeding
{
    public interface ISeeder
    {
        Task SeedAsync(CancellationToken cancellationToken = default);
    }
}