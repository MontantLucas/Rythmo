-- Rhythmo V2 — schéma Supabase (PostgreSQL)
-- Exécuter dans Supabase → SQL Editor après création du projet.

create extension if not exists "pgcrypto";

-- Profil métier lié à auth.users
create table if not exists public.profiles (
  id uuid primary key references auth.users (id) on delete cascade,
  display_name text not null default '',
  biological_sex smallint not null default 0,
  weight_kg double precision not null default 75,
  height_cm double precision,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table if not exists public.exercises (
  id uuid primary key,
  name_fr text not null,
  category text,
  met_approx double precision not null default 3.5,
  is_builtin boolean not null default false,
  created_by uuid references auth.users (id) on delete set null,
  created_at timestamptz not null default now()
);

create table if not exists public.session_templates (
  id uuid primary key default gen_random_uuid(),
  owner_id uuid not null references public.profiles (id) on delete cascade,
  title text not null,
  created_utc timestamptz not null default now(),
  updated_utc timestamptz not null default now()
);

create table if not exists public.session_exercises (
  id uuid primary key default gen_random_uuid(),
  session_id uuid not null references public.session_templates (id) on delete cascade,
  exercise_id uuid not null references public.exercises (id),
  sort_order int not null,
  target_sets int not null default 3,
  target_reps int default 10,
  unique (session_id, sort_order)
);

create table if not exists public.session_last_snapshots (
  session_id uuid primary key references public.session_templates (id) on delete cascade,
  json text not null default '{}',
  saved_utc timestamptz not null default now()
);

create table if not exists public.completed_workouts (
  id uuid primary key default gen_random_uuid(),
  profile_id uuid not null references public.profiles (id) on delete cascade,
  completed_utc timestamptz not null,
  calories_rounded double precision not null,
  session_title text not null default '',
  source_session_template_id uuid,
  payload_json text
);

create index if not exists ix_completed_workouts_profile_completed
  on public.completed_workouts (profile_id, completed_utc desc);

create table if not exists public.exercise_last_weights (
  profile_id uuid not null references public.profiles (id) on delete cascade,
  exercise_id uuid not null references public.exercises (id),
  weight_kg double precision not null,
  updated_utc timestamptz not null default now(),
  primary key (profile_id, exercise_id)
);

create table if not exists public.exercise_performance_daily (
  profile_id uuid not null references public.profiles (id) on delete cascade,
  exercise_id uuid not null references public.exercises (id),
  performance_date date not null,
  max_weight_kg double precision not null,
  primary key (profile_id, exercise_id, performance_date)
);

create index if not exists ix_exercise_performance_daily_profile_date
  on public.exercise_performance_daily (profile_id, performance_date);

-- Amitiés (stats entre amis)
do $do$
begin
  create type public.friendship_status as enum ('pending', 'accepted', 'blocked');
exception
  when duplicate_object then null;
end
$do$;

create table if not exists public.friendships (
  id uuid primary key default gen_random_uuid(),
  requester_id uuid not null references public.profiles (id) on delete cascade,
  addressee_id uuid not null references public.profiles (id) on delete cascade,
  status public.friendship_status not null default 'pending',
  created_at timestamptz not null default now(),
  unique (requester_id, addressee_id),
  check (requester_id <> addressee_id)
);

create index if not exists ix_friendships_addressee on public.friendships (addressee_id, status);

-- Auto-créer le profil à l'inscription
create or replace function public.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
  insert into public.profiles (id, display_name)
  values (
    new.id,
    coalesce(nullif(trim(new.raw_user_meta_data->>'display_name'), ''), split_part(new.email, '@', 1))
  )
  on conflict (id) do nothing;
  return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
  after insert on auth.users
  for each row execute function public.handle_new_user();

-- RLS
alter table public.profiles enable row level security;
alter table public.exercises enable row level security;
alter table public.session_templates enable row level security;
alter table public.session_exercises enable row level security;
alter table public.session_last_snapshots enable row level security;
alter table public.completed_workouts enable row level security;
alter table public.exercise_last_weights enable row level security;
alter table public.exercise_performance_daily enable row level security;
alter table public.friendships enable row level security;

-- Profils : soi + amis acceptés (lecture stats)
create policy profiles_select_own on public.profiles
  for select using (auth.uid() = id);

create policy profiles_select_friends on public.profiles
  for select using (
    exists (
      select 1 from public.friendships f
      where f.status = 'accepted'
        and (
          (f.requester_id = auth.uid() and f.addressee_id = profiles.id)
          or (f.addressee_id = auth.uid() and f.requester_id = profiles.id)
        )
    )
  );

create policy profiles_update_own on public.profiles
  for update using (auth.uid() = id);

-- Exercices : catalogue global + perso
create policy exercises_select_all on public.exercises
  for select using (is_builtin or created_by is null or created_by = auth.uid());

create policy exercises_insert_own on public.exercises
  for insert with check (created_by = auth.uid());

-- Données utilisateur
create policy session_templates_own on public.session_templates
  for all using (owner_id = auth.uid()) with check (owner_id = auth.uid());

create policy session_exercises_own on public.session_exercises
  for all using (
    exists (
      select 1 from public.session_templates t
      where t.id = session_exercises.session_id and t.owner_id = auth.uid()
    )
  );

create policy session_snapshots_own on public.session_last_snapshots
  for all using (
    exists (
      select 1 from public.session_templates t
      where t.id = session_last_snapshots.session_id and t.owner_id = auth.uid()
    )
  );

create policy completed_workouts_own on public.completed_workouts
  for all using (profile_id = auth.uid()) with check (profile_id = auth.uid());

create policy completed_workouts_friend_read on public.completed_workouts
  for select using (
    exists (
      select 1 from public.friendships f
      where f.status = 'accepted'
        and profile_id <> auth.uid()
        and (
          (f.requester_id = auth.uid() and f.addressee_id = completed_workouts.profile_id)
          or (f.addressee_id = auth.uid() and f.requester_id = completed_workouts.profile_id)
        )
    )
  );

create policy exercise_last_weights_own on public.exercise_last_weights
  for all using (profile_id = auth.uid()) with check (profile_id = auth.uid());

create policy exercise_performance_daily_own on public.exercise_performance_daily
  for all using (profile_id = auth.uid()) with check (profile_id = auth.uid());

create policy friendships_select on public.friendships
  for select using (requester_id = auth.uid() or addressee_id = auth.uid());

create policy friendships_insert on public.friendships
  for insert with check (requester_id = auth.uid());

create policy friendships_update_addressee on public.friendships
  for update using (addressee_id = auth.uid());

-- Vue stats amis (7 derniers jours)
create or replace view public.friend_weekly_leaderboard as
select
  p.id as friend_id,
  p.display_name,
  count(cw.id)::int as workout_count,
  coalesce(sum(
    case
      when cw.payload_json is null then 0
      else 0 -- volume calculé côté client pour l'instant
    end
  ), 0) as placeholder_volume,
  coalesce(sum(cw.calories_rounded), 0) as total_kcal
from public.profiles p
join public.friendships f on f.status = 'accepted'
  and ((f.requester_id = auth.uid() and f.addressee_id = p.id)
    or (f.addressee_id = auth.uid() and f.requester_id = p.id))
left join public.completed_workouts cw
  on cw.profile_id = p.id
  and cw.completed_utc >= (now() at time zone 'utc') - interval '7 days'
where p.id <> auth.uid()
group by p.id, p.display_name;

grant select on public.friend_weekly_leaderboard to authenticated;

-- Recherche profil par e-mail (invitation amis)
create or replace function public.find_profile_id_by_email(p_email text)
returns uuid
language sql
security definer
set search_path = public
as $$
  select u.id
  from auth.users u
  where lower(u.email) = lower(trim(p_email))
  limit 1;
$$;

grant execute on function public.find_profile_id_by_email(text) to authenticated;
