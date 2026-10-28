-- Reset progression de TOUS les utilisateurs.
-- Comptes, séances types, amis et catalogue d’exercices sont conservés.
--
-- À lancer dans Supabase → SQL Editor, connecté en rôle postgres (pas l’anon key).
-- Vérifie d’abord les compteurs, puis décommente le bloc DELETE.

-- 1) Aperçu
select 'rank_quest_attempts' as table, count(*) from public.rank_quest_attempts
union all select 'profile_exercise_ranks', count(*) from public.profile_exercise_ranks
union all select 'profile_muscle_rank_snapshots', count(*) from public.profile_muscle_rank_snapshots
union all select 'profile_group_rank_snapshots', count(*) from public.profile_group_rank_snapshots
union all select 'pr_events', count(*) from public.pr_events
union all select 'exercise_personal_bests', count(*) from public.exercise_personal_bests
union all select 'exercise_last_weights', count(*) from public.exercise_last_weights
union all select 'exercise_performance_daily', count(*) from public.exercise_performance_daily
union all select 'completed_workouts', count(*) from public.completed_workouts
union all select 'session_last_snapshots', count(*) from public.session_last_snapshots
order by 1;

-- 2) Wipe (décommente pour exécuter)
/*
begin;

delete from public.rank_quest_attempts;
delete from public.profile_exercise_ranks;
delete from public.profile_muscle_rank_snapshots;
delete from public.profile_group_rank_snapshots;
delete from public.pr_events;
delete from public.exercise_personal_bests;
delete from public.exercise_last_weights;
delete from public.exercise_performance_daily;
delete from public.completed_workouts;
delete from public.session_last_snapshots;

commit;
*/
