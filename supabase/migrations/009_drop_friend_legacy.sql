-- Retire le modèle « amis » remplacé par la lecture communautaire (008).

drop view if exists public.friend_weekly_leaderboard;
drop function if exists public.find_profile_id_by_email(text);

drop policy if exists profiles_select_friends on public.profiles;
drop policy if exists completed_workouts_friend_read on public.completed_workouts;
drop policy if exists session_templates_friend_read on public.session_templates;
drop policy if exists session_exercises_friend_read on public.session_exercises;
drop policy if exists session_last_snapshots_friend_read on public.session_last_snapshots;
drop policy if exists exercise_performance_daily_friend_read on public.exercise_performance_daily;
drop policy if exists exercise_personal_bests_friend_read on public.exercise_personal_bests;
drop policy if exists pr_events_friend_read on public.pr_events;
drop policy if exists friendships_select on public.friendships;
drop policy if exists friendships_insert on public.friendships;
drop policy if exists friendships_update_addressee on public.friendships;

drop table if exists public.friendships;
drop type if exists public.friendship_status;
