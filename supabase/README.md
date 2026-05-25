# Supabase — Rhythmo V2

## 1. Migrations (automatique en local)

Remplis `Rhythmo/.env` (voir `.env.example`), puis :

```powershell
cd Rhythmo
.\scripts\dev.ps1
```

Le script applique les fichiers `supabase/migrations/*.sql` via Postgres (outil `tools/Rhythmo.DbMigrate`), puis lance l’app.

Alternative manuelle : SQL Editor → `migrations/001_initial.sql`.

## 2. Auth Magic Link

1. **Authentication** → **Providers** → **Email** : activer, Magic Link activé.
2. **Authentication** → **URL Configuration** :
   - **Site URL** : `https://lvmqvylradoimaeckwpj.supabase.co` (URL **https**, pas `rhythmo://`)
   - **Redirect URLs** : `rhythmo://login-callback/` (schéma app — liste séparée)
3. **Sign ups** : activés si tu crées un compte via le lien (première connexion).

> Si **Site URL** = `rhythmo://…`, les mails peuvent afficher un lien cassé ou ouvrir `about:blank` : le navigateur ne comprend pas ce schéma dans le corps du mail. Le schéma `rhythmo://` va **uniquement** dans **Redirect URLs** ; l’app envoie déjà `EmailRedirectTo = rhythmo://login-callback/`.

### Créer ton compte

**Ne pas** insérer seulement une ligne dans `public.profiles` : l’app a besoin d’une session **Auth** (`auth.users` + JWT). Le trigger SQL crée `profiles` quand un user Auth est créé.

| Plateforme | Méthode |
|------------|---------|
| **Mobile** | Lien magique → ouvrir le mail **sur le téléphone** |
| **PC (test)** | **Authentication** → **Users** → **Add user** (e-mail + mot de passe) → connexion « mot de passe » dans l’app |
| **PC (lien mail)** | Clic sur le lien → copier l’URL du navigateur → « Valider l’URL collée » dans l’app |

Sur Windows, le navigateur ne renvoie pas toujours vers `rhythmo://` : le mot de passe ou le collage d’URL est le flux prévu pour le dev PC.

## 3. Clés API

**Project Settings** → **API** :

- **Project URL** : `https://lvmqvylradoimaeckwpj.supabase.co`
- **anon public** : à copier dans `SUPABASE_ANON_KEY` (voir `.env.example`)

Ne jamais embarquer la `service_role` ni le mot de passe Postgres dans l’app mobile.

## 4. Lancer l’app

```powershell
$env:SUPABASE_ANON_KEY = "eyJ..."
dotnet run --project Rhythmo.Mobile\Rhythmo.Mobile.csproj -f net10.0-windows10.0.19041.0
```
