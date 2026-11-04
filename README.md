# tagitShop

## Run

```powershell
dotnet run --project .\tagitShop.vbproj
```

Choose **Open demo store** to use seeded data stored under `%LocalAppData%\tagitShop\data.json`. Choose **Sign in to a live store** to authenticate with the tagit Supabase account linked to a seller profile.

## Live setup

Apply [`desktop-stock-fields.sql`](../ExpressMart/supabase/schema/desktop-stock-fields.sql) to the ExpressMart Supabase database before using Live product reorder levels. The desktop client uses the public anon key and the signed-in user token; it does not contain a service-role key. Live reads and writes also require Supabase RLS policies that allow the signed-in seller to access their own seller, product, order, and approved review records. The app reports denied requests instead of bypassing those policies.

Debt records and stocktake audit history are stored locally on this computer in both modes. They are not synchronized to Supabase.
