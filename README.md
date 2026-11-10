# tagitShop

## Run

```powershell
dotnet run --project .\tagitShop.vbproj
```

Sign in with your tagit account to connect to a live store. To use the local demo, enter **demo@tagit.local** as the email and **demo** as the password; demo data is stored under `%LocalAppData%\tagitShop\data.json`.

## Live setup

Apply [`desktop-stock-fields.sql`](../ExpressMart/supabase/schema/desktop-stock-fields.sql) to the ExpressMart Supabase database before using Live product reorder levels. The desktop client uses the public anon key and the signed-in user token; it does not contain a service-role key. Live reads and writes also require Supabase RLS policies that allow the signed-in seller to access their own seller, product, order, and approved review records. The app reports denied requests instead of bypassing those policies.

Debt records and stocktake audit history are stored locally on this computer in both modes. They are not synchronized to Supabase.
