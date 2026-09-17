using Sparkle.Domain.Logistics;
using System.Collections.Generic;

namespace Sparkle.Infrastructure.Services
{
    public class LocationSeedingService
    {
        public List<DeliveryZone> GetZones()
        {
            return new List<DeliveryZone>
            {
                new DeliveryZone { Name = "Inside Dhaka", BaseCharge = 60, MaxWeightKg = 1, ExtraChargePerKg = 20, EstimatedDeliveryTime = "1-2 Days", IsActive = true },
                new DeliveryZone { Name = "Dhaka Suburbs", BaseCharge = 100, MaxWeightKg = 1, ExtraChargePerKg = 30, EstimatedDeliveryTime = "2-3 Days", IsActive = true },
                new DeliveryZone { Name = "Outside Dhaka", BaseCharge = 130, MaxWeightKg = 1, ExtraChargePerKg = 50, EstimatedDeliveryTime = "3-5 Days", IsActive = true }
            };
        }

        public List<DeliveryArea> GetDhakaAreas()
        {
            // Simplified list for Inside Dhaka (ZoneId to be assigned by caller)
            var areas = new List<string>
            {
                "Adabor", "Badda", "Banani", "Bangshal", "Bhashantek", "Cantonment", "Chawkbazar", 
                "Dakshinkhan", "Darus Salam", "Demra", "Dhanmondi", "Gendaria", "Gulshan", "Hazaribagh", 
                "Jatrabari", "Kafrul", "Kalabagan", "Kamrangirchar", "Khilgaon", "Khilkhet", "Kotwali", 
                "Lalbagh", "Mirpur 1", "Mirpur 2", "Mirpur 10", "Mirpur 11", "Mirpur 12", "Mirpur 14", 
                "Mohammadpur", "Motijheel", "New Market", "Pallabi", "Paltan", "Ramna", "Rampura", 
                "Sabujbagh", "Shah Ali", "Shahbag", "Sher-e-Bangla Nagar", "Shyampur", "Sutrapur", 
                "Tejgaon", "Turag", "Uttara Sector 1-14", "Uttar Khan", "Vhatara", "Wari"
            };

            var deliveryAreas = new List<DeliveryArea>();
            foreach (var area in areas)
            {
                deliveryAreas.Add(new DeliveryArea 
                { 
                    Name = area, 
                    District = "Dhaka", 
                    PostCode = "1200" // Placeholder, ideally specific
                });
            }
            return deliveryAreas;
        }

        public List<DeliveryArea> GetDhakaSuburbs()
        {
            var areas = new List<string> { "Savar", "Keraniganj", "Dohar", "Nawabganj", "Dhamrai", "Gazipur Sadar", "Narayanganj Sadar" };
            var deliveryAreas = new List<DeliveryArea>();
            foreach (var area in areas)
            {
                deliveryAreas.Add(new DeliveryArea 
                { 
                    Name = area, 
                    District = "Dhaka",
                    PostCode = "1300"
                });
            }
            return deliveryAreas;
        }
    }
}
