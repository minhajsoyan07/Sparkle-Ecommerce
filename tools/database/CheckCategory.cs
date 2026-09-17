using System;
using Microsoft.Data.SqlClient;

class Program {
    static void Main() {
        string connectionString = ""Data Source=localhost\\SQLEXPRESS;Initial Catalog=SparkleEcommerce;Integrated Security=True;TrustServerCertificate=True"";
        using (SqlConnection connection = new SqlConnection(connectionString)) {
            connection.Open();
            String sql = ""SELECT Id, Name, Slug, ImageUrl FROM Categories WHERE Slug = 'baby-kids-mom'"";
            using (SqlCommand command = new SqlCommand(sql, connection)) {
                using (SqlDataReader reader = command.ExecuteReader()) {
                    while (reader.Read()) {
                        Console.WriteLine($""{reader[0]} | {reader[1]} | {reader[2]} | ImageUrl: '{reader[3]}' (IsNull: {reader.IsDBNull(3)})"");
                    }
                }
            }
        }
    }
}
