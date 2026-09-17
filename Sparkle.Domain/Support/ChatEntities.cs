using Sparkle.Domain.Common;
using Sparkle.Domain.Identity;
using Sparkle.Domain.Sellers;
using Sparkle.Domain.Catalog;

namespace Sparkle.Domain.Support;

/// <summary>
/// Represents a conversation between a User and a Seller.
/// A user can initiate a chat with a seller before making a purchase.
/// </summary>
public class Chat : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public int SellerId { get; set; }
    public Seller Seller { get; set; } = null!;
    
    /// <summary>
    /// Optional: Product that initiated the conversation
    /// </summary>
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    
    /// <summary>
    /// Subject/title of the conversation
    /// </summary>
    public string? Subject { get; set; }
    
    /// <summary>
    /// Status of the chat: Active, Closed, Archived
    /// </summary>
    public string Status { get; set; } = "Active";
    
    /// <summary>
    /// Timestamp when the chat was started
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Last activity timestamp for sorting
    /// </summary>
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Count of unread messages for the user
    /// </summary>
    public int UserUnreadCount { get; set; }
    
    /// <summary>
    /// Count of unread messages for the seller
    /// </summary>
    public int SellerUnreadCount { get; set; }
    
    /// <summary>
    /// Collection of messages in this chat
    /// </summary>
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

/// <summary>
/// Represents a single message in a Chat conversation.
/// </summary>
public class ChatMessage : BaseEntity
{
    public int ChatId { get; set; }
    public Chat Chat { get; set; } = null!;
    
    /// <summary>
    /// The sender of the message (User or Seller's UserId)
    /// </summary>
    public string SenderId { get; set; } = string.Empty;
    public ApplicationUser Sender { get; set; } = null!;
    
    /// <summary>
    /// True if sent by seller, false if sent by user
    /// </summary>
    public bool IsSeller { get; set; }
    
    /// <summary>
    /// Message content (text)
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// Message type: Text, Image, File, System
    /// </summary>
    public string MessageType { get; set; } = "Text";
    
    /// <summary>
    /// Optional attachment URL (for images/files)
    /// </summary>
    public string? AttachmentUrl { get; set; }
    
    /// <summary>
    /// Original filename if attachment was uploaded
    /// </summary>
    public string? AttachmentName { get; set; }
    
    /// <summary>
    /// Timestamp when the message was sent
    /// </summary>
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Timestamp when the message was read by recipient
    /// </summary>
    public DateTime? ReadAt { get; set; }
    
    /// <summary>
    /// Whether the message has been read
    /// </summary>
    public bool IsRead { get; set; }
    
    /// <summary>
    /// Whether the message was deleted (soft delete for everyone)
    /// </summary>
    public new bool IsDeleted { get; set; }
    
    /// <summary>
    /// If set, the message was deleted only for this user (delete for me).
    /// Values: null (visible), SenderId (deleted for sender only), "everyone" (deleted for everyone).
    /// </summary>
    public string? DeletedFor { get; set; }
    
    /// <summary>
    /// Timestamp when the message was deleted
    /// </summary>
    public new DateTime? DeletedAt { get; set; }
    
    /// <summary>
    /// Whether the message has been edited
    /// </summary>
    public bool IsEdited { get; set; }
    
    /// <summary>
    /// Timestamp when the message was last edited
    /// </summary>
    public DateTime? EditedAt { get; set; }
}
