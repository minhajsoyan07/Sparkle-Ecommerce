using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sparkle.Domain.Logistics
{
    public class DeliveryZone
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty; // e.g., "Inside Dhaka", "Dhaka Suburbs"

        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseCharge { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MaxWeightKg { get; set; } = 1.0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ExtraChargePerKg { get; set; }

        public string EstimatedDeliveryTime { get; set; } = "2-3 Days";

        public bool IsActive { get; set; } = true;
    }

    public class DeliveryArea
    {
        [Key]
        public int Id { get; set; }

        public int ZoneId { get; set; }
        [ForeignKey("ZoneId")]
        public virtual DeliveryZone Zone { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty; // e.g., "Mirpur 10"

        [MaxLength(50)]
        public string? District { get; set; } // e.g., "Dhaka"

        [MaxLength(20)]
        public string? PostCode { get; set; }
    }
}
