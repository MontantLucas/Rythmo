# Rhythmo

Application de musculation **.NET MAUI** (Android + Windows) avec backend **Supabase** (PostgreSQL, Auth).  
Séances, historique cloud, rangs musculaires, défis chrono, et hub social.

## Fonctionnalités

### Compte
- Connexion par **lien magique** e-mail (`rhythmo://login-callback/`) ou mot de passe en local.
- Un profil par utilisateur : nom, biométrie (âge, poids, taille) pour l’estimation kcal.
- Déconnexion depuis Profil.

### Accueil
- Carte anatomique interactive (vue avant / arrière).
- Rang validé par **groupe musculaire** (R1–R10) d’après les exercices classifiants.
- Accès aux détails groupe → muscle → exercice.
- Badge et liste des **défis** débloqués.

### Rangs et défis
- e1RM interne (Epley / 30) pour classer un exercice par rapport au standard R10.
- Rang théorique vs rang **validé** (pas de saut de rang).
- Quête = **+1 rang**, timer **strictement 5 minutes**.
- Objectif concret affiché avant et pendant le défi (`kg × reps`, `kg / haltère` si haltères).
- **R1–R4** : validation souple via e1RM, avec alternatives 1RM / 5RM / 10RM.
- **R5+** : contrainte exacte `poids ≥ cible` **et** `reps ≥ cible` (l’e1RM ne contourne plus les reps).
- Une tentative par exercice et par jour.
- Fermeture en cours : popup **Reprendre** ou **Refuser** (échec).

### Séances
- Bibliothèque de séances, recherche, duplication, suppression.
- Éditeur de séance, démarrage, séance à la volée, démarrage rapide.
- Import de séances depuis un autre utilisateur.
- Reprise d’une séance interrompue (brouillon local).

### Exercices
- Catalogue filtré (recherche, catégorie, muscle).
- Muscles **Primary** / **Secondary**, MET, rang utilisateur.
- Lancement d’une séance à la volée depuis une fiche.

### Amis
- Activité du jour, séances en live, streaks, badges.
- Fil des records personnels.
- Classement (semaine / mois / all-time) : nombre de séances et volume.

### Stats
- Aperçu : volume cumulé, séances terminées, énergie estimée.
- Historique des séances terminées (détail, nettoyage).
- Courbe de progression du poids max par exercice.

## Navigation

Barre principale : **Accueil** · **Séances** · **Amis** · **⋯**  
**⋯** → Profil et Stats. Les défis ne sont pas dans la barre, uniquement depuis l’accueil.

## Prérequis

- [.NET SDK 10](https://dotnet.microsoft.com/download) avec charges de travail MAUI Android + Windows.
- Projet [Supabase](https://supabase.com) configuré (voir `supabase/README.md`).

## Configuration Supabase

1. Copie `.env.example` → `.env` et remplis au minimum **`SUPABASE_ANON_KEY`**.
   Le mot de passe Postgres (`SUPABASE_DB_PASSWORD`) sert **uniquement** aux migrations locales, pas à l’app.
2. Magic link : *Authentication* → *URL Configuration* → `rhythmo://login-callback/`

**Ne commite jamais** `.env` (déjà dans `.gitignore`).

## Lancer l’app (local)

```powershell
cd Rhythmo
.\scripts\dev.ps1
```

Charge `.env`, applique `supabase/migrations/*.sql`, puis lance MAUI **Windows**.

Sans le script :

```powershell
cd Rhythmo
dotnet run --project tools\Rhythmo.DbMigrate\Rhythmo.DbMigrate.csproj
dotnet run --project Rhythmo.Mobile\Rhythmo.Mobile.csproj -f net10.0-windows10.0.19041.0
```

## Tests

```powershell
dotnet test Rhythmo.Shared.Tests\Rhythmo.Shared.Tests.csproj
```

## CI GitHub

À chaque **push**, GitHub Actions :

1. lance les tests ;
2. publie un **APK Android** (pas l’app Windows).

Artifact : **Actions** → le run → `rhythmo-android`.

Secret recommandé : `SUPABASE_ANON_KEY` (sinon l’APK compile mais ne se connecte pas au cloud).  
Optionnel : `SUPABASE_URL`.

## APK Android (local)

```powershell
dotnet publish Rhythmo.Mobile\Rhythmo.Mobile.csproj -f net10.0-android -c Release -p:AndroidPackageFormat=apk
```

Le fichier `.env` doit exister **avant** le build pour embarquer la clé client dans l’APK.

## Structure

- `Rhythmo.Mobile` — MAUI, client Supabase, UI.
- `Rhythmo.Shared` — DTOs, e1RM, rangs, objectifs de quête.
- `Rhythmo.Shared.Tests` — tests unitaires.
- `supabase/` — migrations SQL et RLS.
- `tools/Rhythmo.DbMigrate` — application des migrations en local.
- `.github/workflows/ci.yml` — tests + APK.
