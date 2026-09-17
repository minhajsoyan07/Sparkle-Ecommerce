using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sparkle.Infrastructure;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        Console.WriteLine("Using ApplicationDbContextFactory with connection string: Data Source=.\\SQLEXPRESS;Initial Catalog=SparkleEcommerce;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true;Encrypt=False;Command Timeout=180;Connect Timeout=60");
        // This connection string is used only at design-time for migrations.
        optionsBuilder.UseSqlServer("Data Source=.\\SQLEXPRESS;Initial Catalog=SparkleEcommerce;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true;Encrypt=False;Command Timeout=180;Connect Timeout=60");
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
