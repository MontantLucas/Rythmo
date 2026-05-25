# Rhythmo

Application **.NET MAUI** (Android + Windows) avec backend **Supabase** (PostgreSQL + Auth magic link). Historique cloud et hub social communautaire.

## Prérequis

- [.NET SDK 10](https://dotnet.microsoft.com/download) avec charges de travail MAUI Android + Windows.
- Projet [Supabase](https://supabase.com) configuré (voir `supabase/README.md`).

## Configuration Supabase (obligatoire)

1. Copie `.env.example` → `.env` et remplis au minimum **`SUPABASE_ANON_KEY`** (dashboard → *Project Settings* → *API* → clé publishable `sb_publishable_…` ou legacy anon).
   Le mot de passe Postgres (`SUPABASE_DB_PASSWORD`) sert **uniquement** aux migrations locales, pas à l’app.
2. Configure le magic link : *Authentication* → *URL Configuration* → `rhythmo://login-callback/`

## Lancer l’app (migrations + MAUI)

```powershell
cd Rhythmo
.\scripts\dev.ps1
```

`dev.ps1` charge `.env`, applique `supabase/migrations/*.sql` automatiquement, puis `dotnet run` Windows.

Sans le script :

```powershell
cd Rhythmo
dotnet run --project tools\Rhythmo.DbMigrate\Rhythmo.DbMigrate.csproj
dotnet run --project Rhythmo.Mobile\Rhythmo.Mobile.csproj -f net10.0-windows10.0.19041.0
```

**Ne commite jamais** `.env` (déjà dans `.gitignore`).

Connexion par **lien magique** e-mail → ouverture de l’app via le schéma `rhythmo://`.

## Structure

- `Rhythmo.Mobile` — MAUI, client Supabase, onglets + hub social.
- `Rhythmo.Shared` — DTOs et logique métier partagée.
- `supabase/` — migrations SQL et RLS.
- `tools/Rhythmo.DbMigrate` — application des migrations en local.

## APK Android (Release)

```powershell
dotnet publish Rhythmo.Mobile\Rhythmo.Mobile.csproj -f net10.0-android -c Release -p:AndroidPackageFormat=apk
```

Le fichier `Rhythmo/.env` doit exister **avant** le build pour embarquer la clé Supabase dans l’APK.
