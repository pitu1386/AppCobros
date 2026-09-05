# Libreta de Cobros — versión web (PWA)

App web instalable (Blazor WebAssembly) con la base publicada en Supabase.
Misma idea que AppPoblenou. La app MAUI del repo raíz sigue funcionando aparte.

- **Frontend:** `LibretaCobros/` — Blazor WASM (.NET 10), Tailwind CSS
- **Backend:** Supabase (Postgres + Auth + Realtime), `dsnsqrqoxddtvqxaevtx.supabase.co`
- **Hosting:** GitHub Pages, rama `gh-pages` → `https://pitu1386.github.io/AppCobros/`

## Puesta en marcha (una sola vez)

1. **Base:** Supabase → SQL Editor → pegar `supabase_schema.sql` → Run.
2. **Usuario:** Supabase → Authentication → Users → Add user (email + contraseña, ✔ Auto Confirm).
   Y en Authentication → Providers → Email: desactivar "Confirm email".
3. **Datos actuales:** exportar el JSON de la app MAUI a `web/export-actual.json`, y:
   ```
   cd web
   node migrar.mjs export-actual.json > migracion-datos.sql
   ```
   Pegar `migracion-datos.sql` en el SQL Editor → Run.

## Desarrollo local

```
cd web/LibretaCobros
npm install            # solo la primera vez (Tailwind)
npm run watch:css      # en otra terminal mientras editás .razor
dotnet run --urls http://localhost:5177
```

## Publicar

```
cd web
powershell -File .\deploy.ps1
```
La primera vez: en GitHub → Settings → Pages → Source = rama `gh-pages`.
