-- Sorties cardio saisies à la main. L'allure n'est pas stockée.

create table if not exists public.cardio_activities (
  id uuid primary key,
  profile_id uuid not null references public.profiles (id) on delete cascade,
  performed_utc timestamptz not null default now(),
  distance_m double precision not null check (distance_m > 0),
  duration_s integer not null check (duration_s > 0)
);

create index if not exists cardio_activities_profile_performed_idx
  on public.cardio_activities (profile_id, performed_utc desc);

alter table public.cardio_activities enable row level security;

drop policy if exists cardio_activities_own on public.cardio_activities;
create policy cardio_activities_own on public.cardio_activities
  for select to authenticated
  using (profile_id = auth.uid());

drop policy if exists cardio_activities_insert on public.cardio_activities;
create policy cardio_activities_insert on public.cardio_activities
  for insert to authenticated
  with check (profile_id = auth.uid());

drop policy if exists cardio_activities_delete on public.cardio_activities;
create policy cardio_activities_delete on public.cardio_activities
  for delete to authenticated
  using (profile_id = auth.uid());

grant select, insert, delete on public.cardio_activities to authenticated;
