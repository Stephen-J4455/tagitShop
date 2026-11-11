# tagitShop Desktop App

tagitShop is a Windows desktop application for managing a Tagit/ExpressMart
store. It is built with Visual Basic, WPF, and .NET 10. The app provides a
focused workspace for inventory, sales, customer balances, promotions, and
storefront management.

## Core functionalities

### Store dashboard

- View an overview of store activity and key operating metrics.
- See low-stock products, recent sales, and important store information.
- Navigate between workspace, promotion, and store-management pages.
- Collapse the sidebar, search navigation pages, and switch between light and
  dark themes.

### Products and inventory

- Create, edit, view, search, and delete products.
- Manage product titles, descriptions, categories, prices, cost prices, SKUs,
  barcodes, quantities, reorder levels, sizes, colors, tags, badges, and
  specifications.
- Upload product images and videos.
- Track inventory, sold quantities, backorders, preorder status, and product
  availability.
- Delete products with related cart, wishlist, review, flash-sale, order-link,
  and reel-link cleanup where applicable.

### Sales and orders

- View sales records and payment status.
- Search orders by order number or customer.
- Filter sales by paid and unpaid status.
- View paid order counts, revenue, customer counts, and outstanding payments.
- Export sales data to Excel.
- Refresh sales data from the connected store.

### Customer debt tracking

- Record customer debt and payments.
- View outstanding balances, paid amounts, open records, and debt status.
- Mark debt records as paid.
- View payment history.
- Export debt records to Excel.
- Keep debt records and stocktake audit history locally on the computer.

### Reviews

- View approved product reviews associated with the store's products.
- Review customer ratings, titles, comments, and review dates.

### Insights

- Review paid revenue, net revenue after fees, gross profit, and average paid
  order value.
- View revenue trends and performance summaries.
- Change the reporting range between the last 7 days, last 12 months, and last
  5 years.

### Promotions

- Create and manage flash sales.
- Configure sale prices, original prices, start and end times, and quantity
  limits.
- Create coupon codes with descriptions, discount types, discount values, and
  optional expiry dates.
- See newly created coupons without restarting or manually reloading the app.
- Automatically refresh live coupons periodically.
- Delete coupons after confirmation.
- Publish and manage reels/status content, including titles, descriptions,
  prices, categories, tags, and media.

### Store management

- Update store profile and store settings.
- Edit storefront content and visibility.
- Manage storefront branding and presentation settings.

### TagAI assistant

- Explain pages and features in the desktop app.
- Answer questions about the currently selected page.
- In demo mode, browse local records for supported questions.
- In live mode, use the authenticated `ai-assistant` Supabase Edge Function.
- Send only the selected record section, limited to matching rows, when a live
  store question requires store data.

## Operating modes

### Live store mode

Sign in with a Tagit store account to connect to the live store. Live reads and
writes use Supabase REST APIs with the signed-in user's access token. The app
uses seller-scoped queries so store management actions apply to the connected
seller.

### Demo mode

Use the built-in demo credentials:

```text
Email:    demo@tagit.local
Password: demo
```

Demo data is stored locally at:

```text
%LocalAppData%\tagitShop\data.json
```

Demo mode allows the main store-management workflows to be explored without a
live account or remote database.

## Data and security

- The desktop client uses the public Supabase anon key and the signed-in user
  token; it does not contain a service-role key.
- Live access depends on Supabase Row Level Security policies for the signed-in
  seller.
- The app reports denied or failed requests instead of bypassing database
  policies.
- Debt records and stocktake audit history are local-only in both live and demo
  modes.
- The TagAI feature should use a deployed desktop-assistant mode in the
  `ai-assistant` Supabase Edge Function before live questions are enabled.

## Requirements

- Windows
- .NET SDK 10.0 or later
- Access to the Tagit/ExpressMart Supabase project for live mode
- The `desktop-stock-fields.sql` schema update applied for live product reorder
  levels

## Running the app

From the `tagitdesktop` directory:

```powershell
dotnet run --project .\tagitShop.vbproj
```

The application starts at the sign-in screen. Choose live sign-in or use the
demo credentials above to open the store workspace.

## Technology

- Visual Basic
- WPF
- .NET 10 for Windows
- Supabase REST API and authentication
- Supabase Edge Function integration for live TagAI questions
- ClosedXML for Excel exports
- Local JSON storage for demo and local-only records
