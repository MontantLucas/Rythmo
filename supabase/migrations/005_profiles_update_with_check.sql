-- UPDATE profil : WITH CHECK explicite (évite les PATCH silencieux sous RLS)
drop policy if exists profiles_update_own on public.profiles;
create policy profiles_update_own on public.profiles
  for update to authenticated
  using (auth.uid() = id)
  with check (auth.uid() = id);
