using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sparkle.Infrastructure;
using Sparkle.Domain.Catalog;

class Program {
    static void Main() {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(""Data Source=localhost\\SQLEXPRESS;Initial Catalog=SparkleEcommerce;Integrated Security=True;TrustServerCertificate=True"");
        using (var context = new ApplicationDbContext(optionsBuilder.Options)) {
            var cat = context.Categories.FirstOrDefault(c => c.Slug == ""baby-kids-mom"");
            if (cat != null) {
                Console.WriteLine($""ID: {cat.Id}, Name: {cat.Name}, ImageUrl: '{cat.ImageUrl}', IsNull: {cat.ImageUrl == null}"");
            } else {
                Console.WriteLine(""Category not found!"");
            }
        }
    }
}
