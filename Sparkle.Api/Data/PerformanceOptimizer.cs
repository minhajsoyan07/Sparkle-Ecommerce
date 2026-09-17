using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;


namespace Sparkle.Api.Data;

public static class PerformanceOptimizer
{
    public static async Task OptimizeDatabaseAsync(ApplicationDbContext db)
    {
        // Execute raw SQL to create indexes if they don't exist
        // Using Check-Before-Create pattern to prevent EF Core "Fail" logs
        
        // Products Indexes
        await EnsureIndexAsync(db, "[catalog].[Products]", "IX_Products_IsActive", 
            "CREATE INDEX [IX_Products_IsActive] ON [catalog].[Products] ([IsActive])");
            
        await EnsureIndexAsync(db, "[catalog].[Products]", "IX_Products_Price",
            "CREATE INDEX [IX_Products_Price] ON [catalog].[Products] ([BasePrice])");

        await EnsureIndexAsync(db, "[catalog].[Products]", "IX_Products_CreatedAt",
            "CREATE INDEX [IX_Products_CreatedAt] ON [catalog].[Products] ([CreatedAt] DESC)");

        // Orders Indexes
        await EnsureIndexAsync(db, "[orders].[Orders]", "IX_Orders_IsDeleted_Status",
            "CREATE INDEX [IX_Orders_IsDeleted_Status] ON [orders].[Orders] ([IsDeleted], [Status])");

        await EnsureIndexAsync(db, "[orders].[Orders]", "IX_Orders_OrderDate",
            "CREATE INDEX [IX_Orders_OrderDate] ON [orders].[Orders] ([OrderDate] DESC)");

        // Activity Logs
        await EnsureIndexAsync(db, "[system].[ActivityLogs]", "IX_ActivityLogs_Timestamp",
            "CREATE INDEX [IX_ActivityLogs_Timestamp] ON [system].[ActivityLogs] ([Timestamp] DESC)");

        // Foreign Key Indexes (if not already indexed by EF)
        await EnsureIndexAsync(db, "[orders].[CartItems]", "IX_CartItems_CartId",
            "CREATE INDEX [IX_CartItems_CartId] ON [orders].[CartItems] ([CartId])");

        await EnsureIndexAsync(db, "[orders].[WishlistItems]", "IX_WishlistItems_WishlistId",
            "CREATE INDEX [IX_WishlistItems_WishlistId] ON [orders].[WishlistItems] ([WishlistId])");

        await EnsureIndexAsync(db, "[orders].[OrderItems]", "IX_OrderItems_OrderId",
            "CREATE INDEX [IX_OrderItems_OrderId] ON [orders].[OrderItems] ([OrderId])");

        await EnsureIndexAsync(db, "[catalog].[ProductVariants]", "IX_ProductVariants_ProductId",
            "CREATE INDEX [IX_ProductVariants_ProductId] ON [catalog].[ProductVariants] ([ProductId])");

        await EnsureIndexAsync(db, "[catalog].[ProductImages]", "IX_ProductImages_ProductId",
            "CREATE INDEX [IX_ProductImages_ProductId] ON [catalog].[ProductImages] ([ProductId])");

        // Analytics Indexes
        await EnsureIndexAsync(db, "[analytics].[ProductViews]", "IX_ProductViews_ViewedAt",
            "CREATE INDEX [IX_ProductViews_ViewedAt] ON [analytics].[ProductViews] ([ViewedAt] DESC)");
    }

    private static async Task EnsureIndexAsync(ApplicationDbContext db, string tableName, string indexName, string createSql)
    {
        // Wrap command in IF NOT EXISTS execution block to make it idempotent and prevent EF Core failure logs
        // This avoids the race conditions and complexity of checking first in C#
        var sql = $@"
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = '{indexName}' AND object_id = OBJECT_ID('{tableName}'))
            BEGIN
                {createSql};
            END";

        await db.Database.ExecuteSqlRawAsync(sql);
    }
}
