using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;
using Sparkle.Domain.Catalog;

class Program {
    static void Main() {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(""Data Source=localhost\\SQLEXPRESS;Initial Catalog=SparkleEcommerce;Integrated Security=True;TrustServerCertificate=True"");
        using (var context = new ApplicationDbContext(optionsBuilder.Options)) {
            var cat = context.Categories.FirstOrDefault(c => c.Slug == ""baby-kids-mom"");
            if (cat != null) {
                cat.ImageUrl = null;
                context.SaveChanges();
                Console.WriteLine(""Fixed Baby Kids Mom ImageUrl!"");
            }
        }
    }
}
