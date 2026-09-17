# Sparkle E-Commerce Platform

Sparkle E-Commerce is an enterprise-grade, multi-vendor marketplace application built on the ASP.NET Core 8 framework, Entity Framework Core, and Microsoft SQL Server. The system is designed to support high-volume online retail with native multi-tenancy for administrators, independent merchant sellers, and end consumers.

---

## Table of Contents

- [Executive Overview](#executive-overview)
- [System Architecture](#system-architecture)
- [Core Functional Modules](#core-functional-modules)
  - [Customer Experience and Storefront](#customer-experience-and-storefront)
  - [Seller Management and Operations](#seller-management-and-operations)
  - [Administrative Governance and CMS](#administrative-governance-and-cms)
  - [Payment Gateways and Financial Ledger](#payment-gateways-and-financial-ledger)
  - [Logistics and Fulfillment Pipeline](#logistics-and-fulfillment-pipeline)
  - [Real-Time Support and Messaging](#real-time-support-and-messaging)
  - [Intelligence and Automated Fraud Detection](#intelligence-and-automated-fraud-detection)
- [Database Schema and Automated Seeding](#database-schema-and-automated-seeding)
- [Technology Stack](#technology-stack)
- [Project Directory Structure](#project-directory-structure)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Installation](#installation)
  - [Running the Application](#running-the-application)
- [Default System Credentials](#default-system-credentials)
- [Security and Configuration Guidelines](#security-and-configuration-guidelines)
- [License](#license)

---

## Executive Overview

Sparkle E-Commerce addresses the operational and technical requirements of modern digital commerce in regional markets. It combines consumer storefront functionality with merchant multi-tenancy, digital wallet operations, localized courier logistics, and real-time support.

The platform provides out-of-the-box infrastructure for:
- Multi-seller catalog management with isolated inventory and earnings tracking.
- Localized payment processing through SSLCommerz (Credit/Debit Cards, Mobile Financial Services including bKash, Nagad, and Rocket).
- Third-party courier integration via Pathao Logistics API for end-to-end parcel tracking.
- Localization with native bilingual support (English and Bengali).
- Automated database schema migrations and demographic data seeding on startup.

---

## System Architecture

The codebase adheres to Clean Architecture principles, enforcing strict separation of concerns across domain modeling, data persistence, and application presentation.

```
+-------------------------------------------------------------+
|                        Sparkle.Api                          |
|  ASP.NET Core 8 MVC Controllers, Razor Views, SignalR Hubs  |
+------------------------------+------------------------------+
                               |
                               v
+-------------------------------------------------------------+
|                   Sparkle.Infrastructure                    |
|   EF Core ApplicationDbContext, External Services (Payment, |
|      Couriers, Notifications, Intelligence, PDF Generation) |
+------------------------------+------------------------------+
                               |
                               v
+-------------------------------------------------------------+
|                       Sparkle.Domain                        |
|  Pure Business Entities, Enums, Value Objects, Interfaces   |
+-------------------------------------------------------------+
```

### Architectural Principles

1. **Domain Isolation (`Sparkle.Domain`):**
   Contains business entities, enums, interfaces, and core domain rules without external third-party dependencies or database-specific libraries.

2. **Infrastructure Abstraction (`Sparkle.Infrastructure`):**
   Encapsulates data access through Entity Framework Core, handles external service clients (SSLCommerz, Pathao Courier, SMTP), and implements domain service interfaces.

3. **Presentation & Application Layer (`Sparkle.Api`):**
   Implements ASP.NET Core MVC controllers, area-based segregation (`/Areas/Admin`, `/Areas/Seller`), SignalR WebSocket hubs for bi-directional messaging, and middleware pipelines.

---

## Core Functional Modules

### Customer Experience and Storefront
- **Bilingual Interface:** Dynamic locale switching between Bengali (`bn`) and English (`en`) with persistent language cookies and SolaimanLipi typographic rendering.
- **Smart Product Search:** Multi-attribute filtering across category, price brackets, brands, and seller ratings with debounced keyword querying.
- **Dynamic Merchandising:** Configurable homepage sections supporting automated criteria (New Arrivals, Discount Highlights, Trending Items) and manual curation.
- **Cart & Order Processing:** Persistent session-based cart management for guest users with seamless migration to authenticated accounts upon login.
- **Invoice Generation:** Programmatic PDF invoice creation powered by QuestPDF for download upon order confirmation.
- **Product Reviews & Feedback:** Verified purchase review validation, image attachments, and verified buyer badges.

### Seller Management and Operations
- **Merchant Onboarding:** Dedicated vendor registration, business credential verification, and automated shop profile provisioning.
- **Catalog Management:** Full product lifecycle management with multi-image upload handling, SKU management, stock alerts, and variant configuration.
- **Fulfillment Workflow:** Step-by-step order processing from receipt to courier handoff (Pending -> Processing -> Shipped -> Delivered).
- **Merchant Wallet & Ledger:** Real-time calculation of net sales, platform commission deductions, pending settlements, and disbursement requests.
- **Seller Performance Metrics:** Algorithmic performance scoring based on order fulfillment velocity, cancellation rates, and consumer satisfaction ratings.

### Administrative Governance and CMS
- **Platform Management:** Centralized administration of users, merchant stores, product catalogs, and category hierarchies.
- **Taxonomy Management:** 24 predefined regional commerce categories with customized icon mappings, slug generation, and commission overrides.
- **Financial Configuration:** Global and category-specific commission rate controls.
- **Content Management:** Banner scheduling, promotional carousel management, and alert notification broadcasts.
- **Dispute Resolution:** Ticket management for consumer-seller dispute escalation, return authorization, and order cancellations.
- **Audit Trails:** Comprehensive logging of administrative actions, user authentication attempts, and financial transactions.

### Payment Gateways and Financial Ledger
- **SSLCommerz Integration:** Sandbox and production payment processing supporting credit cards, debit cards, and Bangladeshi Mobile Financial Services (MFS).
- **Instant Payment Notification (IPN):** Webhook listener to validate transactions, handle session timeouts, and update order statuses asynchronously.
- **Digital Wallets:** Dual-ledger system maintaining isolated customer credit balances, seller receivables, and administrator platform revenues.

### Logistics and Fulfillment Pipeline
- **Pathao Courier API Integration:** Direct integration for parcel dispatch, consignment ID assignment, and real-time delivery status webhooks.
- **Regional Address Hierarchy:** Structured geographic addressing based on Bangladesh administrative divisions, districts, upazilas, and delivery hubs.
- **Automated Tracking Notifications:** Real-time customer delivery status alerts triggered by courier status transitions.

### Real-Time Support and Messaging
- **SignalR WebSockets:** Persistent bidirectional communication pipelines for live chat between customers, sellers, and platform administrators.
- **Support Ticket System:** Asynchronous customer service ticketing system for inquiries, billing questions, and warranty requests.

### Intelligence and Automated Fraud Detection
- **Smart Recommendations:** Behavior-based recommendation engine evaluating user view history and affinity categories.
- **Sentiment Analysis:** Automated customer review sentiment classification to flag negative feedback for administrative review.
- **Risk Assessment:** Heuristic fraud detection evaluating rapid repeated orders, abnormal cart sizes, and high-frequency guest checkout patterns.

---

## Database Schema and Automated Seeding

The application uses an automated initialization engine (`DbInitializer.cs`) executed during startup:

1. **Schema Validation & Repair:**
   Inspects existing database objects, ensures critical columns (such as chat deletion states and order metadata) exist, and applies non-destructive schema adjustments.

2. **Automated Seeding:**
   When an empty database is connected, the system automatically seeds:
   - **Role Hierarchy:** System roles (`Admin`, `Seller`, `User`).
   - **Administrative Accounts:** Super administrator account.
   - **Category System:** 24 localized e-commerce categories tailored for South Asian / Bangladesh markets.
   - **Demo Sellers:** 25 pre-configured seller accounts spanning multiple product sectors.
   - **Product Inventory:** Over 90 catalog items with complete pricing, descriptions, images, and inventory records.
   - **Geographic Data:** Administrative divisions and delivery hubs.

---

## Technology Stack

| Layer | Technology | Purpose |
|---|---|---|
| **Runtime** | .NET 8.0 SDK (C# 12) | Core application platform |
| **Web Framework** | ASP.NET Core 8 MVC & Web API | Presentation, routing, and controller layer |
| **ORM** | Entity Framework Core 8.0 | Data access and object-relational mapping |
| **Database** | Microsoft SQL Server / SQL Server Express | Primary relational persistence store |
| **Real-Time** | Microsoft SignalR | Real-time chat and live push notifications |
| **Cache / Distributed** | StackExchange.Redis (Optional) | SignalR backplane and distributed caching |
| **Styling & UI** | Tailwind CSS v4, Bootstrap 5.3 | Responsive user interfaces |
| **Document Generation** | QuestPDF | PDF invoice and receipt rendering |
| **API Documentation** | Swashbuckle (Swagger UI) | REST API exploration and documentation |
| **Authentication** | ASP.NET Core Identity, Google OAuth 2.0 | Authentication, authorization, and session security |

---

## Project Directory Structure

```
Sparkle Ecommerce/
├── Sparkle.sln                      # Visual Studio Solution File
├── Directory.Build.props            # Build redirect rules to prevent file locks
├── package.json                     # Node.js toolchain and Tailwind CLI definitions
├── tailwind.config.js               # Tailwind CSS compiler configuration
├── run-project.bat                  # Local application launcher
├── connect-github.bat               # Git credential configuration utility
│
├── Sparkle.Api/                     # Web Application & Presentation Layer
│   ├── Areas/
│   │   ├── Admin/                   # Administrative controllers, view models, and views
│   │   └── Seller/                  # Merchant portal controllers and views
│   ├── Controllers/                 # Storefront, cart, checkout, and auth controllers
│   ├── Data/                        # DbInitializer and schema seeding logic
│   ├── Hubs/                        # SignalR WebSocket hubs (ChatHub, NotificationHub)
│   ├── Middleware/                  # Custom request logging and session management
│   ├── Services/                    # UI-tier helper services and cache wrappers
│   ├── Views/                       # Razor Views (Storefront, Account, Checkout, Tracking)
│   ├── wwwroot/                     # Compiled assets (CSS, JS, vendor libraries, media)
│   ├── Program.cs                   # Application entry point and DI configuration
│   └── appsettings.json             # Database connections and platform configurations
│
├── Sparkle.Domain/                  # Core Business Domain Layer
│   ├── Catalog/                     # Products, categories, brands, variants
│   ├── Common/                      # Base entity contracts and audit fields
│   ├── Configuration/               # System settings and email template entities
│   ├── Identity/                    # ApplicationUser, ApplicationRole, audit logging
│   ├── Intelligence/                # Interfaces for search, recommendations, and analytics
│   ├── Logistics/                   # Delivery hubs, couriers, and tracking entities
│   ├── Marketing/                   # Coupons, promotions, discounts, and loyalty points
│   ├── Orders/                      # Shopping carts, orders, order items, shipments
│   ├── Reviews/                     # Customer ratings and feedback entities
│   ├── Sellers/                     # Seller store profiles and performance tracking
│   ├── Support/                     # Live chat messages, support tickets, disputes
│   └── Wallets/                     # Digital wallets, ledger transactions, disbursements
│
└── Sparkle.Infrastructure/          # Infrastructure & Persistence Layer
    ├── ApplicationDbContext.cs      # EF Core DbContext definition and relationship mapping
    ├── ApplicationDbContextFactory.cs # Design-time context factory for migrations
    ├── Intelligence/                # Recommendation engine and smart search implementation
    └── Services/                    # External gateway adapters (SSLCommerz, Pathao, Email)
```

---

## Getting Started

### Prerequisites

Ensure the following runtimes are installed on your development workstation:
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Microsoft SQL Server 2022 Express](https://www.microsoft.com/sql-server/sql-server-downloads) (or LocalDB)
- [Node.js (LTS v18+)](https://nodejs.org/) (for compiling Tailwind CSS assets)

---

### Installation

1. **Clone the Repository:**
   ```bash
   git clone https://github.com/minhajsoyan07/Sparkle-Ecommerce.git
   cd Sparkle-Ecommerce
   ```

2. **Restore Dependencies:**
   ```bash
   dotnet restore
   npm install
   ```

3. **Configure Database Connection:**
   Review the connection string in `Sparkle.Api/appsettings.json`. By default, it targets a local SQL Server Express instance:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Data Source=localhost\\SQLEXPRESS;Initial Catalog=SparkleEcommerce;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true;Encrypt=False"
   }
   ```

4. **Compile Frontend Assets (Optional):**
   ```bash
   npm run tw:build
   ```

---

### Running the Application

#### Method 1: Using the Launcher Script
Double-click `run-project.bat` from the root directory.

#### Method 2: Command Line
```powershell
cd Sparkle.Api
dotnet watch run
```

Navigate to:
`http://localhost:5279`

The application will automatically initialize the database schema and populate seed data during first startup.

---

## Default System Credentials

| Role | Portal URL | Username / Email | Password |
|---|---|---|---|
| **System Administrator** | `/admin/login` | `admin@sparkle.local` | `Admin@123` |
| **Customer User** | `/auth/login` (Customer Tab) | `user@sparkle.local` | `User@123` |
| **Seller (Daily Essentials)** | `/auth/login` (Seller Tab) | `dailyessentials@sparkle.local` | `Vendor@123` |
| **Seller (DailyMart BD)** | `/auth/login` (Seller Tab) | `dailymart@sparkle.local` | `Vendor@123` |
| **Seller (Computer Plus)** | `/auth/login` (Seller Tab) | `computerplus@sparkle.local` | `Vendor@123` |

All 25 pre-configured seller accounts share the default password: `Vendor@123`. A complete list of all accounts is documented in `AUTH_CREDENTIALS.md`.

---

## Security and Configuration Guidelines

1. **API Keys and Secrets:**
   Do not commit sensitive production keys (Google OAuth Client Secrets, SSLCommerz production passwords, SMTP credentials) to source control. Use `appsettings.Development.json` for local development or environment variables in staging and production environments.

2. **Git Hygiene:**
   The repository includes a `.gitignore` configured to exclude:
   - Secret overrides (`appsettings.*.json`, `secrets.json`, `.env`)
   - Build artifacts (`bin/`, `obj/`, `bin_safe/`, `obj_safe/`)
   - Dependency caches (`node_modules/`, `.vs/`, `.vscode/`)
   - Local database binaries (`*.mdf`, `*.ldf`)

3. **Connection Security:**
   The application enables cookie security (`HttpOnly`, `SameSite=Lax`, configurable `SecurePolicy`), anti-forgery token validation on forms, and parameter-safe Entity Framework Core queries.

---

## Proprietary Ownership and License

Copyright (c) 2026 Minhajul Islam (https://github.com/minhajsoyan07). All Rights Reserved.

This software, its source code, architecture, database schemas, and documentation are proprietary and confidential. No portion of this project may be copied, reproduced, redistributed, sublicensed, or claimed by any third party without explicit prior written authorization from the owner.

For detailed legal terms, consult the [LICENSE](LICENSE) document.