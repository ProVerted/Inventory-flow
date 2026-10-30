# ⚡ InventoryFlow

> **Smart Inventory, Warehouse, and Point of Sale (POS) Management System** built with **.NET 10**, **Entity Framework Core**, and an interactive, modern web dashboard.

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512bd4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![EF Core](https://img.shields.io/badge/Entity%20Framework%20Core-10.0.3-68217a?style=flat-square)](https://learn.microsoft.com/ef/core/)
[![Tailwind CSS](https://img.shields.io/badge/Tailwind_CSS-3.x-38bdf8?style=flat-square&logo=tailwind-css)](https://tailwindcss.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg?style=flat-square)](LICENSE)

---

## 📖 Overview

**InventoryFlow** is a full-featured inventory and sales management solution designed for retail stores, warehouses, and small businesses. It combines a robust ASP.NET Core Web API backend with a responsive, zero-build client dashboard served directly from `wwwroot`.

From real-time stock alerts and delta restocking to an atomic Point of Sale (POS) cashier checkout with printable receipt generation, InventoryFlow delivers an end-to-end operational platform.

---

## ✨ Features

### 📊 1. Executive Dashboard
* **Real-time KPI Cards**: Instant calculation of Total Inventory Valuation ($\sum Price \times Stock$), Total Items in Warehouse, and Low-Stock Warning counters.
* **Financial Turnover**: Live revenue counter and sales count tracking.
* **Quick Navigation**: Fast shortcuts to POS Cashier, Product Catalog, and System Health.

### 📦 2. Product Catalog Management
* **Catalog CRUD**: Add, edit, and delete products with strict SKU uniqueness validation.
* **Instant Debounced Search**: Search across product names and SKUs without page reloads.
* **Stock Health Badges**:
  * 🟢 **In Stock** (Healthy inventory)
  * 🟡 **Low Stock** (At or below alert threshold)
  * 🔴 **Out of Stock** ($0$ items remaining)
* **Quick Restock Modal**: Adjust warehouse stock levels via simple $+ / -$ delta values (e.g. `+50` for incoming shipments or `-2` for damaged write-offs) with optional audit reasons.

### 🛒 3. Point of Sale (POS) & Atomic Checkout
* **Split-Screen Cashier**: Interactive product catalog grid on the left; real-time shopping cart on the right.
* **Stock Limit Protection**: Prevents adding more units to the cart than currently available in warehouse stock.
* **Transactional Checkout**: Uses EF Core database transactions (`BeginTransactionAsync`) to atomically:
  1. Validate stock availability for all line items.
  2. Deduct inventory quantities.
  3. Freeze the product's unit price at point-of-sale for historical integrity.
  4. Generate a unique human-readable invoice reference (e.g., `INV-20261021-482`).
  5. Roll back automatically on any concurrency conflict or insufficient stock.

### 🧾 4. Invoicing & Printable Receipts
* **Complete Sales Archive**: Searchable log of past transactions with date filtering.
* **Print-Ready Receipts**: Modal receipt view formatted with company header, itemized quantities, unit prices, subtotal, and tax lines ready for desktop or thermal receipt printers (`window.print()`).

### 🔐 5. Security & Role-Based Access Control (RBAC)
* **Cookie-Based Authentication**: Secure, HttpOnly cookie sessions.
* **PBKDF2 Password Hashing**: Cryptographic password hashing powered by ASP.NET Core `IPasswordHasher<User>`.
* **Roles**:
  * **Admin**: Full access (product creation, editing, deletion, stock adjustments, and sales reports).
  * **Staff**: View-only access to catalog, POS cashier selling, and invoice generation (administrative buttons are hidden).

---

## 🛠️ Technology Stack

| Layer | Technologies |
| :--- | :--- |
| **Framework** | .NET 10.0 (ASP.NET Core Web API) |
| **Data Access / ORM** | Entity Framework Core 10.0.3 (SQL Server Provider, Migrations, Tools) |
| **Database** | Microsoft SQL Server (`InventoryDB`) |
| **Authentication** | Cookie Authentication (`CookieAuthenticationDefaults`) + PBKDF2 Hashing |
| **API Documentation** | OpenAPI 3.1 & Scalar Interactive API Portal (`Scalar.AspNetCore`) |
| **Frontend UI** | Vanilla JavaScript (ES6+), HTML5, Tailwind CSS (via CDN), FontAwesome 6 |

---

## 🚀 Getting Started

### Prerequisites
* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or higher.
* Microsoft SQL Server (LocalDB, SQL Express, or Docker container).

---

### Installation & Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/ProVerted/Inventory-flow.git
   cd Inventory-flow
   ```

2. **Configure Database Connection:**
   Verify or edit the connection string in [`appsettings.json`](file:///c:/Users/JANARTHTHAN/Desktop/InventorySystem/appsettings.json):
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=.\\SQLEXPRESS;Database=InventoryDB;Trusted_Connection=true;TrustServerCertificate=true;"
   }
   ```

3. **Apply Database Migrations:**
   Run the Entity Framework migration command to create tables, indexes, and foreign keys:
   ```bash
   dotnet ef database update
   ```

4. **Run the Application:**
   ```bash
   dotnet run
   ```

5. **Open in Browser:**
   * 🖥️ **Web Dashboard**: [http://localhost:5111](http://localhost:5111)
   * 📑 **Interactive Scalar API Docs**: [http://localhost:5111/scalar/v1](http://localhost:5111/scalar/v1)

---

## 🔑 Default Seeded Credentials

When launched on an empty database, [`DbInitializer`](file:///c:/Users/JANARTHTHAN/Desktop/InventorySystem/Data/DbInitializer.cs) automatically seeds initial user accounts and a sample product catalog:

| Role | Username | Password | Permissions |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin` | `Admin123!` | Full administrative, restock, and catalog management rights |
| **Staff** | `staff` | `Staff123!` | Catalog lookup, POS checkout, and invoice generation |

*(Quick-login buttons for both accounts are available directly on the login overlay).*

---

## 📡 REST API Reference

All protected endpoints require an authenticated session cookie obtained via `/auth/login`.

### Authentication (`/auth`)
| Method | Endpoint | Access | Description |
| :---: | :--- | :---: | :--- |
| `POST` | `/auth/register` | Public | Register a new user account (first user defaults to Admin) |
| `POST` | `/auth/login` | Public | Authenticate credentials and establish session cookie |
| `POST` | `/auth/logout` | Public | Invalidate current session cookie |
| `GET` | `/auth/me` | Authenticated | Retrieve profile and role of current logged-in user |

### Products & Inventory (`/products`)
| Method | Endpoint | Access | Description |
| :---: | :--- | :---: | :--- |
| `GET` | `/products` | Admin, Staff | List products with optional `?search=` and `?lowStockOnly=true` |
| `GET` | `/products/low-stock` | Admin, Staff | Retrieve all items currently at or below their alert threshold |
| `GET` | `/products/{id}` | Admin, Staff | Retrieve single product details by ID |
| `POST` | `/products` | Admin | Create a new product (validates SKU uniqueness) |
| `PUT` | `/products/{id}` | Admin | Update existing product catalog details |
| `PATCH` | `/products/{id}/stock` | Admin | Adjust inventory stock via positive/negative delta |
| `DELETE` | `/products/{id}` | Admin | Delete product (blocked if referenced in existing invoices) |

### Invoices & Sales (`/invoices`)
| Method | Endpoint | Access | Description |
| :---: | :--- | :---: | :--- |
| `POST` | `/invoices` | Admin, Staff | Atomic checkout: verifies stock, deducts inventory, locks prices |
| `GET` | `/invoices` | Admin, Staff | List sales invoices with optional date range filters |
| `GET` | `/invoices/{id}` | Admin, Staff | Retrieve full invoice details including itemized lines |

### Reports & Analytics (`/reports`)
| Method | Endpoint | Access | Description |
| :---: | :--- | :---: | :--- |
| `GET` | `/reports/dashboard` | Admin, Staff | Overview metrics (valuation, revenue, stock counts, alerts) |
| `GET` | `/reports/sales` | Admin | Filterable sales report with daily revenue and top-selling items |

---

## 📂 Project Structure

```text
InventorySystem/
├── Controllers/
│   ├── AuthController.cs          # Login, Register, Logout, Me endpoints
│   ├── ProductsController.cs      # Catalog CRUD, search, and delta stock adjustment
│   ├── InvoicesController.cs      # Atomic POS checkout and invoice querying
│   └── ReportsController.cs       # Executive dashboard and sales analytics
├── Data/
│   ├── AppDbContext.cs            # EF Core DbContext with precision configurations
│   └── DbInitializer.cs           # Automatic database connection verification & seeding
├── DTOs/
│   ├── AuthDtos.cs                # LoginRequest, RegisterRequest, UserResponse
│   ├── ProductDtos.cs             # CreateProductRequest, AdjustStockRequest, ProductResponse
│   ├── InvoiceDtos.cs             # CreateInvoiceRequest, InvoiceItemResponse, InvoiceResponse
│   └── ReportDtos.cs              # DashboardReport, SalesReportResponse
├── Middleware/
│   └── ExceptionMiddleware.cs     # Centralized RFC 7807 error handling middleware
├── Migrations/                    # EF Core schema snapshots and migration files
├── Models/
│   ├── User.cs                    # User entity with roles
│   ├── Product.cs                 # Catalog item with pricing and stock levels
│   ├── Invoice.cs                 # Order/invoice header
│   └── InvoiceItem.cs             # Line items with locked-in unit prices
├── Properties/
│   └── launchSettings.json        # Local port and profile configurations (Port 5111)
├── wwwroot/                       # Static Frontend Dashboard
│   ├── index.html                 # Single-page dashboard application shell
│   └── js/
│       └── app.js                 # UI interactions, POS cart logic, API integrations
├── InventorySystem.csproj         # Project targets and NuGet dependencies
├── InventorySystem.http           # Pre-configured REST client test requests
└── Program.cs                     # Startup configuration, DI container, and middleware pipeline
```

---

## 🧪 Testing with HTTP File

You can execute end-to-end API tests directly inside VS Code or Visual Studio using [`InventorySystem.http`](file:///c:/Users/JANARTHTHAN/Desktop/InventorySystem/InventorySystem.http). It includes pre-written request blocks for user registration, admin login, product additions, stock checks, and checkout operations.

---

## 📄 License

This project is open-source and licensed under the [MIT License](LICENSE).
