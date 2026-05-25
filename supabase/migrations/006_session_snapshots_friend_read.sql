-- Lecture des snapshots de séance pour les amis (présence « en séance »)
drop policy if exists session_last_snapshots_friend_read on public.session_last_snapshots;
create policy session_last_snapshots_friend_read on public.session_last_snapshots
  for select to authenticated
  using (
    exists (
      select 1
      from public.session_templates t
      join public.friendships f on f.status = 'accepted'
      where t.id = session_last_snapshots.session_id
        and t.owner_id <> auth.uid()
        and (
          (f.requester_id = auth.uid() and f.addressee_id = t.owner_id)
          or (f.addressee_id = auth.uid() and f.requester_id = t.owner_id)
        )
    )
  );

-- Performance quotidienne visible par les amis (courbe / PR côté social)
drop policy if exists exercise_performance_daily_friend_read on public.exercise_performance_daily;
create policy exercise_performance_daily_friend_read on public.exercise_performance_daily
  for select to authenticated
  using (
    exists (
      select 1 from public.friendships f
      where f.status = 'accepted'
        and profile_id <> auth.uid()
        and (
          (f.requester_id = auth.uid() and f.addressee_id = profile_id)
          or (f.addressee_id = auth.uid() and f.requester_id = profile_id)
        )
    )
  );
