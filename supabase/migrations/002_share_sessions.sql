-- Partage des séances entre amis (lecture + import)

create policy session_templates_friend_read on public.session_templates
  for select using (
    owner_id = auth.uid()
    or exists (
      select 1 from public.friendships f
      where f.status = 'accepted'
        and (
          (f.requester_id = auth.uid() and f.addressee_id = session_templates.owner_id)
          or (f.addressee_id = auth.uid() and f.requester_id = session_templates.owner_id)
        )
    )
  );

create policy session_exercises_friend_read on public.session_exercises
  for select using (
    exists (
      select 1 from public.session_templates t
      where t.id = session_exercises.session_id
        and (
          t.owner_id = auth.uid()
          or exists (
            select 1 from public.friendships f
            where f.status = 'accepted'
              and (
                (f.requester_id = auth.uid() and f.addressee_id = t.owner_id)
                or (f.addressee_id = auth.uid() and f.requester_id = t.owner_id)
              )
          )
        )
    )
  );
