# 2.0.0

## Breaking Changes

- Migrations `013`, `014`, `015` obligatoires avant les nouveaux écrans. Aucune colonne existante n’est supprimée, aucune séance ni aucun rang n’est réécrit.
- `013` : `SELECT` pour tout `authenticated` sur `profile_group_rank_snapshots` et `profile_muscle_rank_snapshots`. Les policies `FOR ALL` propriétaire de `011` restent. Écritures, rangs d’exercice et tentatives de quête restent privés.
- `014` / `015` : tables nouvelles (`cardio_activities`, `challenge_templates`, `weekly_challenge_assignments`, `challenge_point_awards`). Un client 2.0 sur une base arrêtée à `012` échoue sur le cardio et les défis. L’historique perso continue de lire les tables déjà là.
- Un client 1.x sur une base migrée reste compatible : les nouvelles policies sont en lecture, les nouvelles tables ne sont pas lues par l’ancien client.
- Formule de volume (`reps × kg`), règles des quêtes de rang et timer de 5 min inchangés.

## Nouvelles fonctionnalités

- Hub Statistiques : période partagée, séances, exercices et volume.
- Historique par exercice et par séance, comparaison musculaire.
- Cardio saisi à la main et dix défis de la semaine.
- 52 icônes pour les exercices qui comptent au rang. Fichiers locaux, liés par l’id d’exercice, sans colonne SQL. Bibliothèque, séance et historique. Sinon l’icône du groupe, puis l’icône générique.
- Barre : Accueil, Séances, Profil. Amis s’ouvre depuis Compte.
- Période proposée : 7 j, le mois en cours, l’année. La pastille 3 mois n’est plus affichée.
- Accueil : boutons Défis de la semaine et Quêtes de rang.

## Fix

- Un seul calcul de volume, `WorkoutVolume.Sum`, pour les stats et l’historique.
- Badge PR seulement si `pr_events.completed_workout_id` est la séance affichée. Un PR de quête sans séance n’est plus badgé.
- Le sélecteur de période est le même store pour le hub et l’historique.
- Une séance vide compte comme séance, ajoute 0 exercice et 0 volume.
