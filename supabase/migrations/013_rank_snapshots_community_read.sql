-- Lecture des rangs de groupe et de sous-muscle par tout compte connecté.
-- Les policies d'écriture propriétaire de 011 restent en place.

drop policy if exists profile_muscle_rank_snapshots_read on public.profile_muscle_rank_snapshots;
create policy profile_muscle_rank_snapshots_read
  on public.profile_muscle_rank_snapshots
  for select to authenticated
  using (auth.uid() is not null);

drop policy if exists profile_group_rank_snapshots_read on public.profile_group_rank_snapshots;
create policy profile_group_rank_snapshots_read
  on public.profile_group_rank_snapshots
  for select to authenticated
  using (auth.uid() is not null);
