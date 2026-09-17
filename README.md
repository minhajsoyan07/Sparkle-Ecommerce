# 🌟 Sparkle E-Commerce

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-MVC%20%26%20API-purple?logo=dotnet)](https://dotnet.microsoft.com/apps/aspnet)
[![Entity Framework Core](https://img.shields.io/badge/EF%20Core-8.0-blue)](https://docs.microsoft.com/ef/core/)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC292B?logo=microsoftsqlserver&logoColor=white)](https://www.microsoft.com/sql-server)
[![Tailwind CSS](https://img.shields.io/badge/Tailwind%20CSS-v4-06B6D4?logo=tailwindcss&logoColor=white)](https://tailwindcss.com/)
[![Bootstrap](https://img.shields.io/badge/Bootstrap-5.3-7952B3?logo=bootstrap&logoColor=white)](https://getbootstrap.com/)
[![SignalR](https://img.shields.io/badge/SignalR-Real--Time-orange)](https://dotnet.microsoft.com/apps/aspnet/signalr)
[![License](https://img.shields.io/badge/License-ISC-green.svg)](LICENSE)

A modern, enterprise-grade multi-vendor e-commerce platform built with **ASP.NET Core 8**, **Entity Framework Core**, and **SQL Server**. Featuring a multi-tenant role system (Admin, Seller, Customer), real-time messaging, logistics and payment integrations, smart product search, bilingual support (English & Bangla), and an automated database initialization engine.

---

## 📑 Table of Contents

- [Key Features](#-key-features)
  - [Customer Experience](#-customer-experience)
  - [Seller Portal](#-seller-portal)
  - [Admin Dashboard](#-admin-dashboard)
  - [Integrations & Logistics](#-integrations--logistics)
- [Architecture & Tech Stack](#-architecture--tech-stack)
- [Project Structure](#-project-structure)
- [Getting Started](#-getting-started)
  - [Prerequisites](#prerequisites)
  - [Installation & Setup](#installation--setup)
  - [Running the Project](#running-the-project)
- [Default Login Credentials](#-default-login-credentials)
- [Configuration](#-configuration)
- [Contributing](#-contributing)
- [License](#-license)

---

## 🚀 Key Features

### 🛍️ Customer Experience
- **Bilingual Interface:** Toggle between English and Bangla (`bn` / `en`) with native typography support.
- **Smart Product Search:** Instant search with fuzzy suggestions, category filtering, and price range sorting.
- **Dynamic Homepage:** Automated & customizable showcase sections (Featured, New Arrivals, Flash Deals, Top Discounts).
- **Cart & Checkout:** Persistent user shopping carts, guest checkout support, and flexible shipping address management.
- **Customer Dashboard:** Order tracking, invoice downloads (via QuestPDF), wishlist, and transaction history.
- **Ratings & Reviews:** Verified customer reviews with image uploads and seller feedback loops.
- **Real-Time Support:** Live chat customer service powered by **SignalR**.

### 🏪 Seller Portal
- **Independent Shop Management:** Custom store profiles, banners, logos, and business registration details.
- **Inventory & Catalog:** Create, edit, and categorize products with multi-image upload support.
- **Order Fulfillment:** Live order lifecycle tracking (Pending, Processing, Shipped, Delivered, Cancelled).
- **Financial Analytics:** Seller wallet, earnings breakdown, commission tracking, and withdrawal requests.
- **Performance Scoring:** Seller quality score based on fulfillment speed and customer reviews.

### 👑 Admin Dashboard
- **Centralized Platform Control:** Manage products, verify sellers, and moderate customer accounts.
- **Category Hierarchy:** Comprehensive Bangladeshi e-commerce taxonomy (24 predefined core categories).
- **Commission Engine:** Configurable commission rates per category or per seller.
- **Content Management System (CMS):** Drag-and-drop homepage banners, promo banners, and marketing campaigns.
- **Dispute & Report Management:** Handle customer-seller disputes, return requests, and support tickets.
- **Audit Logs & Security:** Detailed platform activity logs and role-based access control (RBAC).

### 🚚 Integrations & Logistics
- **SSLCommerz Payment Gateway:** Seamless checkout via Cards, Mobile Banking (bKash, Nagad, Rocket), and Net Banking.
- **Pathao Courier Service:** Logistics API integration for automated parcel tracking and shipment dispatch.
- **Google OAuth 2.0:** One-click customer authentication with Google.
- **Automated Seeding:** Built-in `DbInitializer` that automatically creates database tables, migrates schemas, and seeds demo catalogs, sellers, and test accounts on first run.

---

## 🏗️ Architecture & Tech Stack

Sparkle follows clean, modular architectural boundaries:

```
Sparkle.sln
│
├── Sparkle.Domain           # Core business entities, interfaces, enums, & domain logic
├── Sparkle.Infrastructure   # Data persistence (EF Core DbContext), migrations, external API clients
└── Sparkle.Api              # ASP.NET Core MVC presentation layer, Razor views, controllers, & SignalR hubs
```

| Layer | Technology |
|---|---|
| **Framework** | .NET 8.0 (C# 12) |
| **Web Layer** | ASP.NET Core MVC + Web API |
| **ORM / Database** | Entity Framework Core 8.0, Microsoft SQL Server |
| **Real-time** | Microsoft SignalR (with optional Redis backplane) |
| **UI & Styling** | Bootstrap 5.3, Bootstrap Icons, Tailwind CSS v4, Vanilla JavaScript |
| **Authentication** | ASP.NET Core Identity (Cookie + JWT Bearer), Google OAuth 2.0 |
| **Reporting / PDF** | QuestPDF |
| **API Documentation**| Swashbuckle Swagger |

---

## 📁 Project Structure

```bash
Sparkle Ecommerce/
├── Sparkle.sln                      # Visual Studio Solution File
├── package.json                     # Frontend & Tailwind CSS build configuration
├── tailwind.config.js               # Tailwind configuration
├── run-project.bat                  # One-click startup script
│
├── Sparkle.Api/                     # Web Application & API
│   ├── Areas/                       # Admin & Seller MVC Areas
│   ├── Controllers/                 # Application MVC Controllers
│   ├── Data/                        # DbInitializer & Database Seeding logic
│   ├── Hubs/                        # SignalR Real-Time Hubs (Chat, Notifications)
│   ├── Middleware/                  # Custom HTTP pipeline middleware
│   ├── Services/                    # Web-tier helper & background services
│   ├── Views/                       # Razor Views (Storefront, Account, Checkout)
│   ├── wwwroot/                     # Static files (CSS, JS, images, uploads)
│   ├── Program.cs                   # Application entry point & service registrations
│   └── appsettings.json             # Core configuration & connection strings
│
├── Sparkle.Domain/                  # Domain Layer
│   ├── Catalog/                     # Product, Category, Brand entities
│   ├── Identity/                    # ApplicationUser, ApplicationRole entities
│   ├── Orders/                      # Order, Cart, Shipment, Checkout models
│   ├── Sellers/                     # Seller profile, store, performance models
│   └── Wallets/                     # Financial transactions & ledger models
│
└── Sparkle.Infrastructure/          # Infrastructure Layer
    ├── ApplicationDbContext.cs      # EF Core DbContext with model configurations
    ├── Intelligence/                # Smart search & recommendation services
    └── Services/                    # SSLCommerz, Pathao, Email & SMS implementations
```

---

## ⚙️ Getting Started

### Prerequisites

Ensure you have the following installed on your machine:
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Microsoft SQL Server](https://www.microsoft.com/sql-server/sql-server-downloads) (Express edition or LocalDB)
- [Node.js](https://nodejs.org/) (v18+ recommended, for Tailwind asset compilation)

---

### Installation & Setup

1. **Clone the Repository:**
   ```bash
   git clone https://github.com/minhajsoyan07/Sparkle-Ecommerce.git
   cd "Sparkle-Ecommerce"
   ```

2. **Restore Dependencies:**
   ```bash
   dotnet restore
   npm install
   ```

3. **Configure Database Connection:**
   Open `Sparkle.Api/appsettings.json` and adjust the connection string if needed (default points to `localhost\SQLEXPRESS`):
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Data Source=localhost\\SQLEXPRESS;Initial Catalog=SparkleEcommerce;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true;Encrypt=False"
   }
   ```

4. **Compile Tailwind CSS (Optional / If modifying styles):**
   ```bash
   npm run tw:build
   ```

---

### Running the Project

#### Option A: One-Click Launcher (Windows)
Double-click **`run-project.bat`** in the root directory.

#### Option B: Terminal
```powershell
cd Sparkle.Api
dotnet watch run
```

Open your browser and navigate to:
👉 **`http://localhost:5279`**

> 💡 **Auto-Seeding:** On the first run, `DbInitializer` will automatically detect the empty database, create the schema, and seed sample products, categories, sellers, and test accounts.

---

## 🔑 Default Login Credentials

| Role | Login URL | Email | Password |
|---|---|---|---|
| **Admin** | `/admin/login` | `admin@sparkle.local` | `Admin@123` |
| **Customer** | `/auth/login` *(Customer tab)* | `user@sparkle.local` | `User@123` |
| **Seller** | `/auth/login` *(Seller tab)* | `dailyessentials@sparkle.local` | `Vendor@123` |
| **Seller (Groceries)** | `/auth/login` *(Seller tab)* | `dailymart@sparkle.local` | `Vendor@123` |
| **Seller (Tech)** | `/auth/login` *(Seller tab)* | `computerplus@sparkle.local` | `Vendor@123` |

> *Note: All 25 seeded seller demo accounts use `Vendor@123` as their default password. Full credentials catalog is available in [AUTH_CREDENTIALS.md](AUTH_CREDENTIALS.md).*

---

## 🔒 Configuration & Security

- **Secrets Management:** Keep production API credentials (Google OAuth secrets, SSLCommerz Store IDs, SMTP passwords) in `appsettings.Development.json` or Environment Variables / Secret Manager.
- **Git Hygiene:** `appsettings.Development.json`, `appsettings.*.json`, compiled binaries (`bin/`, `obj/`), and temporary files are automatically ignored via `.gitignore`.

---

## 🤝 Contributing

Contributions, issues, and feature requests are welcome!
1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

---

## 📄 License

This project is licensed under the [ISC License](LICENSE).