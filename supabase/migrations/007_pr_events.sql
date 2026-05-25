-- PR persistés : bests par exo (comparaison O(1)) + fil social (append-only)

do $do$
begin
  create type public.pr_event_kind as enum ('weight', 'reps', 'volume');
exception
  when duplicate_object then null;
end
$do$;

-- Meilleurs records actuels par utilisateur × exercice
create table if not exists public.exercise_personal_bests (
  profile_id uuid not null references public.profiles (id) on delete cascade,
  exercise_id uuid not null references public.exercises (id),
  max_kg double precision not null default 0,
  max_reps int not null default 0,
  max_session_volume double precision not null default 0,
  updated_utc timestamptz not null default now(),
  primary key (profile_id, exercise_id)
);

create index if not exists ix_exercise_personal_bests_profile
  on public.exercise_personal_bests (profile_id);

-- Événements PR (feed Amis) — une ligne à chaque record battu
create table if not exists public.pr_events (
  id uuid primary key default gen_random_uuid(),
  profile_id uuid not null references public.profiles (id) on delete cascade,
  exercise_id uuid not null references public.exercises (id),
  kind public.pr_event_kind not null,
  weight_kg double precision,
  reps int,
  performance_line text not null default '',
  completed_workout_id uuid references public.completed_workouts (id) on delete set null,
  achieved_utc timestamptz not null default now()
);

create index if not exists ix_pr_events_achieved_utc
  on public.pr_events (achieved_utc desc);

create index if not exists ix_pr_events_profile_achieved
  on public.pr_events (profile_id, achieved_utc desc);

-- RLS
alter table public.exercise_personal_bests enable row level security;
alter table public.pr_events enable row level security;

-- exercise_personal_bests : soi
drop policy if exists exercise_personal_bests_own on public.exercise_personal_bests;
create policy exercise_personal_bests_own on public.exercise_personal_bests
  for all to authenticated
  using (profile_id = auth.uid())
  with check (profile_id = auth.uid());

drop policy if exists exercise_personal_bests_friend_read on public.exercise_personal_bests;
create policy exercise_personal_bests_friend_read on public.exercise_personal_bests
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

-- pr_events : soi (lecture + insertion à la finalisation de séance)
drop policy if exists pr_events_own on public.pr_events;
create policy pr_events_own on public.pr_events
  for all to authenticated
  using (profile_id = auth.uid())
  with check (profile_id = auth.uid());

drop policy if exists pr_events_friend_read on public.pr_events;
create policy pr_events_friend_read on public.pr_events
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
