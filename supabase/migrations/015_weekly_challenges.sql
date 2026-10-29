-- Dix défis hebdomadaires. Le score est la somme des attributions, pas un compteur.

create table if not exists public.challenge_templates (
  id text primary key,
  family text not null,
  slot int not null check (slot between 1 and 10)
);

create table if not exists public.weekly_challenge_assignments (
  id uuid primary key,
  profile_id uuid not null references public.profiles (id) on delete cascade,
  iso_year int not null,
  iso_week int not null,
  slot int not null check (slot between 1 and 10),
  template_id text not null references public.challenge_templates (id),
  target_json jsonb,
  status text not null check (status in ('offered', 'active', 'succeeded', 'expired')),
  started_utc timestamptz,
  expires_utc timestamptz not null,
  unique (profile_id, iso_year, iso_week, slot)
);

create table if not exists public.challenge_point_awards (
  assignment_id uuid primary key references public.weekly_challenge_assignments (id) on delete restrict,
  profile_id uuid not null references public.profiles (id) on delete cascade,
  points int not null,
  awarded_utc timestamptz not null default now()
);

insert into public.challenge_templates (id, family, slot) values
  ('strength-1', 'strength', 1),
  ('strength-2', 'strength', 2),
  ('session-volume', 'session_volume', 3),
  ('week-volume', 'week_volume', 4),
  ('variety-exercises', 'exercise_variety', 5),
  ('variety-groups', 'group_variety', 6),
  ('targeted-muscle', 'targeted_muscle', 7),
  ('progression', 'progression', 8),
  ('running', 'running_distance', 9),
  ('pace', 'cardio_pace', 10)
on conflict (id) do nothing;

alter table public.challenge_templates enable row level security;
alter table public.weekly_challenge_assignments enable row level security;
alter table public.challenge_point_awards enable row level security;

drop policy if exists challenge_templates_read on public.challenge_templates;
create policy challenge_templates_read on public.challenge_templates
  for select to authenticated
  using (auth.uid() is not null);

drop policy if exists weekly_challenge_assignments_select on public.weekly_challenge_assignments;
create policy weekly_challenge_assignments_select on public.weekly_challenge_assignments
  for select to authenticated
  using (profile_id = auth.uid());

drop policy if exists weekly_challenge_assignments_insert on public.weekly_challenge_assignments;
create policy weekly_challenge_assignments_insert on public.weekly_challenge_assignments
  for insert to authenticated
  with check (profile_id = auth.uid());

drop policy if exists weekly_challenge_assignments_update on public.weekly_challenge_assignments;
create policy weekly_challenge_assignments_update on public.weekly_challenge_assignments
  for update to authenticated
  using (profile_id = auth.uid())
  with check (profile_id = auth.uid());

drop policy if exists challenge_point_awards_select on public.challenge_point_awards;
create policy challenge_point_awards_select on public.challenge_point_awards
  for select to authenticated
  using (profile_id = auth.uid());

drop policy if exists challenge_point_awards_insert on public.challenge_point_awards;
create policy challenge_point_awards_insert on public.challenge_point_awards
  for insert to authenticated
  with check (profile_id = auth.uid());

drop policy if exists challenge_point_awards_update on public.challenge_point_awards;
create policy challenge_point_awards_update on public.challenge_point_awards
  for update to authenticated
  using (profile_id = auth.uid())
  with check (profile_id = auth.uid());

create or replace function public.freeze_weekly_challenge_assignment()
returns trigger
language plpgsql
as $$
begin
  if old.target_json is not null and new.target_json is distinct from old.target_json then
    raise exception 'challenge target is frozen';
  end if;
  if old.started_utc is not null and new.started_utc is distinct from old.started_utc then
    raise exception 'challenge start is frozen';
  end if;
  if old.status <> 'offered' then
    if new.template_id is distinct from old.template_id
       or new.slot is distinct from old.slot
       or new.iso_year is distinct from old.iso_year
       or new.iso_week is distinct from old.iso_week
       or new.profile_id is distinct from old.profile_id
       or new.expires_utc is distinct from old.expires_utc
    then
      raise exception 'challenge assignment is frozen';
    end if;
  end if;
  return new;
end;
$$;

drop trigger if exists weekly_challenge_assignments_freeze on public.weekly_challenge_assignments;
create trigger weekly_challenge_assignments_freeze
  before update on public.weekly_challenge_assignments
  for each row execute function public.freeze_weekly_challenge_assignment();

create or replace function public.guard_challenge_award_update()
returns trigger
language plpgsql
as $$
declare
  fam text;
  exp timestamptz;
begin
  if new.assignment_id is distinct from old.assignment_id
     or new.profile_id is distinct from old.profile_id then
    raise exception 'award identity is frozen';
  end if;
  select t.family, a.expires_utc into fam, exp
  from public.weekly_challenge_assignments a
  join public.challenge_templates t on t.id = a.template_id
  where a.id = old.assignment_id;
  if fam is distinct from 'running_distance' or exp <= now() then
    raise exception 'award is frozen';
  end if;
  return new;
end;
$$;

drop trigger if exists challenge_point_awards_guard on public.challenge_point_awards;
create trigger challenge_point_awards_guard
  before update on public.challenge_point_awards
  for each row execute function public.guard_challenge_award_update();

grant select on public.challenge_templates to authenticated;
grant select, insert, update on public.weekly_challenge_assignments to authenticated;
grant select, insert, update on public.challenge_point_awards to authenticated;
