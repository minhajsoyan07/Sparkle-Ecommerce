using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Sparkle.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace Sparkle.Api.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            
            // 1. Database Creation & Schema Initialization
            // We rely entirely on EF Core's EnsureCreatedAsync to handle:
            // - Database creation (if missing)
            // - Schema generation
            // - Seeding (via OnModelCreating)
            // This avoids race conditions and manual connection errors (Error 4060).

            // 2. Apply Migrations (silent merge with existing schema)
            using (var scope = serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                try
                {
                    // FORCE RESET via Master logic removed.
                    // The database is now clean.
                    
                    await context.Database.EnsureCreatedAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Database migration error.");
                    throw;
                }
            }

            // 4. Check Schema Version - Skip repairs if already successfully run
            var currentVersion = await GetCurrentSchemaVersion(serviceProvider);
            if (currentVersion < 1.8)
            {
                logger.LogInformation("Applying database schema repairs and updates (Version 1.8)...");
                
                // 4. Update Database Schema (Admin Wallets & Indexes)
                await UpdateDatabaseSchemaAsync(serviceProvider);

                // 5. Emergency Schema Repairs (Consolidated from Program.cs)
                await ExecuteSchemaRepairsAsync(serviceProvider);
                
                // 6. Update Stored Procedures
                await UpdateStoredProceduresAsync(serviceProvider);
                
                // 7. Initialize Homepage Content Schema
                await InitializeHomepageContentSchemaAsync(serviceProvider);

                await SetSchemaVersion(serviceProvider, 1.8);
                logger.LogInformation("Database schema version updated to 1.8");
            }
            else
            {
                logger.LogInformation("Database schema is up to date (Version {Version}). Skipping repairs.", currentVersion);
            }
        }

        private static async Task<double> GetCurrentSchemaVersion(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var db = context.Database;

            try
            {
                var sql = @"
                    IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'system') EXEC('CREATE SCHEMA [system]');
                    IF OBJECT_ID('[system].[InitializeLog]', 'U') IS NULL
                    BEGIN
                        CREATE TABLE [system].[InitializeLog] (
                            [Id] INT IDENTITY(1,1) PRIMARY KEY,
                            [Version] FLOAT NOT NULL,
                            [AppliedAt] DATETIME2 DEFAULT GETUTCDATE()
                        );
                        SELECT CAST(0 AS FLOAT);
                    END
                    ELSE
                    BEGIN
                        SELECT TOP 1 [Version] FROM [system].[InitializeLog] ORDER BY [Id] DESC;
                    END";
                
                var connection = db.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
                
                using var cmd = connection.CreateCommand();
                cmd.CommandText = sql;
                var result = await cmd.ExecuteScalarAsync();
                return result == null ? 0.0 : Convert.ToDouble(result);
            }
            catch
            {
                return 0.0;
            }
        }

        private static async Task SetSchemaVersion(IServiceProvider serviceProvider, double version)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO [system].[InitializeLog] ([Version]) VALUES ({0})", version);
        }

        private static async Task ExecuteSchemaRepairsAsync(IServiceProvider serviceProvider)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                var db = context.Database;

                async Task SafeExecuteSql(string sql, string description)
                {
                    try
                    {
                        await db.ExecuteSqlRawAsync(sql);
                    }
                    catch (Exception ex)
                    {
                        // Log but allow startup to continue for non-critical schema drifts
                        logger.LogWarning(ex, $"Schema repair failed for {description}. Continuing...");
                    }
                }

                try
                {
                    // Consolidated Schema Repair Batch (Efficiently checking multiple columns/tables in one trip)
                    await SafeExecuteSql(@"
                        -- Shipments repair
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[shipping].[Shipments]') AND name = 'NumberOfBoxes')
                            ALTER TABLE [shipping].[Shipments] ADD [NumberOfBoxes] int NOT NULL DEFAULT 1;
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[shipping].[Shipments]') AND name = 'PackageType')
                            ALTER TABLE [shipping].[Shipments] ADD [PackageType] nvarchar(max) NULL;
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[shipping].[Shipments]') AND name = 'ShippedAt')
                            ALTER TABLE [shipping].[Shipments] ADD [ShippedAt] datetime2 NULL;

                        -- AspNetUsers repair
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[AspNetUsers]') AND name = 'Gender')
                            ALTER TABLE [AspNetUsers] ADD [Gender] nvarchar(max) NULL;

                        -- Product repair
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[catalog].[Products]') AND name = 'ViewCount')
                            ALTER TABLE [catalog].[Products] ADD [ViewCount] int NOT NULL DEFAULT 0;
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[catalog].[Products]') AND name = 'PurchaseCount')
                            ALTER TABLE [catalog].[Products] ADD [PurchaseCount] int NOT NULL DEFAULT 0;

                        -- Seller repair
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[sellers].[Sellers]') AND name = 'ApprovedBy') ALTER TABLE [sellers].[Sellers] ADD [ApprovedBy] nvarchar(max) NULL;
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[sellers].[Sellers]') AND name = 'Email') ALTER TABLE [sellers].[Sellers] ADD [Email] nvarchar(max) NULL;
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[sellers].[Sellers]') AND name = 'IsNidVerified') ALTER TABLE [sellers].[Sellers] ADD [IsNidVerified] bit NOT NULL DEFAULT 0;
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[sellers].[Sellers]') AND name = 'Latitude') ALTER TABLE [sellers].[Sellers] ADD [Latitude] float NULL;
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[sellers].[Sellers]') AND name = 'Longitude') ALTER TABLE [sellers].[Sellers] ADD [Longitude] float NULL;
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[sellers].[Sellers]') AND name = 'LogoUrl') ALTER TABLE [sellers].[Sellers] ADD [LogoUrl] nvarchar(max) NULL;
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[sellers].[Sellers]') AND name = 'NidNumber') ALTER TABLE [sellers].[Sellers] ADD [NidNumber] nvarchar(max) NULL;
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[sellers].[Sellers]') AND name = 'NidFrontImageUrl') ALTER TABLE [sellers].[Sellers] ADD [NidFrontImageUrl] nvarchar(max) NULL;
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[sellers].[Sellers]') AND name = 'NidBackImageUrl') ALTER TABLE [sellers].[Sellers] ADD [NidBackImageUrl] nvarchar(max) NULL;
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[sellers].[Sellers]') AND name = 'RejectionReason') ALTER TABLE [sellers].[Sellers] ADD [RejectionReason] nvarchar(max) NULL;

                        -- Review system
                        IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'reviews') EXEC('CREATE SCHEMA reviews');
                        IF COL_LENGTH('reviews.ProductReviews', 'AdminNote') IS NULL ALTER TABLE [reviews].[ProductReviews] ADD [AdminNote] nvarchar(max) NULL;
                        IF COL_LENGTH('reviews.ProductReviews', 'EditCount') IS NULL ALTER TABLE [reviews].[ProductReviews] ADD [EditCount] int NOT NULL DEFAULT 0;
                        IF COL_LENGTH('reviews.ProductReviews', 'IsAdminNoteVisible') IS NULL ALTER TABLE [reviews].[ProductReviews] ADD [IsAdminNoteVisible] bit NOT NULL DEFAULT 1;
                        IF COL_LENGTH('reviews.ProductReviews', 'IsLocked') IS NULL ALTER TABLE [reviews].[ProductReviews] ADD [IsLocked] bit NOT NULL DEFAULT 0;
                        IF COL_LENGTH('reviews.ProductReviews', 'IsPinned') IS NULL ALTER TABLE [reviews].[ProductReviews] ADD [IsPinned] bit NOT NULL DEFAULT 0;
                        IF COL_LENGTH('reviews.ProductReviews', 'LastEditedAt') IS NULL ALTER TABLE [reviews].[ProductReviews] ADD [LastEditedAt] datetime2 NULL;
                    ", "Consolidated Schema Repair Batch");

                    // Product Quality Issues table
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[reviews].[ProductQualityIssues]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [reviews].[ProductQualityIssues] (
                                [Id] int NOT NULL IDENTITY(1,1),
                                [ProductId] int NOT NULL,
                                [Type] int NOT NULL,
                                [Severity] int NOT NULL,
                                [Status] int NOT NULL,
                                [CurrentRating] decimal(18,2) NOT NULL,
                                [TotalReviews] int NOT NULL,
                                [LowRatingCount] int NOT NULL,
                                [ReportCount] int NOT NULL,
                                [RejectedReviewCount] int NOT NULL,
                                [AutoSuspended] bit NOT NULL,
                                [SellerNotified] bit NOT NULL,
                                [DetectedAt] datetime2 NOT NULL,
                                [ReviewedAt] datetime2 NULL,
                                [ResolvedAt] datetime2 NULL,
                                [AdminNotes] nvarchar(max) NULL,
                                [Resolution] nvarchar(max) NULL,
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UpdatedAt] datetime2 NULL,
                                [CreatedBy] nvarchar(max) NULL,
                                [UpdatedBy] nvarchar(max) NULL,
                                [IsDeleted] bit NOT NULL DEFAULT 0,
                                [DeletedAt] datetime2 NULL,
                                CONSTRAINT [PK_ProductQualityIssues] PRIMARY KEY ([Id]),
                                CONSTRAINT [FK_ProductQualityIssues_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [catalog].[Products] ([Id]) ON DELETE CASCADE
                            );
                            CREATE INDEX [IX_ProductQualityIssues_ProductId] ON [reviews].[ProductQualityIssues] ([ProductId]);
                            CREATE INDEX [IX_ProductQualityIssues_Status_Severity] ON [reviews].[ProductQualityIssues] ([Status], [Severity]);
                        END", "Table.ProductQualityIssues");

                    // Chat system repair
                    await SafeExecuteSql(@"IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'support') EXEC('CREATE SCHEMA support');", "Schema.Support");

                    // ==================== WALLET TRANSACTIONS REPAIR ====================
                    // Fix Column Names (Old -> New)
                    await SafeExecuteSql(@"
                        IF EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[wallets].[WalletTransactions]') AND name = 'Type')
                        AND NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[wallets].[WalletTransactions]') AND name = 'TransactionType')
                        BEGIN
                            EXEC sp_rename 'wallets.WalletTransactions.Type', 'TransactionType', 'COLUMN';
                        END", "WalletTransactions.RenameType");

                    await SafeExecuteSql(@"
                        IF EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[wallets].[WalletTransactions]') AND name = 'BalanceAfterTransaction')
                        AND NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[wallets].[WalletTransactions]') AND name = 'BalanceAfter')
                        BEGIN
                            EXEC sp_rename 'wallets.WalletTransactions.BalanceAfterTransaction', 'BalanceAfter', 'COLUMN';
                        END", "WalletTransactions.RenameBalanceAfter");

                    // Add Missing Columns
                    await SafeExecuteSql(@"
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[wallets].[WalletTransactions]') AND name = 'BalanceBefore')
                            ALTER TABLE [wallets].[WalletTransactions] ADD [BalanceBefore] decimal(18,2) NOT NULL DEFAULT 0;", "WalletTransactions.BalanceBefore");

                    await SafeExecuteSql(@"
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[wallets].[WalletTransactions]') AND name = 'Source')
                            ALTER TABLE [wallets].[WalletTransactions] ADD [Source] nvarchar(max) NOT NULL DEFAULT N''; ", "WalletTransactions.Source");

                    await SafeExecuteSql(@"
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[wallets].[WalletTransactions]') AND name = 'UserId')
                            ALTER TABLE [wallets].[WalletTransactions] ADD [UserId] nvarchar(450) NULL;", "WalletTransactions.UserId");

                    await SafeExecuteSql(@"
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[wallets].[WalletTransactions]') AND name = 'SellerWalletId')
                            ALTER TABLE [wallets].[WalletTransactions] ADD [SellerWalletId] int NULL;", "WalletTransactions.SellerWalletId");

                    await SafeExecuteSql(@"
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[wallets].[WalletTransactions]') AND name = 'UserWalletId')
                            ALTER TABLE [wallets].[WalletTransactions] ADD [UserWalletId] int NULL;", "WalletTransactions.UserWalletId");

                    // Make SellerId Nullable if needed (Entity says int?, but SQL said int NOT NULL)
                    // We'll leave it for now as strictness is okay for pure seller transactions, but for Users it might be null?
                    // Entity: public int? SellerId { get; set; }
                    await SafeExecuteSql(@"
                         IF EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[wallets].[WalletTransactions]') AND name = 'SellerId' AND is_nullable = 0)
                             ALTER TABLE [wallets].[WalletTransactions] ALTER COLUMN [SellerId] int NULL;", "WalletTransactions.SellerIdNullable");

                    // Fix Legacy WalletId checking
                    await SafeExecuteSql(@"
                        IF EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[wallets].[WalletTransactions]') AND name = 'WalletId' AND is_nullable = 0)
                        BEGIN
                            ALTER TABLE [wallets].[WalletTransactions] ALTER COLUMN [WalletId] int NULL;
                        END", "WalletTransactions.WalletIdNullable");

                    // Fix AdminWallet Columns
                    await SafeExecuteSql(@"
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[wallets].[AdminWallets]') AND name = 'TotalRefunded')
                            ALTER TABLE [wallets].[AdminWallets] ADD [TotalRefunded] decimal(18,2) NOT NULL DEFAULT 0;", "AdminWallet.TotalRefunded");
                    
                    await SafeExecuteSql(@"
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[wallets].[AdminWallets]') AND name = 'TotalPayoutsToSellers')
                            ALTER TABLE [wallets].[AdminWallets] ADD [TotalPayoutsToSellers] decimal(18,2) NOT NULL DEFAULT 0;", "AdminWallet.TotalPayoutsToSellers");



                    // Commission Configs repair
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[system].[CommissionConfigs]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [system].[CommissionConfigs] (
                                [Id] int NOT NULL IDENTITY,
                                [CommissionPercentage] decimal(18,2) NOT NULL,
                                [AffectedRole] nvarchar(max) NOT NULL DEFAULT N'',
                                [Category] nvarchar(max) NOT NULL DEFAULT N'',
                                [Description] nvarchar(max) NOT NULL DEFAULT N'',
                                [IsActive] bit NOT NULL DEFAULT 1,
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UpdatedAt] datetime2 NULL,
                                [CreatedBy] nvarchar(max) NULL,
                                [UpdatedBy] nvarchar(max) NULL,
                                [IsDeleted] bit NOT NULL DEFAULT 0,
                                [DeletedAt] datetime2 NULL,
                                CONSTRAINT [PK_CommissionConfigs] PRIMARY KEY ([Id])
                            );
                            PRINT 'Created CommissionConfigs table';
                        END", "Table.CommissionConfigs");

                    // Create Chats table
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[support].[Chats]') AND type in (N'U'))
                            CREATE TABLE [support].[Chats] (
                                [Id] int NOT NULL IDENTITY(1,1),
                                [UserId] nvarchar(450) NOT NULL,
                                [SellerId] int NOT NULL,
                                [ProductId] int NULL,
                                [Subject] nvarchar(max) NULL,
                                [Status] nvarchar(50) NOT NULL DEFAULT 'Active',
                                [StartedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [LastMessageAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UserUnreadCount] int NOT NULL DEFAULT 0,
                                [SellerUnreadCount] int NOT NULL DEFAULT 0,
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UpdatedAt] datetime2 NULL,
                                [CreatedBy] nvarchar(max) NULL,
                                [UpdatedBy] nvarchar(max) NULL,
                                [IsDeleted] bit NOT NULL DEFAULT 0,
                                [DeletedAt] datetime2 NULL,
                                CONSTRAINT [PK_Chats] PRIMARY KEY ([Id]),
                                CONSTRAINT [FK_Chats_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]),
                                CONSTRAINT [FK_Chats_Sellers_SellerId] FOREIGN KEY ([SellerId]) REFERENCES [sellers].[Sellers] ([Id])
                            );", "Table.Chats");
                    
                    // Create Chats indexes
                    await SafeExecuteSql(@"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Chats_UserId_SellerId' AND object_id = OBJECT_ID('[support].[Chats]')) CREATE INDEX [IX_Chats_UserId_SellerId] ON [support].[Chats] ([UserId], [SellerId]);", "Index.Chats.UserSeller");
                    await SafeExecuteSql(@"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Chats_LastMessageAt' AND object_id = OBJECT_ID('[support].[Chats]')) CREATE INDEX [IX_Chats_LastMessageAt] ON [support].[Chats] ([LastMessageAt]);", "Index.Chats.LastMessage");

                    // Create ChatMessages table  
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[support].[ChatMessages]') AND type in (N'U'))
                            CREATE TABLE [support].[ChatMessages] (
                                [Id] int NOT NULL IDENTITY(1,1),
                                [ChatId] int NOT NULL,
                                [SenderId] nvarchar(450) NOT NULL,
                                [IsSeller] bit NOT NULL,
                                [Content] nvarchar(max) NOT NULL,
                                [MessageType] nvarchar(50) NOT NULL DEFAULT 'Text',
                                [AttachmentUrl] nvarchar(max) NULL,
                                [AttachmentName] nvarchar(max) NULL,
                                [SentAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [ReadAt] datetime2 NULL,
                                [IsRead] bit NOT NULL DEFAULT 0,
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UpdatedAt] datetime2 NULL,
                                [CreatedBy] nvarchar(max) NULL,
                                [UpdatedBy] nvarchar(max) NULL,
                                [IsDeleted] bit NOT NULL DEFAULT 0,
                                [DeletedAt] datetime2 NULL,
                                CONSTRAINT [PK_ChatMessages] PRIMARY KEY ([Id]),
                                CONSTRAINT [FK_ChatMessages_Chats_ChatId] FOREIGN KEY ([ChatId]) REFERENCES [support].[Chats] ([Id]) ON DELETE CASCADE,
                                CONSTRAINT [FK_ChatMessages_AspNetUsers_SenderId] FOREIGN KEY ([SenderId]) REFERENCES [AspNetUsers] ([Id])
                            );", "Table.ChatMessages");
                    
                    // ChatMessage Indexes
                    await SafeExecuteSql(@"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ChatMessages_ChatId' AND object_id = OBJECT_ID('[support].[ChatMessages]')) CREATE INDEX [IX_ChatMessages_ChatId] ON [support].[ChatMessages] ([ChatId]);", "Index.ChatMessages.ChatId");
                    await SafeExecuteSql(@"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ChatMessages_SentAt' AND object_id = OBJECT_ID('[support].[ChatMessages]')) CREATE INDEX [IX_ChatMessages_SentAt] ON [support].[ChatMessages] ([SentAt]);", "Index.ChatMessages.SentAt");
                    await SafeExecuteSql(@"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ChatMessages_SenderId' AND object_id = OBJECT_ID('[support].[ChatMessages]')) CREATE INDEX [IX_ChatMessages_SenderId] ON [support].[ChatMessages] ([SenderId]);", "Index.ChatMessages.SenderId");

                    // Chat Message Editing Columns
                    await SafeExecuteSql(@"IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[support].[ChatMessages]') AND name = 'IsEdited') ALTER TABLE [support].[ChatMessages] ADD [IsEdited] bit NOT NULL DEFAULT 0;", "ChatMessages.IsEdited");
                    await SafeExecuteSql(@"IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[support].[ChatMessages]') AND name = 'EditedAt') ALTER TABLE [support].[ChatMessages] ADD [EditedAt] datetime2 NULL;", "ChatMessages.EditedAt");

                    // Cart Coupon Schema Update
                    await SafeExecuteSql(@"IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[orders].[Carts]') AND name = 'CouponCode') ALTER TABLE [orders].[Carts] ADD [CouponCode] nvarchar(50) NULL;", "Carts.CouponCode");
                    await SafeExecuteSql(@"IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[orders].[Carts]') AND name = 'DiscountAmount') ALTER TABLE [orders].[Carts] ADD [DiscountAmount] decimal(18,2) NOT NULL DEFAULT 0;", "Carts.DiscountAmount");

                    // Order Coupon Schema Update
                    await SafeExecuteSql(@"IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[orders].[Orders]') AND name = 'CouponCode') ALTER TABLE [orders].[Orders] ADD [CouponCode] nvarchar(50) NULL;", "Orders.CouponCode");

                    // Parent Order Number Schema Update
                    await SafeExecuteSql(@"IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[orders].[Orders]') AND name = 'ParentOrderNumber') ALTER TABLE [orders].[Orders] ADD [ParentOrderNumber] nvarchar(max) NULL;", "Orders.ParentOrderNumber");

                    // Payment Initiated At Schema Update (for auto-cancellation of online payment orders)
                    await SafeExecuteSql(@"IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[orders].[Orders]') AND name = 'PaymentInitiatedAt') ALTER TABLE [orders].[Orders] ADD [PaymentInitiatedAt] datetime2 NULL;", "Orders.PaymentInitiatedAt");

                    // Optimizations
                    await SafeExecuteSql(@"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Products_DiscountPercent' AND object_id = OBJECT_ID('[catalog].[Products]')) CREATE INDEX [IX_Products_DiscountPercent] ON [catalog].[Products] ([DiscountPercent] DESC);", "Index.Products.Discount");
                    await SafeExecuteSql(@"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Products_Performance' AND object_id = OBJECT_ID('[catalog].[Products]')) CREATE INDEX [IX_Products_Performance] ON [catalog].[Products] ([TotalReviews] DESC, [AverageRating] DESC);", "Index.Products.Performance");
                    
                    // NEW: Critical Performance Index for Homepage Trending Products (Fixes 25s slow query)
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Products_Active_Performance' AND object_id = OBJECT_ID('[catalog].[Products]')) 
                        BEGIN
                            CREATE INDEX [IX_Products_Active_Performance] ON [catalog].[Products] ([IsActive], [TotalReviews] DESC, [AverageRating] DESC) INCLUDE ([SellerId]);
                        END", "Index.Products.ActivePerformance");

                    await SafeExecuteSql(@"IF EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[marketing].[Banners]') AND name = 'Position' AND max_length = -1) ALTER TABLE [marketing].[Banners] ALTER COLUMN [Position] nvarchar(100) NULL;", "Banners.Position");
                    await SafeExecuteSql(@"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Banners_Position_IsActive' AND object_id = OBJECT_ID('[marketing].[Banners]')) CREATE INDEX [IX_Banners_Position_IsActive] ON [marketing].[Banners] ([Position], [IsActive]);", "Index.Banners.Position");

                    await SafeExecuteSql(@"ALTER TABLE [orders].[Orders] ALTER COLUMN [ShippingAddressId] int NULL;", "Orders.ShippingAddressIdNullable");

                    // Create Analytics Views
                    await SafeExecuteSql(@"IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'analytics') EXEC('CREATE SCHEMA analytics');", "Schema.Analytics");
                    await SafeExecuteSql(@"IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'sales') EXEC('CREATE SCHEMA sales');", "Schema.Sales");
                    await SafeExecuteSql(@"IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'users') EXEC('CREATE SCHEMA users');", "Schema.Users");
                    
                    await SafeExecuteSql(@"
                        CREATE OR ALTER VIEW [analytics].[vw_ProductPerformance] AS
                        SELECT 
                            p.Id AS ProductId,
                            p.Title,
                            p.BasePrice,
                            p.DiscountPercent,
                            c.Name AS CategoryName,
                            s.ShopName AS SellerName,
                            p.ViewCount,
                            p.PurchaseCount,
                            p.AverageRating,
                            p.TotalReviews,
                            (SELECT COALESCE(SUM(Stock),0) FROM [catalog].[ProductVariants] WHERE ProductId = p.Id) AS TotalStock,
                            CAST((p.PurchaseCount * p.BasePrice) AS DECIMAL(18,2)) AS EstimatedRevenue,
                            CASE WHEN p.IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS Status
                        FROM [catalog].[Products] p
                        JOIN [catalog].[Categories] c ON p.CategoryId = c.Id
                        JOIN [sellers].[Sellers] s ON p.SellerId = s.Id;", "View.ProductPerformance");

                    // User Panel Indexes
                    // Fix UserId column types to be indexable (nvarchar(max) -> nvarchar(450))
                    await SafeExecuteSql(@"IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[orders].[Wishlists]') AND name = 'UserId' AND max_length = -1) 
                                           ALTER TABLE [orders].[Wishlists] ALTER COLUMN [UserId] nvarchar(450) NOT NULL;", "Wishlists.UserId Type Fix");

                    await SafeExecuteSql(@"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Wishlists_UserId' AND object_id = OBJECT_ID('[orders].[Wishlists]')) CREATE INDEX [IX_Wishlists_UserId] ON [orders].[Wishlists] ([UserId]);", "Index.Wishlists.UserId");
                    await SafeExecuteSql(@"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ProductReviews_UserId' AND object_id = OBJECT_ID('[reviews].[ProductReviews]')) CREATE INDEX [IX_ProductReviews_UserId] ON [reviews].[ProductReviews] ([UserId]);", "Index.Reviews.UserId");

                    // Create RecentlyViewedItems Table (Missing Table Fix)
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[users].[RecentlyViewedItems]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [users].[RecentlyViewedItems] (
                                [Id] int NOT NULL IDENTITY(1,1),
                                [UserId] nvarchar(450) NOT NULL,
                                [ProductId] int NULL,
                                [ViewedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [ViewCount] int NOT NULL DEFAULT 1,
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UpdatedAt] datetime2 NULL,
                                [CreatedBy] nvarchar(max) NULL,
                                [UpdatedBy] nvarchar(max) NULL,
                                [IsDeleted] bit NOT NULL DEFAULT 0,
                                [DeletedAt] datetime2 NULL,
                                CONSTRAINT [PK_RecentlyViewedItems] PRIMARY KEY ([Id]),
                                CONSTRAINT [FK_RecentlyViewedItems_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]),
                                CONSTRAINT [FK_RecentlyViewedItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [catalog].[Products] ([Id])
                            );
                            
                            CREATE INDEX [IX_RecentlyViewedItems_UserId] ON [users].[RecentlyViewedItems] ([UserId]);
                            CREATE INDEX [IX_RecentlyViewedItems_ProductId] ON [users].[RecentlyViewedItems] ([ProductId]);
                            CREATE INDEX [IX_RecentlyViewedItems_ViewedAt] ON [users].[RecentlyViewedItems] ([ViewedAt]);
                        END", "Table.RecentlyViewedItems");

                    logger.LogInformation("Schema repairs and updates completed successfully.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Schema repair failed during initialization.");
                }
            }
        }

        private static async Task UpdateDatabaseSchemaAsync(IServiceProvider serviceProvider)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                var db = context.Database;

                // Helper local function for safe execution
                async Task SafeExecuteSql(string sql, string description)
                {
                    try
                    {
                        await db.ExecuteSqlRawAsync(sql);
                    }
                    catch (Exception ex)
                    {
                        // Log but allow startup to continue
                        logger.LogWarning(ex, $"Schema update failed for {description}. Continuing...");
                    }
                }

                async Task ExecuteCleanupAsync()
                {
                    // Clean up conflicting tables from 'system' schema that should now be in 'support'
                    // IMPORTANT: Drop dependent tables first to avoid FK constraints
                    await SafeExecuteSql(@"
                        IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[system].[TicketMessages]') AND type in (N'U'))
                        BEGIN
                            DROP TABLE [system].[TicketMessages];
                        END", "Cleanup.TicketMessages.Conflict");

                    await SafeExecuteSql(@"
                        IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[system].[SupportTickets]') AND type in (N'U'))
                        BEGIN
                            DROP TABLE [system].[SupportTickets];
                        END", "Cleanup.SupportTickets.Conflict");

                    // Clean up legacy schemas that were removed from DbContext
                    await SafeExecuteSql(@"
                        IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[notifications].[Notifications]') AND type in (N'U'))
                            DROP TABLE [notifications].[Notifications];
                        IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[notifications].[SmsLogs]') AND type in (N'U'))
                            DROP TABLE [notifications].[SmsLogs];
                        IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[notifications].[EmailLogs]') AND type in (N'U'))
                            DROP TABLE [notifications].[EmailLogs];
                    ", "Cleanup.LegacyNotifications");
                }

                try
                {
                    // 0. Fix conflicts and clean up legacy structures
                    await ExecuteCleanupAsync();

                    // 1. Ensure wallets schema exists
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'wallets')
                        BEGIN
                            EXEC('CREATE SCHEMA [wallets]');
                        END", "Schema.Wallets");

                    // 1. Create SellerWallets table (if not exists via migration)
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[wallets].[SellerWallets]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [wallets].[SellerWallets] (
                                [Id] int NOT NULL IDENTITY(1,1),
                                [SellerId] int NOT NULL,
                                [AvailableBalance] decimal(18,2) NOT NULL DEFAULT 0,
                                [PendingBalance] decimal(18,2) NOT NULL DEFAULT 0,
                                [TotalEarnings] decimal(18,2) NOT NULL DEFAULT 0,
                                [TotalWithdrawn] decimal(18,2) NOT NULL DEFAULT 0,
                                [Currency] nvarchar(10) NOT NULL DEFAULT 'BDT',
                                [IsActive] bit NOT NULL DEFAULT 1,
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UpdatedAt] datetime2 NULL,
                                [CreatedBy] nvarchar(max) NULL,
                                [UpdatedBy] nvarchar(max) NULL,
                                [IsDeleted] bit NOT NULL DEFAULT 0,
                                [DeletedAt] datetime2 NULL,
                                CONSTRAINT [PK_SellerWallets] PRIMARY KEY ([Id]),
                                CONSTRAINT [FK_SellerWallets_Sellers_SellerId] FOREIGN KEY ([SellerId]) REFERENCES [sellers].[Sellers] ([Id]) ON DELETE CASCADE
                            );
                            CREATE INDEX [IX_SellerWallets_SellerId] ON [wallets].[SellerWallets] ([SellerId]);
                        END", "SellerWallets Table");

                    // 2. Create UserWallets table (if not exists via migration)
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[wallets].[UserWallets]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [wallets].[UserWallets] (
                                [Id] int NOT NULL IDENTITY(1,1),
                                [UserId] nvarchar(450) NOT NULL,
                                [Balance] decimal(18,2) NOT NULL DEFAULT 0,
                                [Currency] nvarchar(10) NOT NULL DEFAULT 'BDT',
                                [IsActive] bit NOT NULL DEFAULT 1,
                                [IsLocked] bit NOT NULL DEFAULT 0,
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UpdatedAt] datetime2 NULL,
                                [CreatedBy] nvarchar(max) NULL,
                                [UpdatedBy] nvarchar(max) NULL,
                                [IsDeleted] bit NOT NULL DEFAULT 0,
                                [DeletedAt] datetime2 NULL,
                                CONSTRAINT [PK_UserWallets] PRIMARY KEY ([Id]),
                                CONSTRAINT [FK_UserWallets_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
                            );
                            CREATE INDEX [IX_UserWallets_UserId] ON [wallets].[UserWallets] ([UserId]);
                        END", "UserWallets Table");

                    // 3. Create AdminWallets table
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[wallets].[AdminWallets]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [wallets].[AdminWallets] (
                                [Id] int NOT NULL IDENTITY(1,1),
                                [TotalCommissionEarned] decimal(18,2) NOT NULL DEFAULT 0,
                                [TotalRefunded] decimal(18,2) NOT NULL DEFAULT 0,
                                [TotalPayoutsToSellers] decimal(18,2) NOT NULL DEFAULT 0,
                                [CurrentBalance] decimal(18,2) NOT NULL DEFAULT 0,
                                [Currency] nvarchar(10) NOT NULL DEFAULT 'BDT',
                                [ThisMonthCommission] decimal(18,2) NOT NULL DEFAULT 0,
                                [LastMonthCommission] decimal(18,2) NOT NULL DEFAULT 0,
                                [LastUpdated] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UpdatedAt] datetime2 NULL,
                                [CreatedBy] nvarchar(max) NULL,
                                [UpdatedBy] nvarchar(max) NULL,
                                [IsDeleted] bit NOT NULL DEFAULT 0,
                                [DeletedAt] datetime2 NULL,
                                CONSTRAINT [PK_AdminWallets] PRIMARY KEY ([Id])
                            );
                            
                            -- Seed initial record
                            INSERT INTO [wallets].[AdminWallets] 
                                ([Currency], [CreatedAt], [LastUpdated])
                            VALUES 
                                ('BDT', GETUTCDATE(), GETUTCDATE());
                                
                            PRINT 'Created AdminWallets table';
                        END", "AdminWallets Table");

                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[wallets].[WalletTransactions]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [wallets].[WalletTransactions] (
                                [Id] int NOT NULL IDENTITY,
                                [WalletId] int NOT NULL,
                                [SellerId] int NOT NULL,
                                [Amount] decimal(18,2) NOT NULL,
                                [Type] nvarchar(max) NOT NULL,
                                [Description] nvarchar(max) NOT NULL DEFAULT N'',
                                [ReferenceType] nvarchar(450) NULL,
                                [ReferenceId] nvarchar(450) NULL,
                                [TransactionDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [Status] nvarchar(max) NOT NULL DEFAULT N'Completed',
                                [BalanceAfterTransaction] decimal(18,2) NOT NULL,
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UpdatedAt] datetime2 NULL,
                                [CreatedBy] nvarchar(max) NULL,
                                [UpdatedBy] nvarchar(max) NULL,
                                [IsDeleted] bit NOT NULL DEFAULT 0,
                                [DeletedAt] datetime2 NULL,
                                CONSTRAINT [PK_WalletTransactions] PRIMARY KEY ([Id]),
                                CONSTRAINT [FK_WalletTransactions_Sellers_SellerId] FOREIGN KEY ([SellerId]) REFERENCES [sellers].[Sellers] ([Id]) ON DELETE CASCADE
                            );
                            
                            PRINT 'Created WalletTransactions table';
                        END", "WalletTransactions Table");

                    // 2. Add Wallet Transaction Indexes
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_WalletTransactions_SellerId_TransactionDate' AND object_id = OBJECT_ID('[wallets].[WalletTransactions]'))
                        BEGIN
                            CREATE INDEX [IX_WalletTransactions_SellerId_TransactionDate] 
                                ON [wallets].[WalletTransactions] ([SellerId], [TransactionDate]);
                        END", "WalletTransactions Index 1");
                        
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_WalletTransactions_ReferenceType_ReferenceId' AND object_id = OBJECT_ID('[wallets].[WalletTransactions]'))
                        BEGIN
                            CREATE INDEX [IX_WalletTransactions_ReferenceType_ReferenceId] 
                                ON [wallets].[WalletTransactions] ([ReferenceType], [ReferenceId]);
                        END", "WalletTransactions Index 2");

                    // 4. Create WithdrawalRequests table
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[wallets].[WithdrawalRequests]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [wallets].[WithdrawalRequests] (
                                [Id] int NOT NULL IDENTITY(1,1),
                                [SellerId] int NOT NULL,
                                [Amount] decimal(18,2) NOT NULL,
                                [Status] nvarchar(50) NOT NULL DEFAULT 'Pending',
                                [BankName] nvarchar(100) NOT NULL,
                                [AccountNumber] nvarchar(50) NOT NULL,
                                [AccountHolderName] nvarchar(100) NOT NULL,
                                [BranchName] nvarchar(100) NULL,
                                [RoutingNumber] nvarchar(50) NULL,
                                [ProcessedBy] nvarchar(450) NULL,
                                [ProcessedAt] datetime2 NULL,
                                [RejectionReason] nvarchar(max) NULL,
                                [TransactionReference] nvarchar(100) NULL,
                                [RequestDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UpdatedAt] datetime2 NULL,
                                [CreatedBy] nvarchar(max) NULL,
                                [UpdatedBy] nvarchar(max) NULL,
                                [IsDeleted] bit NOT NULL DEFAULT 0,
                                [DeletedAt] datetime2 NULL,
                                CONSTRAINT [PK_WithdrawalRequests] PRIMARY KEY ([Id]),
                                CONSTRAINT [FK_WithdrawalRequests_Sellers_SellerId] FOREIGN KEY ([SellerId]) REFERENCES [sellers].[Sellers] ([Id]) ON DELETE CASCADE
                            );
                            CREATE INDEX [IX_WithdrawalRequests_SellerId] ON [wallets].[WithdrawalRequests] ([SellerId]);
                        END", "WithdrawalRequests Table");

                    // 5. Create LedgerEntries table
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[wallets].[LedgerEntries]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [wallets].[LedgerEntries] (
                                [Id] int NOT NULL IDENTITY(1,1),
                                [EntryNumber] nvarchar(50) NOT NULL,
                                [OrderId] int NULL,
                                [SellerId] int NULL,
                                [UserId] nvarchar(450) NULL,
                                [EntryType] int NOT NULL,
                                [TransactionType] nvarchar(20) NOT NULL DEFAULT 'Credit',
                                [Amount] decimal(18,2) NOT NULL,
                                [BalanceBefore] decimal(18,2) NOT NULL,
                                [BalanceAfter] decimal(18,2) NOT NULL,
                                [Reference] nvarchar(100) NULL,
                                [Description] nvarchar(max) NULL,
                                [IsEscrowHeld] bit NOT NULL DEFAULT 0,
                                [EscrowReleasedAt] datetime2 NULL,
                                [EntryDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UpdatedAt] datetime2 NULL,
                                [CreatedBy] nvarchar(max) NULL,
                                [UpdatedBy] nvarchar(max) NULL,
                                [IsDeleted] bit NOT NULL DEFAULT 0,
                                [DeletedAt] datetime2 NULL,
                                CONSTRAINT [PK_LedgerEntries] PRIMARY KEY ([Id])
                            );
                            CREATE INDEX [IX_LedgerEntries_OrderId] ON [wallets].[LedgerEntries] ([OrderId]);
                            CREATE INDEX [IX_LedgerEntries_SellerId] ON [wallets].[LedgerEntries] ([SellerId]);
                            CREATE INDEX [IX_LedgerEntries_EntryDate] ON [wallets].[LedgerEntries] ([EntryDate]);
                        END", "LedgerEntries Table");

                    // 6. Create AdminTransactions table
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[wallets].[AdminTransactions]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [wallets].[AdminTransactions] (
                                [Id] int NOT NULL IDENTITY(1,1),
                                [Amount] decimal(18,2) NOT NULL,
                                [BalanceBefore] decimal(18,2) NOT NULL,
                                [BalanceAfter] decimal(18,2) NOT NULL,
                                [TransactionType] nvarchar(50) NOT NULL DEFAULT 'Commission',
                                [Description] nvarchar(max) NOT NULL DEFAULT '',
                                [ReferenceType] nvarchar(50) NULL,
                                [ReferenceId] nvarchar(50) NULL,
                                [TransactionDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UpdatedAt] datetime2 NULL,
                                [CreatedBy] nvarchar(max) NULL,
                                [UpdatedBy] nvarchar(max) NULL,
                                [IsDeleted] bit NOT NULL DEFAULT 0,
                                [DeletedAt] datetime2 NULL,
                                CONSTRAINT [PK_AdminTransactions] PRIMARY KEY ([Id])
                            );
                            CREATE INDEX [IX_AdminTransactions_TransactionDate] ON [wallets].[AdminTransactions] ([TransactionDate]);
                            CREATE INDEX [IX_AdminTransactions_ReferenceType_ReferenceId] ON [wallets].[AdminTransactions] ([ReferenceType], [ReferenceId]);
                        END", "AdminTransactions Table");

                    // 7. Create Support System Tables
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[support].[SupportTickets]') AND type in (N'U'))
                        BEGIN
                            -- Ensure schema exists
                            IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'support')
                            BEGIN
                                EXEC('CREATE SCHEMA [support]')
                            END

                            CREATE TABLE [support].[SupportTickets] (
                                [Id] int NOT NULL IDENTITY,
                                [TicketNumber] nvarchar(max) NOT NULL DEFAULT N'',
                                [UserId] nvarchar(450) NOT NULL,
                                [SellerId] int NULL,
                                [OrderId] int NULL,
                                [Category] nvarchar(max) NOT NULL DEFAULT N'',
                                [Priority] nvarchar(max) NOT NULL DEFAULT N'Medium',
                                [Status] nvarchar(max) NOT NULL DEFAULT N'Open',
                                [Subject] nvarchar(max) NOT NULL DEFAULT N'',
                                [Description] nvarchar(max) NOT NULL DEFAULT N'',
                                [AssignedTo] nvarchar(max) NULL,
                                [AssignedAt] datetime2 NULL,
                                [FirstResponseAt] datetime2 NULL,
                                [ResolvedAt] datetime2 NULL,
                                [ClosedAt] datetime2 NULL,
                                [LastUpdatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [ResolutionNotes] nvarchar(max) NULL,
                                [SatisfactionRating] int NULL,
                                [CustomerFeedback] nvarchar(max) NULL,
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UpdatedAt] datetime2 NULL,
                                [CreatedBy] nvarchar(max) NULL,
                                [UpdatedBy] nvarchar(max) NULL,
                                [IsDeleted] bit NOT NULL DEFAULT 0,
                                [DeletedAt] datetime2 NULL,
                                CONSTRAINT [PK_SupportTickets] PRIMARY KEY ([Id]),
                                CONSTRAINT [FK_SupportTickets_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
                            );
                            
                            -- Index for User Tickets
                            CREATE INDEX [IX_SupportTickets_UserId] ON [support].[SupportTickets] ([UserId]);
                        END", "SupportTickets Table");
                        
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[support].[TicketMessages]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [support].[TicketMessages] (
                                [Id] int NOT NULL IDENTITY,
                                [SupportTicketId] int NOT NULL,
                                [UserId] nvarchar(max) NOT NULL,
                                [Message] nvarchar(max) NOT NULL DEFAULT N'',
                                [IsStaffReply] bit NOT NULL DEFAULT 0,
                                [IsInternal] bit NOT NULL DEFAULT 0,
                                [Attachments] nvarchar(max) NULL,
                                [SentAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [IsRead] bit NOT NULL DEFAULT 0,
                                [ReadAt] datetime2 NULL,
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UpdatedAt] datetime2 NULL,
                                [CreatedBy] nvarchar(max) NULL,
                                [UpdatedBy] nvarchar(max) NULL,
                                [IsDeleted] bit NOT NULL DEFAULT 0,
                                [DeletedAt] datetime2 NULL,
                                CONSTRAINT [PK_TicketMessages] PRIMARY KEY ([Id]),
                                CONSTRAINT [FK_TicketMessages_SupportTickets_SupportTicketId] FOREIGN KEY ([SupportTicketId]) REFERENCES [support].[SupportTickets] ([Id]) ON DELETE CASCADE
                            );
                            
                            CREATE INDEX [IX_TicketMessages_SupportTicketId] ON [support].[TicketMessages] ([SupportTicketId]);
                        END", "TicketMessages Table");

                    // 4. Update Sellers Table (Schema Drift Fix)
                    await SafeExecuteSql(@"IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[sellers].[Sellers]') AND name = 'NotifyOnNewOrder') ALTER TABLE [sellers].[Sellers] ADD [NotifyOnNewOrder] bit NOT NULL DEFAULT 1;", "Seller.NotifyOnNewOrder");
                    await SafeExecuteSql(@"IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[sellers].[Sellers]') AND name = 'NotifyOnNewMessage') ALTER TABLE [sellers].[Sellers] ADD [NotifyOnNewMessage] bit NOT NULL DEFAULT 1;", "Seller.NotifyOnNewMessage");
                    await SafeExecuteSql(@"IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[sellers].[Sellers]') AND name = 'NotifyOnReview') ALTER TABLE [sellers].[Sellers] ADD [NotifyOnReview] bit NOT NULL DEFAULT 1;", "Seller.NotifyOnReview");
                    await SafeExecuteSql(@"IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[sellers].[Sellers]') AND name = 'ShippingPolicy') ALTER TABLE [sellers].[Sellers] ADD [ShippingPolicy] nvarchar(max) NULL;", "Seller.ShippingPolicy");
                    await SafeExecuteSql(@"IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[sellers].[Sellers]') AND name = 'ReturnPolicy') ALTER TABLE [sellers].[Sellers] ADD [ReturnPolicy] nvarchar(max) NULL;", "Seller.ReturnPolicy");
                    await SafeExecuteSql(@"IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[sellers].[Sellers]') AND name = 'ProcessingTime') ALTER TABLE [sellers].[Sellers] ADD [ProcessingTime] nvarchar(max) NULL;", "Seller.ProcessingTime");

                    // Add UpdatedAt to Products if missing
                    await SafeExecuteSql(@"IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[catalog].[Products]') AND name = 'UpdatedAt') ALTER TABLE [catalog].[Products] ADD [UpdatedAt] datetime2 NULL;", "Products.UpdatedAt");
                        
                    // Update default value for ProcessingTime
                    await SafeExecuteSql(@"IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[sellers].[Sellers]') AND name = 'ProcessingTime') EXEC('UPDATE [sellers].[Sellers] SET [ProcessingTime] = ''1-3 business days'' WHERE [ProcessingTime] IS NULL');", "Seller.ProcessingTime Default");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error updating database schema for wallets.");
                }
            }
        }

        private static async Task InitializeHomepageContentSchemaAsync(IServiceProvider serviceProvider)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                var db = context.Database;

                async Task SafeExecuteSql(string sql, string description)
                {
                    try
                    {
                        await db.ExecuteSqlRawAsync(sql);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, $"Homepage schema update failed for {description}. Continuing...");
                    }
                }

                try
                {
                    // 1. Create Required Schemas
                    await SafeExecuteSql(@"IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'catalog') EXEC('CREATE SCHEMA catalog');", "Schema.Catalog");

                    // HomepageSections
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[content].[HomepageSections]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [content].[HomepageSections] (
                                [Id] int NOT NULL IDENTITY(1,1),
                                [Name] nvarchar(max) NOT NULL,
                                [Slug] nvarchar(450) NOT NULL,
                                [DisplayTitle] nvarchar(max) NOT NULL,
                                [Description] nvarchar(max) NULL,
                                [SectionType] nvarchar(max) NOT NULL,
                                [BackgroundColor] nvarchar(max) NULL,
                                [BannerImageUrl] nvarchar(max) NULL,
                                [DisplayOrder] int NOT NULL,
                                [IsActive] bit NOT NULL,
                                [MaxProductsToDisplay] int NOT NULL,
                                [ProductsPerRow] int NOT NULL,
                                [LayoutType] nvarchar(max) NOT NULL,
                                [CardSize] nvarchar(max) NOT NULL,
                                [UseAutomatedSelection] bit NOT NULL,
                                [UseManualSelection] bit NOT NULL,
                                [ShowRating] bit NOT NULL,
                                [ShowPrice] bit NOT NULL,
                                [ShowDiscount] bit NOT NULL,
                                [LastAutomationRunTime] datetime2 NULL,
                                [CreatedByUserId] nvarchar(450) NULL,
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UpdatedAt] datetime2 NULL,
                                [CreatedBy] nvarchar(max) NULL,
                                [UpdatedBy] nvarchar(max) NULL,
                                [IsDeleted] bit NOT NULL DEFAULT 0,
                                [DeletedAt] datetime2 NULL,
                                CONSTRAINT [PK_HomepageSections] PRIMARY KEY ([Id]),
                                CONSTRAINT [FK_HomepageSections_AspNetUsers_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [AspNetUsers] ([Id])
                            );
                            CREATE UNIQUE INDEX [IX_HomepageSections_Slug] ON [content].[HomepageSections] ([Slug]);
                        END", "Table.HomepageSections");

                    // HomepageSectionProducts
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[content].[HomepageSectionProducts]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [content].[HomepageSectionProducts] (
                                [Id] int NOT NULL IDENTITY(1,1),
                                [SectionId] int NOT NULL,
                                [ProductId] int NOT NULL,
                                [DisplayOrder] int NOT NULL,
                                [PromotionalText] nvarchar(max) NULL,
                                [BadgeText] nvarchar(max) NULL,
                                [IsActive] bit NOT NULL,
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UpdatedAt] datetime2 NULL,
                                [CreatedBy] nvarchar(max) NULL,
                                [UpdatedBy] nvarchar(max) NULL,
                                [IsDeleted] bit NOT NULL DEFAULT 0,
                                [DeletedAt] datetime2 NULL,
                                CONSTRAINT [PK_HomepageSectionProducts] PRIMARY KEY ([Id]),
                                CONSTRAINT [FK_HomepageSectionProducts_HomepageSections_SectionId] FOREIGN KEY ([SectionId]) REFERENCES [content].[HomepageSections] ([Id]) ON DELETE CASCADE,
                                CONSTRAINT [FK_HomepageSectionProducts_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [catalog].[Products] ([Id]) ON DELETE CASCADE
                            );
                            CREATE INDEX [IX_HomepageSectionProducts_SectionId] ON [content].[HomepageSectionProducts] ([SectionId]);
                            CREATE INDEX [IX_HomepageSectionProducts_ProductId] ON [content].[HomepageSectionProducts] ([ProductId]);
                        END", "Table.HomepageSectionProducts");

                    // HomepageSectionCategories
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[content].[HomepageSectionCategories]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [content].[HomepageSectionCategories] (
                                [Id] int NOT NULL IDENTITY(1,1),
                                [SectionId] int NOT NULL,
                                [CategoryId] int NOT NULL,
                                [DisplayOrder] int NOT NULL,
                                [CustomDisplayTitle] nvarchar(max) NULL,
                                [ProductCountToShow] int NOT NULL,
                                [IsActive] bit NOT NULL,
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UpdatedAt] datetime2 NULL,
                                [CreatedBy] nvarchar(max) NULL,
                                [UpdatedBy] nvarchar(max) NULL,
                                [IsDeleted] bit NOT NULL DEFAULT 0,
                                [DeletedAt] datetime2 NULL,
                                CONSTRAINT [PK_HomepageSectionCategories] PRIMARY KEY ([Id]),
                                CONSTRAINT [FK_HomepageSectionCategories_HomepageSections_SectionId] FOREIGN KEY ([SectionId]) REFERENCES [content].[HomepageSections] ([Id]) ON DELETE CASCADE,
                                CONSTRAINT [FK_HomepageSectionCategories_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [catalog].[Categories] ([Id]) ON DELETE CASCADE
                            );
                            CREATE INDEX [IX_HomepageSectionCategories_SectionId] ON [content].[HomepageSectionCategories] ([SectionId]);
                            CREATE INDEX [IX_HomepageSectionCategories_CategoryId] ON [content].[HomepageSectionCategories] ([CategoryId]);
                        END", "Table.HomepageSectionCategories");

                    // Repair existing tables if they exist but miss columns
                    await SafeExecuteSql(@"
                        IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[content].[HomepageSections]') AND type in (N'U'))
                        BEGIN
                            IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[content].[HomepageSections]') AND name = 'CreatedAt')
                                ALTER TABLE [content].[HomepageSections] ADD [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE();
                            IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[content].[HomepageSections]') AND name = 'UpdatedAt')
                                ALTER TABLE [content].[HomepageSections] ADD [UpdatedAt] datetime2 NULL;
                            IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[content].[HomepageSections]') AND name = 'CreatedBy')
                                ALTER TABLE [content].[HomepageSections] ADD [CreatedBy] nvarchar(max) NULL;
                            IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[content].[HomepageSections]') AND name = 'UpdatedBy')
                                ALTER TABLE [content].[HomepageSections] ADD [UpdatedBy] nvarchar(max) NULL;
                            IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[content].[HomepageSections]') AND name = 'IsDeleted')
                                ALTER TABLE [content].[HomepageSections] ADD [IsDeleted] bit NOT NULL DEFAULT 0;
                            IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[content].[HomepageSections]') AND name = 'DeletedAt')
                                ALTER TABLE [content].[HomepageSections] ADD [DeletedAt] datetime2 NULL;
                        END", "Repair.HomepageSections");

                    // Repair HomepageSectionProducts
                    await SafeExecuteSql(@"
                        IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[content].[HomepageSectionProducts]') AND type in (N'U'))
                        BEGIN
                            IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[content].[HomepageSectionProducts]') AND name = 'CreatedAt')
                                ALTER TABLE [content].[HomepageSectionProducts] ADD [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE();
                            IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[content].[HomepageSectionProducts]') AND name = 'UpdatedAt')
                                ALTER TABLE [content].[HomepageSectionProducts] ADD [UpdatedAt] datetime2 NULL;
                            IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[content].[HomepageSectionProducts]') AND name = 'CreatedBy')
                                ALTER TABLE [content].[HomepageSectionProducts] ADD [CreatedBy] nvarchar(max) NULL;
                            IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[content].[HomepageSectionProducts]') AND name = 'UpdatedBy')
                                ALTER TABLE [content].[HomepageSectionProducts] ADD [UpdatedBy] nvarchar(max) NULL;
                            IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[content].[HomepageSectionProducts]') AND name = 'IsDeleted')
                                ALTER TABLE [content].[HomepageSectionProducts] ADD [IsDeleted] bit NOT NULL DEFAULT 0;
                            IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[content].[HomepageSectionProducts]') AND name = 'DeletedAt')
                                ALTER TABLE [content].[HomepageSectionProducts] ADD [DeletedAt] datetime2 NULL;
                        END", "Repair.HomepageSectionProducts");

                    // Repair HomepageSectionCategories
                    await SafeExecuteSql(@"
                        IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[content].[HomepageSectionCategories]') AND type in (N'U'))
                        BEGIN
                            IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[content].[HomepageSectionCategories]') AND name = 'CreatedAt')
                                ALTER TABLE [content].[HomepageSectionCategories] ADD [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE();
                            IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[content].[HomepageSectionCategories]') AND name = 'UpdatedAt')
                                ALTER TABLE [content].[HomepageSectionCategories] ADD [UpdatedAt] datetime2 NULL;
                            IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[content].[HomepageSectionCategories]') AND name = 'CreatedBy')
                                ALTER TABLE [content].[HomepageSectionCategories] ADD [CreatedBy] nvarchar(max) NULL;
                            IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[content].[HomepageSectionCategories]') AND name = 'UpdatedBy')
                                ALTER TABLE [content].[HomepageSectionCategories] ADD [UpdatedBy] nvarchar(max) NULL;
                            IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[content].[HomepageSectionCategories]') AND name = 'IsDeleted')
                                ALTER TABLE [content].[HomepageSectionCategories] ADD [IsDeleted] bit NOT NULL DEFAULT 0;
                            IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[content].[HomepageSectionCategories]') AND name = 'DeletedAt')
                                ALTER TABLE [content].[HomepageSectionCategories] ADD [DeletedAt] datetime2 NULL;
                        END", "Repair.HomepageSectionCategories");

                    // TrendingProductSuggestions
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[content].[TrendingProductSuggestions]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [content].[TrendingProductSuggestions] (
                                [Id] int NOT NULL IDENTITY(1,1),
                                [ProductId] int NOT NULL,
                                [ConfidenceScore] decimal(18,2) NOT NULL,
                                [SalesCount] int NOT NULL,
                                [ViewCount] int NOT NULL,
                                [WishlistCount] int NOT NULL,
                                [AverageRating] decimal(18,2) NOT NULL,
                                [SalesGrowthRate] decimal(18,2) NOT NULL,
                                [CalculatedAt] datetime2 NOT NULL,
                                [ExpiresAt] datetime2 NOT NULL,
                                [IsActive] bit NOT NULL,
                                [AnalysisPeriod] nvarchar(max) NOT NULL,
                                [Rank] int NOT NULL,
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UpdatedAt] datetime2 NULL,
                                [CreatedBy] nvarchar(max) NULL,
                                [UpdatedBy] nvarchar(max) NULL,
                                [IsDeleted] bit NOT NULL DEFAULT 0,
                                [DeletedAt] datetime2 NULL,
                                CONSTRAINT [PK_TrendingProductSuggestions] PRIMARY KEY ([Id]),
                                CONSTRAINT [FK_TrendingProductSuggestions_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [catalog].[Products] ([Id]) ON DELETE CASCADE
                            );
                            CREATE INDEX [IX_TrendingProductSuggestions_ProductId_CalculatedAt] ON [content].[TrendingProductSuggestions] ([ProductId], [CalculatedAt]);
                        END", "Table.TrendingProductSuggestions");

                    // FlashSaleProductSuggestions
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[content].[FlashSaleProductSuggestions]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [content].[FlashSaleProductSuggestions] (
                                [Id] int NOT NULL IDENTITY(1,1),
                                [ProductId] int NOT NULL,
                                [SuggestedDiscountPercentage] decimal(18,2) NOT NULL,
                                [SuggestedFlashPrice] decimal(18,2) NOT NULL,
                                [SuggestionReason] nvarchar(max) NOT NULL,
                                [CurrentInventory] int NOT NULL,
                                [RecommendedQuantityForFlash] int NOT NULL,
                                [ExpectedSalesBoost] decimal(18,2) NOT NULL,
                                [EstimatedRevenueLift] decimal(18,2) NOT NULL,
                                [CalculatedAt] datetime2 NOT NULL,
                                [ExpiresAt] datetime2 NOT NULL,
                                [IsActive] bit NOT NULL,
                                [PriorityScore] decimal(18,2) NOT NULL,
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UpdatedAt] datetime2 NULL,
                                [CreatedBy] nvarchar(max) NULL,
                                [UpdatedBy] nvarchar(max) NULL,
                                [IsDeleted] bit NOT NULL DEFAULT 0,
                                [DeletedAt] datetime2 NULL,
                                CONSTRAINT [PK_FlashSaleProductSuggestions] PRIMARY KEY ([Id]),
                                CONSTRAINT [FK_FlashSaleProductSuggestions_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [catalog].[Products] ([Id]) ON DELETE CASCADE
                            );
                            CREATE INDEX [IX_FlashSaleProductSuggestions_ProductId_CalculatedAt] ON [content].[FlashSaleProductSuggestions] ([ProductId], [CalculatedAt]);
                        END", "Table.FlashSaleProductSuggestions");

                    // UserBehaviorAnalytics
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[content].[UserBehaviorAnalytics]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [content].[UserBehaviorAnalytics] (
                                [Id] int NOT NULL IDENTITY(1,1),
                                [UserId] nvarchar(450) NOT NULL,
                                [ActionType] nvarchar(max) NOT NULL,
                                [ProductId] int NULL,
                                [CategoryId] int NULL,
                                [SearchTerm] nvarchar(max) NULL,
                                [TimeSpentSeconds] int NULL,
                                [DeviceType] nvarchar(max) NULL,
                                [SessionId] nvarchar(max) NULL,
                                [ActionDateTime] datetime2 NOT NULL,
                                [IpAddress] nvarchar(max) NULL,
                                [UserAgent] nvarchar(max) NULL,
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UpdatedAt] datetime2 NULL,
                                [CreatedBy] nvarchar(max) NULL,
                                [UpdatedBy] nvarchar(max) NULL,
                                [IsDeleted] bit NOT NULL DEFAULT 0,
                                [DeletedAt] datetime2 NULL,
                                CONSTRAINT [PK_UserBehaviorAnalytics] PRIMARY KEY ([Id])
                            );
                            CREATE INDEX [IX_UserBehaviorAnalytics_UserId_ActionDateTime] ON [content].[UserBehaviorAnalytics] ([UserId], [ActionDateTime]);
                            CREATE INDEX [IX_UserBehaviorAnalytics_ProductId_ActionDateTime] ON [content].[UserBehaviorAnalytics] ([ProductId], [ActionDateTime]);
                        END", "Table.UserBehaviorAnalytics");

                    // SalesMetricsSnapshots
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[content].[SalesMetricsSnapshots]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [content].[SalesMetricsSnapshots] (
                                [Id] int NOT NULL IDENTITY(1,1),
                                [ProductId] int NOT NULL,
                                [TotalSales] int NOT NULL,
                                [TotalRevenue] decimal(18,2) NOT NULL,
                                [AverageSellingPrice] decimal(18,2) NOT NULL,
                                [UniqueBuyers] int NOT NULL,
                                [PageViews] int NOT NULL,
                                [SearchImpressions] int NOT NULL,
                                [ClickThroughRate] decimal(18,2) NOT NULL,
                                [ConversionRate] decimal(18,2) NOT NULL,
                                [AverageRating] decimal(18,2) NOT NULL,
                                [ReviewCount] int NOT NULL,
                                [ReturnRate] decimal(18,2) NOT NULL,
                                [PeriodStartDate] datetime2 NOT NULL,
                                [PeriodEndDate] datetime2 NOT NULL,
                                [SnapshotDateTime] datetime2 NOT NULL,
                                [SalesTrend] nvarchar(max) NOT NULL,
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UpdatedAt] datetime2 NULL,
                                [CreatedBy] nvarchar(max) NULL,
                                [UpdatedBy] nvarchar(max) NULL,
                                [IsDeleted] bit NOT NULL DEFAULT 0,
                                [DeletedAt] datetime2 NULL,
                                CONSTRAINT [PK_SalesMetricsSnapshots] PRIMARY KEY ([Id]),
                                CONSTRAINT [FK_SalesMetricsSnapshots_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [catalog].[Products] ([Id]) ON DELETE CASCADE
                            );
                            CREATE INDEX [IX_SalesMetricsSnapshots_ProductId_SnapshotDateTime] ON [content].[SalesMetricsSnapshots] ([ProductId], [SnapshotDateTime]);
                        END", "Table.SalesMetricsSnapshots");

                    // HomepageSectionAuditLogs
                    await SafeExecuteSql(@"
                        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[content].[HomepageSectionAuditLogs]') AND type in (N'U'))
                        BEGIN
                            CREATE TABLE [content].[HomepageSectionAuditLogs] (
                                [Id] int NOT NULL IDENTITY(1,1),
                                [SectionId] int NOT NULL,
                                [ChangeType] nvarchar(max) NOT NULL,
                                [PropertyName] nvarchar(max) NOT NULL,
                                [OldValue] nvarchar(max) NULL,
                                [NewValue] nvarchar(max) NULL,
                                [ChangedByUserId] nvarchar(450) NULL,
                                [ChangedAt] datetime2 NOT NULL,
                                [Notes] nvarchar(max) NULL,
                                [IsAutomatedChange] bit NOT NULL,
                                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                                [UpdatedAt] datetime2 NULL,
                                [CreatedBy] nvarchar(max) NULL,
                                [UpdatedBy] nvarchar(max) NULL,
                                [IsDeleted] bit NOT NULL DEFAULT 0,
                                [DeletedAt] datetime2 NULL,
                                CONSTRAINT [PK_HomepageSectionAuditLogs] PRIMARY KEY ([Id]),
                                CONSTRAINT [FK_HomepageSectionAuditLogs_HomepageSections_SectionId] FOREIGN KEY ([SectionId]) REFERENCES [content].[HomepageSections] ([Id]) ON DELETE CASCADE
                            );
                            CREATE INDEX [IX_HomepageSectionAuditLogs_SectionId_ChangedAt] ON [content].[HomepageSectionAuditLogs] ([SectionId], [ChangedAt]);
                        END", "Table.HomepageSectionAuditLogs");

                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error initializing homepage content schema.");
                }
            }
        }

        private static async Task UpdateStoredProceduresAsync(IServiceProvider serviceProvider)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                try
                {
                    var sql = @"
CREATE OR ALTER PROCEDURE [catalog].[usp_SearchProducts]
    @CategoryId INT = NULL,
    @SearchTerm NVARCHAR(100) = NULL,
    @MinPrice DECIMAL(18,2) = NULL,
    @MaxPrice DECIMAL(18,2) = NULL,
    @AttributeFilters NVARCHAR(MAX) = NULL,
    @SortBy NVARCHAR(50) = 'Relevance',
    @PageNumber INT = 1,
    @PageSize INT = 20
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FilterTable TABLE (FieldKey NVARCHAR(50), FilterValue NVARCHAR(MAX));

    IF @AttributeFilters IS NOT NULL
    BEGIN
        INSERT INTO @FilterTable (FieldKey, FilterValue)
        SELECT [Key], [Value] FROM OPENJSON(@AttributeFilters)
        WITH ([Key] NVARCHAR(50), [Value] NVARCHAR(MAX));
    END

    ;WITH ProductResults AS (
        SELECT
            p.Id,
            p.Title,
            p.ShortDescription,
            p.BasePrice,
            p.DiscountPercent,
            (SELECT TOP 1 Url FROM [catalog].[ProductImages] pi WHERE pi.ProductId = p.Id ORDER BY pi.SortOrder) as Thumbnail,
            (SELECT COALESCE(SUM(Stock), 0) FROM [catalog].[ProductVariants] pv WHERE pv.ProductId = p.Id) as StockQuantity,
            p.Slug,
            c.Name as CategoryName,
            s.ShopName as SellerName,
            (CASE WHEN p.Title LIKE '%' + @SearchTerm + '%' THEN 10 ELSE 0 END +
             CASE WHEN p.Description LIKE '%' + @SearchTerm + '%' THEN 5 ELSE 0 END) as Relevance,

            -- AI/Smart Search Columns
            100 as ConfidenceScore,
            CAST(NULL AS NVARCHAR(MAX)) as SmartTags,
            CAST('None' AS NVARCHAR(50)) as PersonalizationLevel,
            CAST(0 AS BIT) as IsFuzzyMatch,

            p.CreatedAt
        FROM [catalog].[Products] p
        INNER JOIN [catalog].[Categories] c ON p.CategoryId = c.Id
        INNER JOIN [sellers].[Sellers] s ON p.SellerId = s.Id
        WHERE p.IsActive = 1
        AND (@CategoryId IS NULL OR p.CategoryId = @CategoryId)
        AND (@MinPrice IS NULL OR (p.BasePrice * (1 - ISNULL(p.DiscountPercent,0)/100.0)) >= @MinPrice)
        AND (@MaxPrice IS NULL OR (p.BasePrice * (1 - ISNULL(p.DiscountPercent,0)/100.0)) <= @MaxPrice)
        AND (@SearchTerm IS NULL OR p.Title LIKE '%' + @SearchTerm + '%' OR p.Description LIKE '%' + @SearchTerm + '%')
        AND NOT EXISTS (
            SELECT 1 FROM @FilterTable ft
            WHERE NOT EXISTS (
                SELECT 1
                FROM [dynamic].[FormData] fe
                JOIN [dynamic].[FormDataFieldValues] fv ON fe.Id = fv.EntryId
                JOIN [dynamic].[FormFieldMaster] ff ON fv.FieldId = ff.Id
                WHERE fe.ReferenceType = 'Product'
                AND fe.ReferenceId = CAST(p.Id AS NVARCHAR(50))
                AND ff.Name = ft.FieldKey
                AND fv.Value = ft.FilterValue
            )
        )
    )
    SELECT
        Id, Title, ShortDescription, BasePrice, DiscountPercent,
        Thumbnail, StockQuantity, Slug, CategoryName, SellerName, Relevance,
        ConfidenceScore, SmartTags, PersonalizationLevel, IsFuzzyMatch
    FROM ProductResults
    ORDER BY
        CASE WHEN @SortBy = 'PriceLowHigh' THEN (BasePrice * (1 - ISNULL(DiscountPercent,0)/100.0)) END ASC,     
        CASE WHEN @SortBy = 'PriceHighLow' THEN (BasePrice * (1 - ISNULL(DiscountPercent,0)/100.0)) END DESC,    
        CASE WHEN @SortBy = 'Relevance' THEN Relevance END DESC,
        CreatedAt DESC

    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END";

                    await context.Database.ExecuteSqlRawAsync(sql);
                }
                catch
                {
                    // Silent - stored procedure update is not critical
                }
            }
        }
    }
}
