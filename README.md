# Cariñitos

A Windows desktop app to run a small baby-products store ("pañalera"): products and stock,
sales, expenses, customers, to-dos, reports, physical stock counts and the supplier's catalog.
It is used every day by a real shop, so the user interface is in **Spanish (Argentina)**; the
code, comments and database schema are in English.

## What it does

| Area | What it covers |
|---|---|
| Products | The shop's catalog: prices, stock, minimum stock, code, category and photo, with price history and a per-product change log. |
| Stock count | Physical count of every product: printable PDF checklist, on-screen verification, missing/extra units and notes. Finishing it adjusts the stock. |
| Sales | Multi-item sales with an optional customer, discount (amount or %) and payment method (cash or Mercado Pago). Sales are voided, never deleted, and voiding puts the stock back. |
| Expenses | Money going out, by date and payment method. |
| Customers | Contact book with each customer's purchase history. |
| To-dos | Pending tasks with priority and an optional customer. |
| Reports | Profit per product, discounts, expenses, net result and inventory value, filterable by date, product, seller and payment method, exportable to CSV. |
| Distributor | The supplier's catalog, downloaded by a companion scraper, to look up prices and photos and add products from it. |
| Audit | Log of every relevant operation, with before/after values (admin only). |
| Users | Users with three roles: Admin, Employee and Manager. |

## Tech stack

- **.NET 9**, **Blazor Hybrid** hosted in **WinForms** (`BlazorWebView`, needs the WebView2 runtime).
- **EF Core 9 + SQLite**: a single local database file; migrations run automatically at startup.
- **QuestPDF** for the stock count PDF.
- **xUnit + bUnit** tests against a real SQLite database (no mocks for the data layer).
- **Inno Setup** installer (`Carinitos.iss`), self-contained `win-x64` publish.

## Project layout

```
Components/      Blazor UI: Layout, Pages (one per screen) and Shared components
Services/        Business logic (one service per area) and the audit log
Models/          EF Core entities and report rows
Data/            DbContext and design-time factory
Migrations/      EF Core migrations (older ones keep the original Spanish schema; see RenameSchemaToEnglish)
wwwroot/         Host page, global CSS and Bootstrap
tests/           panialera.Tests (services, screens, infrastructure and project rules)
*.cs (root)      App startup, settings, backups, logging, error hub, CSV/PDF export
```

## Design notes

- **Sales are snapshots.** Each sold item stores the product name and prices at that moment,
  so editing or deleting a product never changes past sales or reports.
- **One writer.** Only this app writes the database. The companion phone app records sales as
  events in an external log, and `EventImportService` applies each one in a single transaction,
  storing its id so it can never be applied twice. Off until `EventLogUrl` and `EventLogKey` are set.
- **Everything goes through the audit log.** Services record what changed (field by field) and
  who did it; every exception goes through a single `ErrorHub` (file log + audit log + toast).
- **Unsaved changes are guarded.** Forms register with `UnsavedGuard`; leaving a dirty form asks
  whether to save or discard.
- **One settings file.** Everything adjustable lives in `appsettings.json` next to the `.exe`
  (database path, backup folder and schedule, audit retention, scraper path). The app creates it
  on first run and the installer never overwrites it.
- **Daily backups** use SQLite's online backup API and keep the last N copies.

## Building and running

Requirements: Windows, .NET 9 SDK, WebView2 runtime.

```bash
dotnet build panialera.sln
dotnet test tests/panialera.Tests
dotnet run --project panialera.csproj
```

On first run the app creates `appsettings.json` next to the executable and a database under
`%APPDATA%\panialera`. If there is no administrator it creates **`admin` / `123`**: change that
password right after the first login.

The distributor sync uses the companion **Scrapper** project, expected in a sibling folder
(`../Scrapper`). Publish it first so the app build can bundle it:

```bash
dotnet publish ../Scrapper -c Release -o ../Scrapper/publish
dotnet publish panialera.csproj -c Release -r win-x64 --self-contained true
```

Then compile `Carinitos.iss` with Inno Setup to get the installer. The test project also links a
few pure classes from `../Scrapper` to check the catalog file contract between both programs.

## License

All rights reserved. The source is published for reference only; no license is granted to use,
copy or distribute it.
