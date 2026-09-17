using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Catalog;
using Sparkle.Domain.Notifications;
using Sparkle.Domain.Orders;
using Sparkle.Domain.Support;
using Sparkle.Domain.Sellers;
using Sparkle.Domain.Users;
using Sparkle.Domain.Reviews;
using Sparkle.Domain.Content;
using Sparkle.Domain.Marketing;
using Sparkle.Domain.Logistics;
using Sparkle.Domain.System;
using Sparkle.Domain.Configuration;
using Sparkle.Domain.Identity;
using SystemNotification = Sparkle.Domain.System.Notification;
using LegacyNotification = Sparkle.Domain.Notifications.Notification;
using AnalyticsSellerEarning = Sparkle.Domain.System.SellerEarning;

namespace Sparkle.Infrastructure;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    // ==================== CATALOG ====================
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Review> Reviews => Set<Review>();

    // ==================== USER MANAGEMENT ====================
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserAddress> UserAddresses => Set<UserAddress>();
    public DbSet<UserWishlistItem> UserWishlistItems => Set<UserWishlistItem>();
    public DbSet<UserSearchHistory> UserSearchHistories => Set<UserSearchHistory>();
    public DbSet<UserNotificationSettings> UserNotificationSettings => Set<UserNotificationSettings>();
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();
    public DbSet<UserSearchPreference> UserSearchPreferences => Set<UserSearchPreference>();
    
    // New User Features
    public DbSet<Sparkle.Domain.Users.RecentlyViewedItem> RecentlyViewedItems => Set<Sparkle.Domain.Users.RecentlyViewedItem>();
    public DbSet<Sparkle.Domain.Users.CompareListItem> CompareListItems => Set<Sparkle.Domain.Users.CompareListItem>();
    public DbSet<Sparkle.Domain.Users.StockNotification> StockNotifications => Set<Sparkle.Domain.Users.StockNotification>();
    public DbSet<Sparkle.Domain.Users.PriceAlert> PriceAlerts => Set<Sparkle.Domain.Users.PriceAlert>();

    // ==================== ORDERS & PAYMENTS ====================
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Wishlist> Wishlists => Set<Wishlist>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderTracking> OrderTrackings => Set<OrderTracking>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<Sparkle.Domain.Content.StaticPage> StaticPages { get; set; }
    public DbSet<Sparkle.Domain.Content.FaqItem> FaqItems { get; set; }

    // ==================== HOMEPAGE SECTIONS & INTELLIGENT FEATURES ====================
    public DbSet<Sparkle.Domain.Content.HomepageSection> HomepageSections => Set<Sparkle.Domain.Content.HomepageSection>();
    public DbSet<Sparkle.Domain.Content.HomepageSectionProduct> HomepageSectionProducts => Set<Sparkle.Domain.Content.HomepageSectionProduct>();
    public DbSet<Sparkle.Domain.Content.HomepageSectionCategory> HomepageSectionCategories => Set<Sparkle.Domain.Content.HomepageSectionCategory>();
    public DbSet<Sparkle.Domain.Content.TrendingProductSuggestion> TrendingProductSuggestions => Set<Sparkle.Domain.Content.TrendingProductSuggestion>();
    public DbSet<Sparkle.Domain.Content.FlashSaleProductSuggestion> FlashSaleProductSuggestions => Set<Sparkle.Domain.Content.FlashSaleProductSuggestion>();
    public DbSet<Sparkle.Domain.Content.UserBehaviorAnalytic> UserBehaviorAnalytics => Set<Sparkle.Domain.Content.UserBehaviorAnalytic>();
    public DbSet<Sparkle.Domain.Content.SalesMetricsSnapshot> SalesMetricsSnapshots => Set<Sparkle.Domain.Content.SalesMetricsSnapshot>();
    public DbSet<Sparkle.Domain.Content.HomepageSectionAuditLog> HomepageSectionAuditLogs => Set<Sparkle.Domain.Content.HomepageSectionAuditLog>();

    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<ReturnRequest> ReturnRequests => Set<ReturnRequest>();


    // ==================== REVIEWS & RATINGS ====================
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<ReviewImage> ReviewImages => Set<ReviewImage>();
    public DbSet<ReviewVote> ReviewVotes => Set<ReviewVote>();
    public DbSet<ReviewEditHistory> ReviewEditHistories => Set<ReviewEditHistory>();
    public DbSet<ProductQuestion> ProductQuestions => Set<ProductQuestion>();
    public DbSet<QuestionAnswer> QuestionAnswers => Set<QuestionAnswer>();


    // ==================== MARKETING & PROMOTIONS ====================
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<LoyaltyPointHistory> LoyaltyPointHistories => Set<LoyaltyPointHistory>();
    public DbSet<LoyaltyPointConfig> LoyaltyPointConfigs => Set<LoyaltyPointConfig>();
    public DbSet<VoucherUsage> VoucherUsages => Set<VoucherUsage>();
    public DbSet<FlashDeal> FlashDeals => Set<FlashDeal>();
    public DbSet<FlashDealProduct> FlashDealProducts => Set<FlashDealProduct>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignProduct> CampaignProducts => Set<CampaignProduct>();
    public DbSet<CampaignCategory> CampaignCategories => Set<CampaignCategory>();
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<EmailSubscription> EmailSubscriptions => Set<EmailSubscription>();
    public DbSet<NewsletterCampaign> NewsletterCampaigns => Set<NewsletterCampaign>();
    
    // Gift Cards & Store Credits
    public DbSet<Sparkle.Domain.Marketing.GiftCard> GiftCards => Set<Sparkle.Domain.Marketing.GiftCard>();
    public DbSet<Sparkle.Domain.Marketing.GiftCardTransaction> GiftCardTransactions => Set<Sparkle.Domain.Marketing.GiftCardTransaction>();
    public DbSet<Sparkle.Domain.Marketing.StoreCredit> StoreCredits => Set<Sparkle.Domain.Marketing.StoreCredit>();
    public DbSet<Sparkle.Domain.Marketing.StoreCreditTransaction> StoreCreditTransactions => Set<Sparkle.Domain.Marketing.StoreCreditTransaction>();

    // ==================== SELLER MANAGEMENT ====================
    public DbSet<Seller> Sellers => Set<Seller>();
    public DbSet<SellerPayoutRequest> SellerPayoutRequests => Set<SellerPayoutRequest>();
    public DbSet<SellerDocument> SellerDocuments => Set<SellerDocument>();
    public DbSet<SellerBankAccount> SellerBankAccounts => Set<SellerBankAccount>();
    public DbSet<SellerPayout> SellerPayouts => Set<SellerPayout>();
    public DbSet<SellerPerformanceMetric> SellerPerformanceMetrics => Set<SellerPerformanceMetric>();
    public DbSet<SellerSubscription> SellerSubscriptions => Set<SellerSubscription>();
    public DbSet<SellerFollower> SellerFollowers => Set<SellerFollower>();
    public DbSet<SellerAnalyticsSummary> SellerAnalyticsSummaries => Set<SellerAnalyticsSummary>();
    public DbSet<SellerPerformanceScore> SellerPerformanceScores => Set<SellerPerformanceScore>();

    // ==================== CONNECTIVITY FEATURES ====================
    public DbSet<Sparkle.Domain.System.PlatformAnnouncement> PlatformAnnouncements => Set<Sparkle.Domain.System.PlatformAnnouncement>();
    public DbSet<Sparkle.Domain.System.AnnouncementDismissal> AnnouncementDismissals => Set<Sparkle.Domain.System.AnnouncementDismissal>();
    public DbSet<Sparkle.Domain.System.StoreVisit> StoreVisits => Set<Sparkle.Domain.System.StoreVisit>();
    public DbSet<Sparkle.Domain.System.SellerMessage> SellerMessages => Set<Sparkle.Domain.System.SellerMessage>();
    public DbSet<Sparkle.Domain.System.AdminMessage> AdminMessages => Set<Sparkle.Domain.System.AdminMessage>();
    public DbSet<Sparkle.Domain.System.FavoriteSeller> FavoriteSellers => Set<Sparkle.Domain.System.FavoriteSeller>();

    // ==================== SUPPORT & COMMUNICATION ====================
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<SystemNotification> SystemNotifications => Set<SystemNotification>();
    public DbSet<Sparkle.Domain.Notifications.Notification> Notifications => Set<Sparkle.Domain.Notifications.Notification>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<Report> Reports => Set<Report>();
    
    // ==================== CONFIGURATION ====================
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();
    public DbSet<CommissionConfig> CommissionConfigs => Set<CommissionConfig>();
    public DbSet<Sparkle.Domain.Configuration.EmailTemplate> EmailTemplates => Set<Sparkle.Domain.Configuration.EmailTemplate>();

    // ==================== WALLETS ====================
    public DbSet<Sparkle.Domain.Wallets.UserWallet> UserWallets => Set<Sparkle.Domain.Wallets.UserWallet>();
    public DbSet<Sparkle.Domain.Wallets.SellerWallet> SellerWallets => Set<Sparkle.Domain.Wallets.SellerWallet>();
    public DbSet<Sparkle.Domain.Wallets.WalletTransaction> WalletTransactions => Set<Sparkle.Domain.Wallets.WalletTransaction>();
    public DbSet<Sparkle.Domain.Wallets.WithdrawalRequest> WithdrawalRequests => Set<Sparkle.Domain.Wallets.WithdrawalRequest>();
    public DbSet<Sparkle.Domain.Wallets.LedgerEntry> LedgerEntries => Set<Sparkle.Domain.Wallets.LedgerEntry>();
    public DbSet<Sparkle.Domain.Wallets.AdminWallet> AdminWallets => Set<Sparkle.Domain.Wallets.AdminWallet>();
    public DbSet<Sparkle.Domain.Wallets.AdminTransaction> AdminTransactions => Set<Sparkle.Domain.Wallets.AdminTransaction>();

    // ==================== PHASE 1: AUDIT LOGGING ====================
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // ==================== PHASE 2: LOGISTICS HUB SYSTEM ====================
    public DbSet<Hub> Hubs => Set<Hub>();
    public DbSet<HubInventory> HubInventory => Set<HubInventory>();
    public DbSet<Rider> Riders => Set<Rider>();
    public DbSet<PickupRequest> PickupRequests => Set<PickupRequest>();
    public DbSet<DeliveryAssignment> DeliveryAssignments => Set<DeliveryAssignment>();

    // ==================== PHASE 3: DISPUTES & PENALTIES ====================
    public DbSet<Sparkle.Domain.Support.Dispute> Disputes => Set<Sparkle.Domain.Support.Dispute>();
    public DbSet<Sparkle.Domain.Support.DisputeNote> DisputeNotes => Set<Sparkle.Domain.Support.DisputeNote>();
    public DbSet<Sparkle.Domain.Support.SellerPenalty> SellerPenalties => Set<Sparkle.Domain.Support.SellerPenalty>();

    // ==================== DYNAMIC FORMS SCHEMA ====================
    public DbSet<Sparkle.Domain.DynamicForms.DynamicFormDefinition> DynamicForms => Set<Sparkle.Domain.DynamicForms.DynamicFormDefinition>();
    public DbSet<Sparkle.Domain.DynamicForms.DynamicFormSection> DynamicFormSections => Set<Sparkle.Domain.DynamicForms.DynamicFormSection>();
    public DbSet<Sparkle.Domain.DynamicForms.DynamicFormField> DynamicFormFields => Set<Sparkle.Domain.DynamicForms.DynamicFormField>();
    public DbSet<Sparkle.Domain.DynamicForms.DynamicFieldOption> DynamicFieldOptions => Set<Sparkle.Domain.DynamicForms.DynamicFieldOption>();
    public DbSet<Sparkle.Domain.DynamicForms.DynamicFieldDependency> DynamicFieldDependencies => Set<Sparkle.Domain.DynamicForms.DynamicFieldDependency>();
    public DbSet<Sparkle.Domain.DynamicForms.DynamicFormEntry> DynamicFormEntries => Set<Sparkle.Domain.DynamicForms.DynamicFormEntry>();
    public DbSet<Sparkle.Domain.DynamicForms.DynamicFormValue> DynamicFormValues => Set<Sparkle.Domain.DynamicForms.DynamicFormValue>();
    public DbSet<Sparkle.Domain.DynamicForms.DynamicFormActivityLog> DynamicFormActivityLogs => Set<Sparkle.Domain.DynamicForms.DynamicFormActivityLog>();

    

    // ==================== SHIPPING & LOGISTICS ====================
    public DbSet<DeliveryZone> DeliveryZones => Set<DeliveryZone>();
    public DbSet<DeliveryArea> DeliveryAreas => Set<DeliveryArea>();
    public DbSet<ShippingMethod> ShippingMethods => Set<ShippingMethod>();
    public DbSet<CourierPartner> CourierPartners => Set<CourierPartner>();
    public DbSet<ShippingZone> ShippingZones => Set<ShippingZone>();
    public DbSet<ShippingRate> ShippingRates => Set<ShippingRate>();
    public DbSet<Sparkle.Domain.Orders.Shipment> Shipments => Set<Sparkle.Domain.Orders.Shipment>();
    public DbSet<Sparkle.Domain.Orders.ShipmentItem> ShipmentItems => Set<Sparkle.Domain.Orders.ShipmentItem>();
    public DbSet<Sparkle.Domain.Orders.ShipmentTrackingEvent> ShipmentTrackingEvents => Set<Sparkle.Domain.Orders.ShipmentTrackingEvent>();

    // ==================== ANALYTICS & REPORTS ====================
    public DbSet<ProductView> ProductViews => Set<ProductView>();
    public DbSet<SearchAnalytics> SearchAnalytics => Set<SearchAnalytics>();
    public DbSet<SalesReport> SalesReports => Set<SalesReport>();
    public DbSet<AnalyticsSellerEarning> AnalyticsSellerEarnings => Set<AnalyticsSellerEarning>();
    public DbSet<PlatformMetric> PlatformMetrics => Set<PlatformMetric>();


    public override int SaveChanges()
    {
        ValidateProductRules();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ValidateProductRules();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void ValidateProductRules()
    {
        var productEntries = ChangeTracker.Entries<Product>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in productEntries)
        {
            var product = entry.Entity;
            
            // RULE 1: "Sparkle Star" products (IsAdminProduct = true) must not have regular seller IDs
            // Only admin/system seller can create Sparkle Star products
            if (product.IsAdminProduct && product.SellerId.HasValue)
            {
                // Get seller to check if it's the system admin seller
                var seller = Sellers.Local.FirstOrDefault(v => v.Id == product.SellerId.Value)
                    ?? Sellers.Find(product.SellerId.Value);
                
                // Only allow if this is the Sparkle admin seller (ShopName = "Sparkle Official")
                if (seller == null || seller.ShopName != "Sparkle Official")
                {
                    throw new InvalidOperationException(
                        "Only administrators can create 'Sparkle Star' products. " +
                        "Sellers cannot set IsAdminProduct = true on their products.");
                }
            }
            
            // RULE 2: Regular seller products must have a valid SellerId
            if (!product.IsAdminProduct && !product.SellerId.HasValue)
            {
                throw new InvalidOperationException(
                    "Non-admin products must be associated with a seller. " +
                    "Please set SellerId for this product.");
            }
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HaveColumnType("decimal(18,2)");
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ==================== CATALOG SCHEMA ====================
        builder.Entity<Category>().ToTable("Categories", "catalog");

        builder.Entity<Brand>().ToTable("Brands", "catalog");
        builder.Entity<Product>().ToTable("Products", "catalog");
        builder.Entity<ProductVariant>().ToTable("ProductVariants", "catalog");
        builder.Entity<ProductImage>().ToTable("ProductImages", "catalog");
        builder.Entity<Review>().ToTable("Reviews", "catalog");

        // ==================== USER SCHEMA ====================
        builder.Entity<UserProfile>().ToTable("UserProfiles", "users");
        builder.Entity<UserAddress>().ToTable("UserAddresses", "users");
        builder.Entity<UserWishlistItem>().ToTable("UserWishlistItems", "users");
        builder.Entity<UserSearchHistory>().ToTable("UserSearchHistories", "users");
        builder.Entity<UserNotificationSettings>().ToTable("UserNotificationSettings", "users");
        builder.Entity<UserDevice>().ToTable("UserDevices", "users");
        builder.Entity<UserSearchPreference>().ToTable("UserSearchPreferences", "users");
        builder.Entity<Sparkle.Domain.Users.RecentlyViewedItem>().ToTable("RecentlyViewedItems", "users");

        // ==================== ORDERS SCHEMA ====================
        builder.Entity<Address>().ToTable("Addresses", "orders");
        builder.Entity<Cart>().ToTable("Carts", "orders");
        builder.Entity<CartItem>().ToTable("CartItems", "orders");
        builder.Entity<Wishlist>().ToTable("Wishlists", "orders");
        builder.Entity<WishlistItem>().ToTable("WishlistItems", "orders");
        builder.Entity<Order>().ToTable("Orders", "orders");
        builder.Entity<OrderItem>().ToTable("OrderItems", "orders");
        builder.Entity<OrderTracking>().ToTable("OrderTrackings", "orders");

        // ==================== PAYMENTS SCHEMA ====================
        builder.Entity<PaymentMethod>().ToTable("PaymentMethods", "payments");
        builder.Entity<Transaction>().ToTable("Transactions", "payments");
        builder.Entity<Refund>().ToTable("Refunds", "payments");
        builder.Entity<ReturnRequest>().ToTable("ReturnRequests", "payments");

        // ==================== REVIEWS SCHEMA ====================
        builder.Entity<ProductReview>().ToTable("ProductReviews", "reviews");
        builder.Entity<ReviewImage>().ToTable("ReviewImages", "reviews");
        builder.Entity<ReviewVote>().ToTable("ReviewVotes", "reviews");
        builder.Entity<ReviewVote>()
            .HasOne(rv => rv.ProductReview)
            .WithMany(pr => pr.Votes)
            .HasForeignKey(rv => rv.ProductReviewId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<ReviewVote>()
            .HasOne(rv => rv.User)
            .WithMany()
            .HasForeignKey(rv => rv.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ReviewEditHistory>().ToTable("ReviewEditHistories", "reviews");
        builder.Entity<ReviewEditHistory>()
            .HasOne(h => h.ProductReview)
            .WithMany(r => r.EditHistory)
            .HasForeignKey(h => h.ProductReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ProductQuestion>().ToTable("ProductQuestions", "reviews");
        builder.Entity<QuestionAnswer>().ToTable("QuestionAnswers", "reviews");

        // ==================== MARKETING SCHEMA ====================
        builder.Entity<Coupon>().ToTable("Coupons", "marketing");
        builder.Entity<VoucherUsage>().ToTable("VoucherUsages", "marketing");
        builder.Entity<FlashDeal>().ToTable("FlashDeals", "marketing");
        builder.Entity<FlashDealProduct>().ToTable("FlashDealProducts", "marketing");
        builder.Entity<Campaign>().ToTable("Campaigns", "marketing");
        builder.Entity<CampaignProduct>().ToTable("CampaignProducts", "marketing");
        builder.Entity<CampaignCategory>().ToTable("CampaignCategories", "marketing");
        builder.Entity<Banner>().ToTable("Banners", "marketing");
        builder.Entity<EmailSubscription>().ToTable("EmailSubscriptions", "marketing");
        builder.Entity<NewsletterCampaign>().ToTable("NewsletterCampaigns", "marketing");

        // ==================== SELLERS SCHEMA ====================
        builder.Entity<Seller>().ToTable("Sellers", "sellers");
        builder.Entity<SellerPayoutRequest>().ToTable("SellerPayoutRequests", "sellers");
        builder.Entity<SellerDocument>().ToTable("SellerDocuments", "sellers");
        builder.Entity<SellerBankAccount>().ToTable("SellerBankAccounts", "sellers");
        builder.Entity<SellerPayout>().ToTable("SellerPayouts", "sellers");
        builder.Entity<SellerPerformanceMetric>().ToTable("SellerPerformanceMetrics", "sellers");
        builder.Entity<SellerSubscription>().ToTable("SellerSubscriptions", "sellers");
        builder.Entity<SellerFollower>().ToTable("SellerFollowers", "sellers");
        builder.Entity<SellerAnalyticsSummary>().ToTable("SellerAnalyticsSummaries", "sellers");
        builder.Entity<SellerPerformanceScore>().ToTable("SellerPerformanceScores", "sellers");
        
        builder.Entity<Seller>()
            .HasIndex(s => s.ShopName);

        // ==================== SUPPORT SCHEMA ====================
        builder.Entity<SupportTicket>().ToTable("SupportTickets", "support");
        builder.Entity<TicketMessage>().ToTable("TicketMessages", "support");
        builder.Entity<Chat>().ToTable("Chats", "support");
        builder.Entity<ChatMessage>().ToTable("ChatMessages", "support");
        builder.Entity<Report>().ToTable("Reports", "support");

        // ==================== SHIPPING SCHEMA ====================
        builder.Entity<ShippingMethod>().ToTable("ShippingMethods", "shipping");
        builder.Entity<CourierPartner>().ToTable("CourierPartners", "shipping");
        builder.Entity<ShippingZone>().ToTable("ShippingZones", "shipping");
        builder.Entity<ShippingRate>().ToTable("ShippingRates", "shipping");
        builder.Entity<Sparkle.Domain.Orders.Shipment>().ToTable("Shipments", "shipping");
        builder.Entity<Sparkle.Domain.Orders.ShipmentItem>().ToTable("ShipmentItems", "shipping");
        builder.Entity<Sparkle.Domain.Orders.ShipmentTrackingEvent>().ToTable("ShipmentTrackingEvents", "shipping");

        // ==================== ANALYTICS SCHEMA ====================
        builder.Entity<ProductView>().ToTable("ProductViews", "analytics");
        builder.Entity<SearchAnalytics>().ToTable("SearchAnalytics", "analytics");
        builder.Entity<SalesReport>().ToTable("SalesReports", "analytics");
        builder.Entity<AnalyticsSellerEarning>().ToTable("SellerEarnings", "analytics");
        builder.Entity<PlatformMetric>().ToTable("PlatformMetrics", "analytics");

        // ==================== CONTENT & HOMEPAGE SECTIONS SCHEMA ====================
        builder.Entity<Sparkle.Domain.Content.HomepageSection>().ToTable("HomepageSections", "content");
        builder.Entity<Sparkle.Domain.Content.HomepageSection>().HasIndex(hs => hs.Slug).IsUnique();
        builder.Entity<Sparkle.Domain.Content.HomepageSectionProduct>().ToTable("HomepageSectionProducts", "content");
        builder.Entity<Sparkle.Domain.Content.HomepageSectionCategory>().ToTable("HomepageSectionCategories", "content");
        builder.Entity<Sparkle.Domain.Content.TrendingProductSuggestion>().ToTable("TrendingProductSuggestions", "content");
        builder.Entity<Sparkle.Domain.Content.TrendingProductSuggestion>().HasIndex(t => new { t.ProductId, t.CalculatedAt });
        builder.Entity<Sparkle.Domain.Content.FlashSaleProductSuggestion>().ToTable("FlashSaleProductSuggestions", "content");
        builder.Entity<Sparkle.Domain.Content.FlashSaleProductSuggestion>().HasIndex(f => new { f.ProductId, f.CalculatedAt });
        builder.Entity<Sparkle.Domain.Content.UserBehaviorAnalytic>().ToTable("UserBehaviorAnalytics", "content");
        builder.Entity<Sparkle.Domain.Content.UserBehaviorAnalytic>().HasIndex(u => new { u.UserId, u.ActionDateTime });
        builder.Entity<Sparkle.Domain.Content.UserBehaviorAnalytic>().HasIndex(u => new { u.ProductId, u.ActionDateTime });
        builder.Entity<Sparkle.Domain.Content.SalesMetricsSnapshot>().ToTable("SalesMetricsSnapshots", "content");
        builder.Entity<Sparkle.Domain.Content.SalesMetricsSnapshot>().HasIndex(s => new { s.ProductId, s.SnapshotDateTime });
        builder.Entity<Sparkle.Domain.Content.HomepageSectionAuditLog>().ToTable("HomepageSectionAuditLogs", "content");
        builder.Entity<Sparkle.Domain.Content.HomepageSectionAuditLog>().HasIndex(a => new { a.SectionId, a.ChangedAt });

        // ==================== SYSTEM SCHEMA ====================
        builder.Entity<SystemNotification>().ToTable("Notifications", "system");
        builder.Entity<ActivityLog>().ToTable("ActivityLogs", "system");
        builder.Entity<SiteSetting>().ToTable("SiteSettings", "system");
        builder.Entity<CommissionConfig>().ToTable("CommissionConfigs", "system");
        builder.Entity<Sparkle.Domain.Configuration.EmailTemplate>().ToTable("EmailTemplates", "system");

        // ==================== WALLETS SCHEMA ====================
        builder.Entity<Sparkle.Domain.Wallets.UserWallet>().ToTable("UserWallets", "wallets");
        builder.Entity<Sparkle.Domain.Wallets.SellerWallet>().ToTable("SellerWallets", "wallets");
        builder.Entity<Sparkle.Domain.Wallets.WalletTransaction>().ToTable("WalletTransactions", "wallets");
        builder.Entity<Sparkle.Domain.Wallets.WithdrawalRequest>().ToTable("WithdrawalRequests", "wallets");
        builder.Entity<Sparkle.Domain.Wallets.LedgerEntry>().ToTable("LedgerEntries", "wallets");
        builder.Entity<Sparkle.Domain.Wallets.AdminWallet>().ToTable("AdminWallets", "wallets");
        builder.Entity<Sparkle.Domain.Wallets.AdminTransaction>().ToTable("AdminTransactions", "wallets");
        
        // Wallet indexes for performance
        builder.Entity<Sparkle.Domain.Wallets.WalletTransaction>()
            .HasIndex(wt => new { wt.SellerId, wt.TransactionDate });
        
        builder.Entity<Sparkle.Domain.Wallets.WalletTransaction>()
            .HasIndex(wt => new { wt.ReferenceType, wt.ReferenceId });

        builder.Entity<Sparkle.Domain.Wallets.LedgerEntry>()
            .HasIndex(le => le.OrderId);

        builder.Entity<Sparkle.Domain.Wallets.LedgerEntry>()
            .HasIndex(le => le.EntryDate);

        builder.Entity<Sparkle.Domain.Wallets.AdminTransaction>()
            .HasIndex(at => at.TransactionDate);

        // ==================== PHASE 1: AUDIT LOGGING SCHEMA ====================
        builder.Entity<AuditLog>().ToTable("AuditLogs", "system");
        builder.Entity<AuditLog>().HasIndex(a => new { a.UserId, a.Timestamp });
        builder.Entity<AuditLog>().HasIndex(a => new { a.EntityType, a.EntityId });

        // ==================== PHASE 2: LOGISTICS HUB SCHEMA ====================
        builder.Entity<Hub>().ToTable("Hubs", "logistics");
        builder.Entity<Hub>().HasIndex(h => h.HubCode).IsUnique();
        builder.Entity<HubInventory>().ToTable("HubInventory", "logistics");
        builder.Entity<Rider>().ToTable("Riders", "logistics");
        builder.Entity<Rider>().HasIndex(r => r.RiderCode).IsUnique();
        builder.Entity<PickupRequest>().ToTable("PickupRequests", "logistics");
        builder.Entity<PickupRequest>().HasIndex(p => p.PickupNumber).IsUnique();
        builder.Entity<DeliveryAssignment>().ToTable("DeliveryAssignments", "logistics");
        builder.Entity<DeliveryAssignment>().HasIndex(d => d.DeliveryNumber).IsUnique();

        // ==================== PHASE 3: DISPUTES SCHEMA ====================
        builder.Entity<Sparkle.Domain.Support.Dispute>().ToTable("Disputes", "support");
        builder.Entity<Sparkle.Domain.Support.Dispute>().HasIndex(d => d.DisputeNumber).IsUnique();
        builder.Entity<Sparkle.Domain.Support.DisputeNote>().ToTable("DisputeNotes", "support");
        builder.Entity<Sparkle.Domain.Support.SellerPenalty>().ToTable("SellerPenalties", "sellers");

        // Disputes - Fix multiple cascade paths
        // Disputes -> Orders -> User + Disputes -> User = CONFLICT
        builder.Entity<Sparkle.Domain.Support.Dispute>()
            .HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.Entity<Sparkle.Domain.Support.Dispute>()
            .HasOne(d => d.Order)
            .WithMany()
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
        // ==================== DYNAMIC FORMS SCHEMA ====================
        builder.Entity<Sparkle.Domain.DynamicForms.DynamicFormDefinition>().ToTable("FormMaster", "dynamic");
        builder.Entity<Sparkle.Domain.DynamicForms.DynamicFormSection>().ToTable("FormSectionMaster", "dynamic");
        builder.Entity<Sparkle.Domain.DynamicForms.DynamicFormField>().ToTable("FormFieldMaster", "dynamic");
        builder.Entity<Sparkle.Domain.DynamicForms.DynamicFieldOption>().ToTable("FormFieldOptions", "dynamic");
        builder.Entity<Sparkle.Domain.DynamicForms.DynamicFieldDependency>().ToTable("FieldDependencies", "dynamic");
        builder.Entity<Sparkle.Domain.DynamicForms.DynamicFormEntry>().ToTable("FormData", "dynamic");
        builder.Entity<Sparkle.Domain.DynamicForms.DynamicFormValue>().ToTable("FormDataFieldValues", "dynamic");
        builder.Entity<Sparkle.Domain.DynamicForms.DynamicFormActivityLog>().ToTable("ActivityLog", "dynamic");

        // Configuration for Dynamic Forms Relationships to avoid Cascade Cycles
        builder.Entity<Sparkle.Domain.DynamicForms.DynamicFieldDependency>()
            .HasOne(d => d.Field)
            .WithMany(f => f.Dependencies)
            .HasForeignKey(d => d.FieldId)
            .OnDelete(DeleteBehavior.Cascade); // Delete dependency if child field is deleted

        builder.Entity<Sparkle.Domain.DynamicForms.DynamicFieldDependency>()
            .HasOne(d => d.DependsOnField)
            .WithMany()
            .HasForeignKey(d => d.DependsOnFieldId)
            .OnDelete(DeleteBehavior.Restrict); // Don't delete dependency if parent field is deleted (requires manual cleanup or triggers)

        builder.Entity<Sparkle.Domain.DynamicForms.DynamicFormValue>()
            .HasOne(v => v.Entry)
            .WithMany(e => e.Values)
            .HasForeignKey(v => v.EntryId)
            .OnDelete(DeleteBehavior.Cascade); // Delete values if entry is deleted

        builder.Entity<Sparkle.Domain.DynamicForms.DynamicFormValue>()
            .HasOne(v => v.Field)
            .WithMany()
            .HasForeignKey(v => v.FieldId)
            .OnDelete(DeleteBehavior.Restrict); // Don't auto-delete values if field definition is deleted




        // ==================== CONFIGURE RELATIONSHIPS & INDEXES ====================
        
        // Product - Seller relationship
        builder.Entity<Product>()
            .HasOne(p => p.Seller)
            .WithMany()
            .HasForeignKey(p => p.SellerId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Product - Category relationship
        builder.Entity<Product>()
            .HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // ProductVariant - Product relationship
        builder.Entity<ProductVariant>()
            .HasOne(pv => pv.Product)
            .WithMany(p => p.Variants)
            .HasForeignKey(pv => pv.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // ProductImage - Product relationship
        builder.Entity<ProductImage>()
            .HasOne(pi => pi.Product)
            .WithMany(p => p.Images)
            .HasForeignKey(pi => pi.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Review - Product relationship
        builder.Entity<Review>()
            .HasOne(r => r.Product)
            .WithMany(p => p.Reviews)
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // OrderItem - ProductVariant relationship
        builder.Entity<OrderItem>()
            .HasOne(oi => oi.ProductVariant)
            .WithMany()
            .HasForeignKey(oi => oi.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Cart relationship configuration
        builder.Entity<Cart>()
            .HasMany(c => c.Items)
            .WithOne(ci => ci.Cart)
            .HasForeignKey(ci => ci.CartId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Wishlist relationship configuration
        builder.Entity<Wishlist>()
            .HasMany(w => w.Items)
            .WithOne(wi => wi.Wishlist)
            .HasForeignKey(wi => wi.WishlistId)
            .OnDelete(DeleteBehavior.Cascade);

        // Performance Indexes for Foreign Keys
        builder.Entity<CartItem>().HasIndex(ci => ci.CartId);
        builder.Entity<WishlistItem>().HasIndex(wi => wi.WishlistId);
        builder.Entity<OrderItem>().HasIndex(oi => oi.OrderId);
        builder.Entity<OrderItem>().HasIndex(oi => oi.ProductVariantId);
        builder.Entity<ProductVariant>().HasIndex(pv => pv.ProductId);
        builder.Entity<ProductImage>().HasIndex(pi => pi.ProductId);
        builder.Entity<Review>().HasIndex(r => r.ProductId);
        builder.Entity<UserAddress>().HasIndex(ua => ua.UserId);
        builder.Entity<UserWishlistItem>().HasIndex(uwi => uwi.UserId);

        // Order relationship configuration
        builder.Entity<Order>()
            .HasMany(o => o.OrderItems)
            .WithOne(oi => oi.Order)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Ignore backward compatibility properties
        builder.Entity<Order>().Ignore(o => o.Items);
        builder.Entity<Order>().Ignore(o => o.Subtotal);
        builder.Entity<Order>().Ignore(o => o.DiscountTotal);
        builder.Entity<Order>().Ignore(o => o.ShippingFee);
        builder.Entity<Order>().Ignore(o => o.Total);
        builder.Entity<Order>().Ignore(o => o.CreatedAt);
        
        // Order indexes for performance
        builder.Entity<Order>()
            .HasIndex(o => o.OrderNumber)
            .IsUnique();
        
        builder.Entity<Order>()
            .HasIndex(o => new { o.UserId, o.OrderDate });
        
        builder.Entity<Order>()
            .HasIndex(o => o.Status);

        // Transaction indexes
        builder.Entity<Transaction>()
            .HasIndex(t => t.TransactionNumber)
            .IsUnique();
        
        builder.Entity<Transaction>()
            .HasIndex(t => new { t.UserId, t.TransactionDate });

        // ProductView indexes for analytics
        builder.Entity<ProductView>()
            .HasIndex(pv => new { pv.ProductId, pv.ViewedAt });
        
        builder.Entity<ProductView>()
            .HasIndex(pv => pv.UserId);

        // SearchAnalytics indexes
        builder.Entity<SearchAnalytics>()
            .HasIndex(sa => sa.SearchQuery);
        
        builder.Entity<SearchAnalytics>()
            .HasIndex(sa => sa.SearchedAt);

        // Review indexes
        builder.Entity<ProductReview>()
            .HasIndex(r => new { r.ProductId, r.Status });
        
        builder.Entity<ProductReview>()
            .HasIndex(r => r.UserId);

        // Coupon code unique index
        builder.Entity<Coupon>()
            .HasIndex(c => c.Code)
            .IsUnique();

        // Activity Log indexes
        builder.Entity<ActivityLog>()
            .HasIndex(al => new { al.UserId, al.Timestamp });
        
        builder.Entity<ActivityLog>()
            .HasIndex(al => new { al.EntityType, al.EntityId });
        
        // ==================== FIX CASCADE DELETE CONFLICTS ====================
        
        // VoucherUsages - Fix multiple cascade paths
        // VoucherUsages -> User (CASCADE) + VoucherUsages -> Orders -> User (CASCADE) = CONFLICT
        // Solution: Set VoucherUsages -> User to RESTRICT using navigation properties
        builder.Entity<VoucherUsage>()
            .HasOne(vu => vu.User)
            .WithMany()
            .HasForeignKey(vu => vu.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // VoucherUsages -> Orders set to RESTRICT to avoid cascade conflict
        builder.Entity<VoucherUsage>()
            .HasOne(vu => vu.Order)
            .WithMany()
            .HasForeignKey(vu => vu.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // VoucherUsages -> Coupons set to RESTRICT
        builder.Entity<VoucherUsage>()
            .HasOne(vu => vu.Coupon)
            .WithMany(c => c.UsageHistory)
            .HasForeignKey(vu => vu.CouponId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // TicketMessages - Fix multiple cascade paths
        // TicketMessages -> User (CASCADE) + TicketMessages -> SupportTickets -> User (CASCADE) = CONFLICT
        builder.Entity<TicketMessage>()
            .HasOne(tm => tm.User)
            .WithMany()
            .HasForeignKey(tm => tm.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.Entity<TicketMessage>()
            .HasOne(tm => tm.SupportTicket)
            .WithMany(st => st.Messages)
            .HasForeignKey(tm => tm.SupportTicketId)
            .OnDelete(DeleteBehavior.Cascade); // Keep cascade for ticket deletion
        
        // QuestionAnswers - Fix multiple cascade paths
        // QuestionAnswers -> User (CASCADE) + QuestionAnswers -> ProductQuestions -> User (CASCADE) = CONFLICT
        builder.Entity<QuestionAnswer>()
            .HasOne(qa => qa.User)
            .WithMany()
            .HasForeignKey(qa => qa.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.Entity<QuestionAnswer>()
            .HasOne(qa => qa.ProductQuestion)
            .WithMany(pq => pq.Answers)
            .HasForeignKey(qa => qa.ProductQuestionId)
            .OnDelete(DeleteBehavior.Cascade); // Keep cascade for question deletion
        
        // Refunds - Fix multiple cascade paths
        // Refunds -> User (CASCADE) + Refunds -> Orders -> User (CASCADE) = CONFLICT
        builder.Entity<Refund>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.Entity<Refund>()
            .HasOne(r => r.Order)
            .WithMany()
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Restrict); // Don't cascade delete refunds
        
        // ReturnRequests - Fix cascade conflicts
        // ReturnRequests -> User + ReturnRequests -> Order -> User = CONFLICT  
        // ReturnRequests -> OrderItem -> Order + ReturnRequests -> Order = CONFLICT
        builder.Entity<ReturnRequest>()
            .HasOne(rr => rr.User)
            .WithMany()
            .HasForeignKey(rr => rr.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // SellerEarnings - Fix cascade conflicts
        // SellerEarnings -> OrderItem -> Order + SellerEarnings -> Order = CONFLICT
        builder.Entity<Sparkle.Domain.System.SellerEarning>() // Mapped to AnalyticsSellerEarning
            .HasOne(ve => ve.OrderItem)
            .WithMany()
            .HasForeignKey(ve => ve.OrderItemId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.Entity<Sparkle.Domain.System.SellerEarning>()
            .HasOne(ve => ve.Order)
            .WithMany()
            .HasForeignKey(ve => ve.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.Entity<ReturnRequest>()
            .HasOne(rr => rr.Order)
            .WithMany()
            .HasForeignKey(rr => rr.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.Entity<ReturnRequest>()
            .HasOne(rr => rr.OrderItem)
            .WithMany()
            .HasForeignKey(rr => rr.OrderItemId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // SellerMessages - Fix multiple cascade paths to Sellers
        builder.Entity<Sparkle.Domain.System.SellerMessage>()
            .HasOne(sm => sm.SenderSeller)
            .WithMany()
            .HasForeignKey(sm => sm.SenderSellerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Sparkle.Domain.System.SellerMessage>()
            .HasOne(sm => sm.ReceiverSeller)
            .WithMany()
            .HasForeignKey(sm => sm.ReceiverSellerId)
            .OnDelete(DeleteBehavior.Restrict);


        // ShipmentItems - Fix cascade conflicts
        // ShipmentItems -> Shipment -> Order -> User + ShipmentItems -> OrderItem -> Order -> User = CONFLICT
        builder.Entity<Sparkle.Domain.Orders.ShipmentItem>()
            .HasOne(si => si.Shipment)
            .WithMany(s => s.Items)
            .HasForeignKey(si => si.ShipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // ==================== CHAT RELATIONSHIPS ====================
        // Chat -> User (restrict to avoid cascade conflict)
        builder.Entity<Chat>()
            .HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.Entity<Chat>()
            .HasOne(c => c.Seller)
            .WithMany()
            .HasForeignKey(c => c.SellerId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.Entity<Chat>()
            .HasOne(c => c.Product)
            .WithMany()
            .HasForeignKey(c => c.ProductId)
            .OnDelete(DeleteBehavior.SetNull);
        
        builder.Entity<Chat>()
            .HasMany(c => c.Messages)
            .WithOne(m => m.Chat)
            .HasForeignKey(m => m.ChatId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // ChatMessage -> Sender (restrict to avoid cascade conflict)
        builder.Entity<ChatMessage>()
            .HasOne(cm => cm.Sender)
            .WithMany()
            .HasForeignKey(cm => cm.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Chat indexes for performance
        builder.Entity<Chat>()
            .HasIndex(c => new { c.UserId, c.SellerId });
        
        builder.Entity<Chat>()
            .HasIndex(c => c.LastMessageAt);
        
        builder.Entity<ChatMessage>()
            .HasIndex(cm => cm.SentAt);

        // ==================== REPORT RELATIONSHIPS ====================
        builder.Entity<Report>()
            .HasOne(r => r.Reporter)
            .WithMany()
            .HasForeignKey(r => r.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Report>()
            .HasOne(r => r.Product)
            .WithMany()
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Report>()
            .HasOne(r => r.Seller)
            .WithMany()
            .HasForeignKey(r => r.SellerId)
            .OnDelete(DeleteBehavior.SetNull);

        // ==================== HOMEPAGE SECTIONS RELATIONSHIPS ====================
        // HomepageSection relationships
        builder.Entity<Sparkle.Domain.Content.HomepageSection>()
            .HasOne(hs => hs.CreatedByUser)
            .WithMany()
            .HasForeignKey(hs => hs.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
        
        builder.Entity<Sparkle.Domain.Content.HomepageSection>()
            .HasMany(hs => hs.ManualProducts)
            .WithOne(hp => hp.Section)
            .HasForeignKey(hp => hp.SectionId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<Sparkle.Domain.Content.HomepageSection>()
            .HasMany(hs => hs.RelatedCategories)
            .WithOne(hc => hc.Section)
            .HasForeignKey(hc => hc.SectionId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<Sparkle.Domain.Content.HomepageSection>()
            .HasMany(hs => hs.AuditLogs)
            .WithOne(al => al.Section)
            .HasForeignKey(al => al.SectionId)
            .OnDelete(DeleteBehavior.Cascade);

        // HomepageSectionProduct relationships
        builder.Entity<Sparkle.Domain.Content.HomepageSectionProduct>()
            .HasOne(hp => hp.Product)
            .WithMany()
            .HasForeignKey(hp => hp.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // HomepageSectionCategory relationships
        builder.Entity<Sparkle.Domain.Content.HomepageSectionCategory>()
            .HasOne(hc => hc.Category)
            .WithMany()
            .HasForeignKey(hc => hc.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // TrendingProductSuggestion relationships
        builder.Entity<Sparkle.Domain.Content.TrendingProductSuggestion>()
            .HasOne(t => t.Product)
            .WithMany()
            .HasForeignKey(t => t.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // FlashSaleProductSuggestion relationships
        builder.Entity<Sparkle.Domain.Content.FlashSaleProductSuggestion>()
            .HasOne(f => f.Product)
            .WithMany()
            .HasForeignKey(f => f.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // UserBehaviorAnalytic relationships
        builder.Entity<Sparkle.Domain.Content.UserBehaviorAnalytic>()
            .HasOne(u => u.User)
            .WithMany()
            .HasForeignKey(u => u.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.Entity<Sparkle.Domain.Content.UserBehaviorAnalytic>()
            .HasOne(u => u.Product)
            .WithMany()
            .HasForeignKey(u => u.ProductId)
            .OnDelete(DeleteBehavior.SetNull);
        
        builder.Entity<Sparkle.Domain.Content.UserBehaviorAnalytic>()
            .HasOne(u => u.Category)
            .WithMany()
            .HasForeignKey(u => u.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        // SalesMetricsSnapshot relationships
        builder.Entity<Sparkle.Domain.Content.SalesMetricsSnapshot>()
            .HasOne(s => s.Product)
            .WithMany()
            .HasForeignKey(s => s.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // HomepageSectionAuditLog relationships
        builder.Entity<Sparkle.Domain.Content.HomepageSectionAuditLog>()
            .HasOne(al => al.ChangedByUser)
            .WithMany()
            .HasForeignKey(al => al.ChangedByUserId)
            .OnDelete(DeleteBehavior.SetNull);    }
}