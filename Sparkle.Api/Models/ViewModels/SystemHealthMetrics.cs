namespace Sparkle.Api.Models.ViewModels;

public class SystemHealthMetrics
{
    public string DatabaseStatus { get; set; } = "Unknown";
    public int TotalUsers { get; set; }
    public int TotalOrders { get; set; }
    public int TotalProducts { get; set; }
    public int TotalSellers { get; set; }
    public TimeSpan ServerUptime { get; set; }
    public double MemoryUsage { get; set; }
    public double MemoryUsedMB { get; set; }
    public double GCMemoryMB { get; set; }
    public double CpuUsage { get; set; }
    
    // Additional real-time metrics
    public int PendingOrders { get; set; }
    public int TodayOrders { get; set; }
    public int ActiveSellers { get; set; }
    public int OpenTickets { get; set; }
}
