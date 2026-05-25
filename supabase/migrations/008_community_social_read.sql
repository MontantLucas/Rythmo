-- Tous les utilisateurs authentifiés voient les stats sociales de la communauté (pas de filtre amitié).

create policy profiles_select_community on public.profiles
  for select to authenticated
  using (auth.uid() is not null);

create policy completed_workouts_community_read on public.completed_workouts
  for select to authenticated
  using (auth.uid() is not null);

create policy session_templates_community_read on public.session_templates
  for select to authenticated
  using (auth.uid() is not null);

create policy session_exercises_community_read on public.session_exercises
  for select to authenticated
  using (auth.uid() is not null);

create policy session_last_snapshots_community_read on public.session_last_snapshots
  for select to authenticated
  using (auth.uid() is not null);

create policy exercise_performance_daily_community_read on public.exercise_performance_daily
  for select to authenticated
  using (auth.uid() is not null);

create policy exercise_personal_bests_community_read on public.exercise_personal_bests
  for select to authenticated
  using (auth.uid() is not null);

create policy pr_events_community_read on public.pr_events
  for select to authenticated
  using (auth.uid() is not null);
