-- Ranking musculaire V1 : groupes, muscles, standards versionnés, rangs user, quêtes.

create table if not exists public.muscle_groups (
  id text primary key,
  name_fr text not null,
  sort_order int not null
);

create table if not exists public.muscles (
  id text primary key,
  group_id text not null references public.muscle_groups (id) on delete cascade,
  name_fr text not null,
  sort_order int not null
);

create table if not exists public.strength_standard_versions (
  id text primary key,
  version text not null,
  source text not null default '',
  created_at timestamptz not null default now(),
  is_active boolean not null default false,
  notes text not null default ''
);

create table if not exists public.muscle_group_coefficients (
  version_id text not null references public.strength_standard_versions (id) on delete cascade,
  muscle_id text not null references public.muscles (id) on delete cascade,
  coefficient double precision not null,
  primary key (version_id, muscle_id)
);

create table if not exists public.exercise_muscle_roles (
  exercise_id uuid not null references public.exercises (id) on delete cascade,
  muscle_id text not null references public.muscles (id) on delete cascade,
  role text not null check (role in ('primary', 'secondary')),
  primary key (exercise_id, muscle_id)
);

create table if not exists public.exercise_rank_standards (
  version_id text not null references public.strength_standard_versions (id) on delete cascade,
  exercise_id uuid not null references public.exercises (id) on delete cascade,
  measurement_type text not null check (measurement_type in ('1rm', '5rm', '10rm')),
  load_mode text not null check (load_mode in ('total', 'per_dumbbell', 'added_load')),
  comparability text not null check (comparability in ('high', 'medium', 'low')),
  source text not null,
  confidence text not null default 'medium',
  r10_male_70 double precision not null,
  r10_male_100 double precision not null,
  r10_female_60 double precision not null,
  r10_female_90 double precision not null,
  primary key (version_id, exercise_id)
);

-- Points de référence extensibles (V2 : ajouter H85 etc.)
create table if not exists public.exercise_rank_standard_points (
  version_id text not null references public.strength_standard_versions (id) on delete cascade,
  exercise_id uuid not null references public.exercises (id) on delete cascade,
  biological_sex smallint not null,
  bodyweight_kg double precision not null,
  r10_kg double precision not null,
  primary key (version_id, exercise_id, biological_sex, bodyweight_kg)
);

create table if not exists public.profile_exercise_ranks (
  profile_id uuid not null references public.profiles (id) on delete cascade,
  exercise_id uuid not null references public.exercises (id) on delete cascade,
  theoretical_raw double precision not null default 0,
  theoretical_rank int,
  validated_rank int,
  has_successful_validation boolean not null default false,
  calibration_floor int,
  available_quest_rank int,
  quest_unlocked_utc timestamptz,
  bodyweight_kg_at_compute double precision,
  sex_at_compute smallint,
  r10_used double precision,
  standard_version_id text references public.strength_standard_versions (id),
  updated_utc timestamptz not null default now(),
  primary key (profile_id, exercise_id)
);

create table if not exists public.profile_muscle_rank_snapshots (
  profile_id uuid not null references public.profiles (id) on delete cascade,
  muscle_id text not null references public.muscles (id) on delete cascade,
  standard_version_id text not null references public.strength_standard_versions (id),
  validated_rank int,
  theoretical_rank int,
  evaluated_count int not null default 0,
  updated_utc timestamptz not null default now(),
  primary key (profile_id, muscle_id, standard_version_id)
);

create table if not exists public.profile_group_rank_snapshots (
  profile_id uuid not null references public.profiles (id) on delete cascade,
  group_id text not null references public.muscle_groups (id) on delete cascade,
  standard_version_id text not null references public.strength_standard_versions (id),
  validated_raw double precision,
  validated_rank int,
  evaluated_count int not null default 0,
  total_count int not null default 0,
  updated_utc timestamptz not null default now(),
  primary key (profile_id, group_id, standard_version_id)
);

create table if not exists public.rank_quest_attempts (
  id uuid primary key default gen_random_uuid(),
  profile_id uuid not null references public.profiles (id) on delete cascade,
  exercise_id uuid not null references public.exercises (id),
  target_rank int not null,
  started_utc timestamptz not null default now(),
  expires_utc timestamptz not null,
  local_date date not null,
  status text not null check (status in ('in_progress', 'succeeded', 'failed_expired', 'failed_user')),
  weight_kg double precision,
  reps int,
  standard_version_id text references public.strength_standard_versions (id)
);

create index if not exists ix_rank_quest_attempts_profile_date
  on public.rank_quest_attempts (profile_id, exercise_id, local_date);

create index if not exists ix_profile_exercise_ranks_profile
  on public.profile_exercise_ranks (profile_id);

-- Catalogue
insert into public.muscle_groups (id, name_fr, sort_order) values
  ('chest', 'Pectoraux', 1),
  ('back', 'Dos', 2),
  ('shoulders', 'Épaules', 3),
  ('arms', 'Bras', 4),
  ('abs', 'Abdominaux', 5),
  ('legs', 'Jambes', 6)
on conflict (id) do nothing;

insert into public.muscles (id, group_id, name_fr, sort_order) values
  ('upper_chest', 'chest', 'Pectoraux supérieurs', 1),
  ('mid_chest', 'chest', 'Pectoraux sternocostaux', 2),
  ('lats', 'back', 'Grand dorsal', 1),
  ('traps', 'back', 'Trapèzes', 2),
  ('rhomboids', 'back', 'Rhomboïdes', 3),
  ('erectors', 'back', 'Érecteurs', 4),
  ('front_delt', 'shoulders', 'Deltoïde antérieur', 1),
  ('side_delt', 'shoulders', 'Deltoïde latéral', 2),
  ('rear_delt', 'shoulders', 'Deltoïde postérieur', 3),
  ('biceps', 'arms', 'Biceps', 1),
  ('triceps', 'arms', 'Triceps', 2),
  ('abs_muscle', 'abs', 'Abdominaux', 1),
  ('quads', 'legs', 'Quadriceps', 1),
  ('hamstrings', 'legs', 'Ischio-jambiers', 2),
  ('glutes', 'legs', 'Fessiers', 3),
  ('calves', 'legs', 'Mollets', 4),
  ('adductors', 'legs', 'Adducteurs', 5)
on conflict (id) do nothing;

insert into public.strength_standard_versions (id, version, source, is_active, notes) values
  ('rhythmo-v1', 'rhythmo-v1', 'strength_level+rhythmo_manual', true,
   'Standards V1. Strength Level Elite = R10. Ne pas recalculer les historiques si une V2 devient active.')
on conflict (id) do nothing;

insert into public.muscle_group_coefficients (version_id, muscle_id, coefficient) values
  ('rhythmo-v1', 'mid_chest', 0.65),
  ('rhythmo-v1', 'upper_chest', 0.35),
  ('rhythmo-v1', 'lats', 0.40),
  ('rhythmo-v1', 'erectors', 0.25),
  ('rhythmo-v1', 'traps', 0.22),
  ('rhythmo-v1', 'rhomboids', 0.13),
  ('rhythmo-v1', 'side_delt', 0.38),
  ('rhythmo-v1', 'front_delt', 0.34),
  ('rhythmo-v1', 'rear_delt', 0.28),
  ('rhythmo-v1', 'triceps', 0.60),
  ('rhythmo-v1', 'biceps', 0.40),
  ('rhythmo-v1', 'abs_muscle', 1.00),
  ('rhythmo-v1', 'quads', 0.28),
  ('rhythmo-v1', 'glutes', 0.26),
  ('rhythmo-v1', 'hamstrings', 0.20),
  ('rhythmo-v1', 'calves', 0.14),
  ('rhythmo-v1', 'adductors', 0.12)
on conflict (version_id, muscle_id) do nothing;

-- RLS
alter table public.muscle_groups enable row level security;
alter table public.muscles enable row level security;
alter table public.strength_standard_versions enable row level security;
alter table public.muscle_group_coefficients enable row level security;
alter table public.exercise_muscle_roles enable row level security;
alter table public.exercise_rank_standards enable row level security;
alter table public.exercise_rank_standard_points enable row level security;
alter table public.profile_exercise_ranks enable row level security;
alter table public.profile_muscle_rank_snapshots enable row level security;
alter table public.profile_group_rank_snapshots enable row level security;
alter table public.rank_quest_attempts enable row level security;

drop policy if exists muscle_groups_read on public.muscle_groups;
create policy muscle_groups_read on public.muscle_groups
  for select to authenticated using (true);

drop policy if exists muscles_read on public.muscles;
create policy muscles_read on public.muscles
  for select to authenticated using (true);

drop policy if exists strength_standard_versions_read on public.strength_standard_versions;
create policy strength_standard_versions_read on public.strength_standard_versions
  for select to authenticated using (true);

drop policy if exists muscle_group_coefficients_read on public.muscle_group_coefficients;
create policy muscle_group_coefficients_read on public.muscle_group_coefficients
  for select to authenticated using (true);

drop policy if exists exercise_muscle_roles_read on public.exercise_muscle_roles;
create policy exercise_muscle_roles_read on public.exercise_muscle_roles
  for select to authenticated using (true);

drop policy if exists exercise_rank_standards_read on public.exercise_rank_standards;
create policy exercise_rank_standards_read on public.exercise_rank_standards
  for select to authenticated using (true);

drop policy if exists exercise_rank_standard_points_read on public.exercise_rank_standard_points;
create policy exercise_rank_standard_points_read on public.exercise_rank_standard_points
  for select to authenticated using (true);

drop policy if exists profile_exercise_ranks_own on public.profile_exercise_ranks;
create policy profile_exercise_ranks_own on public.profile_exercise_ranks
  for all to authenticated
  using (profile_id = auth.uid())
  with check (profile_id = auth.uid());

drop policy if exists profile_muscle_rank_snapshots_own on public.profile_muscle_rank_snapshots;
create policy profile_muscle_rank_snapshots_own on public.profile_muscle_rank_snapshots
  for all to authenticated
  using (profile_id = auth.uid())
  with check (profile_id = auth.uid());

drop policy if exists profile_group_rank_snapshots_own on public.profile_group_rank_snapshots;
create policy profile_group_rank_snapshots_own on public.profile_group_rank_snapshots
  for all to authenticated
  using (profile_id = auth.uid())
  with check (profile_id = auth.uid());

drop policy if exists rank_quest_attempts_own on public.rank_quest_attempts;
create policy rank_quest_attempts_own on public.rank_quest_attempts
  for all to authenticated
  using (profile_id = auth.uid())
  with check (profile_id = auth.uid());

grant select on public.muscle_groups, public.muscles, public.strength_standard_versions,
  public.muscle_group_coefficients, public.exercise_muscle_roles, public.exercise_rank_standards,
  public.exercise_rank_standard_points to authenticated;

grant select, insert, update, delete on public.profile_exercise_ranks,
  public.profile_muscle_rank_snapshots, public.profile_group_rank_snapshots,
  public.rank_quest_attempts to authenticated;

-- Exercices ranking V1 (insert idempotent).
insert into public.exercises (id, name_fr, category, met_approx, is_builtin, created_by) values
  ('79e55bab-97e1-dc30-d8a7-97ecc20c1f27', 'Développé couché barre', 'Pectoraux', 6.2, true, null)
 ,('bc657db8-3d75-dd06-e5f9-5187d87959c0', 'Développé couché haltères', 'Pectoraux', 6.2, true, null)
 ,('c12df4f7-a460-1d07-84ac-d8ce35b0fd9c', 'Développé incliné', 'Pectoraux', 6.2, true, null)
 ,('a9369ee8-12cc-f4fe-d536-13ccfd4afd1b', 'Développé incliné haltères', 'Pectoraux', 6.2, true, null)
 ,('8d16ccc5-52d5-0a3c-16cc-f250ea37e584', 'Développé décliné', 'Pectoraux', 6.2, true, null)
 ,('52eacc40-9cf8-3ce7-10a2-ac8655e2cccf', 'Développé décliné haltères', 'Pectoraux', 6.2, true, null)
 ,('b737af0a-5e89-de30-fe35-f1a1a9a08b91', 'Chest press', 'Pectoraux', 6.2, true, null)
 ,('472a9190-a34e-5d31-f4a4-efdc6ca235d1', 'Dips', 'Pectoraux', 7, true, null)
 ,('e676fe2a-823d-6e41-bad6-9270799ea302', 'Écarté haltères', 'Pectoraux', 4.8, true, null)
 ,('e24f35c4-d576-ad68-f358-e5d835582413', 'Écarté poulie', 'Pectoraux', 4.8, true, null)
 ,('061929b0-e876-d551-a978-831c4e4a6b2e', 'Écarté haltères incliné', 'Pectoraux', 4.8, true, null)
 ,('6e5c82e4-43b7-49cc-d713-f10b55d540d3', 'Écarté poulie incliné', 'Pectoraux', 4.8, true, null)
 ,('f7a6ef9f-3c3b-0508-f4b7-c527eb71580b', 'Écarté haltères décliné', 'Pectoraux', 4.8, true, null)
 ,('d10f7b90-bbb8-a91e-d8f4-5ed8010425a9', 'Pec deck / Butterfly', 'Pectoraux', 4.8, true, null)
 ,('3db750a3-36b1-76c9-f38f-60642fa902ef', 'Pompes', 'Pectoraux', 7, true, null)
 ,('c217bc09-fe05-5525-0bdf-bb8ffb826417', 'Tractions', 'Dos', 7.2, true, null)
 ,('c288a105-23fd-abc8-161c-07c5b7442a6a', 'Tractions lestées', 'Dos', 7.2, true, null)
 ,('edb1b64a-665c-23dc-8aed-4f74929dba29', 'Muscle-up', 'Dos', 8.8, true, null)
 ,('b3316450-c4b0-92b4-8075-24fe9eeb8196', 'Tirage vertical', 'Dos', 7.2, true, null)
 ,('85633508-9d8f-81da-83ad-48cb6932ab1b', 'Tirage vertical prise serrée', 'Dos', 7.2, true, null)
 ,('84a4c47c-8c65-2273-26cb-0516e0e0460c', 'Rowing barre', 'Dos', 7.5, true, null)
 ,('33cc9252-6aa4-c063-2ba5-7b4e3c12860d', 'Rowing haltère', 'Dos', 7.5, true, null)
 ,('4bee2133-934e-5fc5-8759-2c16835f3ab2', 'Tirage horizontal', 'Dos', 7.2, true, null)
 ,('81a892fe-b00b-9bc3-5e8e-21d0624b88a1', 'Rowing poulie unilatéral', 'Dos', 7.5, true, null)
 ,('a9400ccf-a128-7df1-48d1-e58c325c6382', 'Soulevé de terre', 'Dos', 8, true, null)
 ,('a53f6ae0-b0c0-fdbe-a541-fbc4b9909bb8', 'Pullover', 'Dos', 7.2, true, null)
 ,('7b310e3b-b7ad-933f-3ea0-c0a38d447cdd', 'Shrug barre', 'Dos', 7.2, true, null)
 ,('f298e620-3c61-8dac-ffe3-8210a0dc4dcb', 'Shrug haltères', 'Dos', 7.2, true, null)
 ,('2873fc2c-082c-034a-f508-9e2addde770a', 'Shrug machine', 'Dos', 7.2, true, null)
 ,('28d0764e-89de-72c5-b64c-d8205ff35121', 'Extension lombaire machine', 'Dos', 7.2, true, null)
 ,('9a6fa0e0-5226-ec96-5609-abc061ec1620', 'Rowing T-Bar', 'Dos', 7.5, true, null)
 ,('8680771f-adb7-d5d9-1f68-9c18b3631cb4', 'Landmine 180', 'Dos', 7.2, true, null)
 ,('595502e9-f837-2fbc-34cc-b17da659aac9', 'Développé militaire', 'Épaules', 7, true, null)
 ,('354fde39-bbb2-9af3-01e4-a5e3f2c24514', 'Développé militaire haltères', 'Épaules', 7, true, null)
 ,('ab52539f-7e75-7aa6-9e7b-7fa81b84aa8c', 'Shoulder press', 'Épaules', 7, true, null)
 ,('772b896e-dd26-d4b5-07f9-25715dcb300e', 'Élévations latérales', 'Épaules', 7, true, null)
 ,('bb42b14b-ebf5-6e8f-d494-ccdfb39a1026', 'Élévations latérales poulie', 'Épaules', 7, true, null)
 ,('a58cc0d9-ef75-70f7-326a-83a7a3674ecb', 'Élévations latérales machine', 'Épaules', 7, true, null)
 ,('41bb4b82-49ab-6e5e-372e-198f7d3842fb', 'Élévations frontales', 'Épaules', 7, true, null)
 ,('6d651f61-eb64-3b39-f88e-c8a702de37db', 'Oiseau', 'Épaules', 7, true, null)
 ,('1edae2a0-f2f0-7844-0477-f4e7fb01dfd8', 'Oiseau poulie', 'Épaules', 7, true, null)
 ,('370883a2-713a-e1cc-5efe-cd96d3a981bb', 'Reverse pec deck', 'Épaules', 7, true, null)
 ,('f55ca830-e71f-0ea3-3f07-daf078df85dd', 'Face pull', 'Épaules', 7, true, null)
 ,('4dc084d7-0602-1d61-8375-3d605249412b', 'Tirage menton', 'Épaules', 7, true, null)
 ,('0c7ca7e4-f797-1033-f59c-dc8aa9e4aaea', 'Arnold press', 'Épaules', 7, true, null)
 ,('3ca79fb0-d8b1-3ca5-ef1a-d712cf67e49a', 'Curl barre', 'Biceps', 5.5, true, null)
 ,('b24cffe6-a60f-14f4-c411-ed7794e1f903', 'Curl haltères', 'Biceps', 5.5, true, null)
 ,('0e42e400-ee35-3cec-c0bc-81ca4b8a2699', 'Curl incliné', 'Biceps', 5.5, true, null)
 ,('19dcc78f-4acb-670a-4716-26ef14480c3a', 'Curl marteau', 'Biceps', 5.5, true, null)
 ,('a764ce72-1cd5-361a-1f5c-e80c692faafe', 'Curl pupitre', 'Biceps', 5.5, true, null)
 ,('9033928d-644f-44e5-980e-c05f32efd975', 'Curl poulie', 'Biceps', 5.5, true, null)
 ,('348fe02c-c76b-f634-39d8-0f6f81b8a019', 'Barre au front', 'Triceps', 5.5, true, null)
 ,('e91413e8-9a9b-2618-ba8a-bfcd75d84692', 'Extension poulie', 'Triceps', 5.5, true, null)
 ,('961a6e90-9a97-9c99-9e45-225840c37790', 'Dips', 'Triceps', 5.5, true, null)
 ,('6b60f33a-eb19-b21a-c1f5-00e8f391f5ba', 'Extension haltère derrière la tête', 'Triceps', 5.5, true, null)
 ,('a90dafea-187a-8496-9ddc-94293f2a3e7d', 'Pushdown corde', 'Triceps', 5.5, true, null)
 ,('7b1be08f-2a43-05e9-09fd-07aa830026d9', 'Extension overhead poulie', 'Triceps', 5.5, true, null)
 ,('641bd507-78fb-bf70-aa2b-cad4292ed8a4', 'Développé serré', 'Triceps', 5.5, true, null)
 ,('63a7a51f-59e4-f0a4-d185-36b3a7c3ffc5', 'Squat', 'Jambes — Quadriceps', 8.4, true, null)
 ,('ded5de77-50b2-d373-d6de-cf27146e9691', 'Front squat', 'Jambes — Quadriceps', 8.4, true, null)
 ,('74f2de2b-e0aa-2ffd-c3be-bd0cc5cb0bf8', 'Presse à cuisses', 'Jambes — Quadriceps', 8.4, true, null)
 ,('840a6fe8-8555-14ea-57bb-b9be7ef7f94f', 'Hack squat', 'Jambes — Quadriceps', 8.4, true, null)
 ,('a8943372-cb78-981c-84a8-27f08685dc88', 'Fentes', 'Jambes — Quadriceps', 8.4, true, null)
 ,('e8571544-d370-2b20-eb00-1fef9eeba996', 'Bulgarian split squat', 'Jambes — Quadriceps', 6.8, true, null)
 ,('37c3e6d6-cc24-e932-2258-c44971ed301a', 'Leg extension', 'Jambes — Quadriceps', 6.8, true, null)
 ,('5e87d435-0bd6-314b-f52a-1eecbb981e50', 'Sissy squat', 'Jambes — Quadriceps', 5.9, true, null)
 ,('4acc71c0-f806-9aea-401a-66720df3ceda', 'Adducteur machine', 'Jambes — Quadriceps', 8.4, true, null)
 ,('30bc3acd-b856-e299-952b-bdc99dc990dc', 'Adduction hanche poulie', 'Jambes — Quadriceps', 8.4, true, null)
 ,('6b06e2e8-68dd-c1db-8001-05c8395130ef', 'Soulevé de terre roumain', 'Ischios / Fessiers', 8, true, null)
 ,('c698362d-13c5-c3eb-e766-e4dae970e3e8', 'Leg curl', 'Ischios / Fessiers', 6, true, null)
 ,('761a4b61-ba7c-de0f-6a57-06e46df25b01', 'Hip thrust', 'Ischios / Fessiers', 8, true, null)
 ,('22028389-b3f8-95b2-618a-4faeac7e37da', 'Good morning', 'Ischios / Fessiers', 8, true, null)
 ,('915c03e0-8d2c-1269-ad52-7f44cd7a8070', 'Fentes marchées', 'Ischios / Fessiers', 8, true, null)
 ,('b36c7bc2-7730-0611-9f35-c4af4c58c389', 'Glute bridge', 'Ischios / Fessiers', 8, true, null)
 ,('a428c23b-972b-107f-f8af-1bf868753bd7', 'Kickback poulie', 'Ischios / Fessiers', 6, true, null)
 ,('5e46c87f-8ae0-86d2-f858-6a259bb59abf', 'Mollets machine', 'Mollets', 5.2, true, null)
 ,('4aa204fd-ee5c-30af-105a-428569405f3c', 'Mollets smith', 'Mollets', 5.2, true, null)
 ,('0ee1fa6e-69e7-3e1b-71e1-40e86fc925bd', 'Mollets à la presse', 'Mollets', 5.2, true, null)
 ,('8b8eee3c-8f20-1528-0ccf-ecbc0714d437', 'Crunch', 'Abdominaux', 4.2, true, null)
 ,('7cfc3cd3-3254-2560-9227-fac64a99cd0b', 'Crunch poulie', 'Abdominaux', 4.2, true, null)
 ,('93eb818c-29c1-0e90-cbb9-87eed79fc85f', 'Crunch machine', 'Abdominaux', 4.2, true, null)
 ,('2d2a96d6-1cf1-8fdc-8b11-5c8cd735fc87', 'Crunch décliné lesté', 'Abdominaux', 4.2, true, null)
 ,('4e524f1a-acf2-6294-c6cc-6124d809f08d', 'Reverse crunch lesté', 'Abdominaux', 4.2, true, null)
 ,('4d3efd22-96f8-eb36-6c66-eb6c4a8ec210', 'Relevés de jambes', 'Abdominaux', 4.2, true, null)
 ,('8c39cecf-78bd-f7c2-d1e7-7f14f7f3d5ca', 'Relevés de jambes suspendu', 'Abdominaux', 4.2, true, null)
 ,('b6dafd25-ac20-29f4-8b3e-42f6204ff747', 'Gainage', 'Abdominaux', 4.2, true, null)
 ,('de5b9d97-a1e5-da39-29a5-4f58ae9d959f', 'Russian twist', 'Abdominaux', 4.2, true, null)
 ,('382d981d-a6d1-8758-7505-00a5d8fd7a12', 'Mountain climbers', 'Abdominaux', 4.2, true, null)
 ,('b7098e34-7b3d-c382-de33-e52ebdbc3656', 'Planche latérale', 'Abdominaux', 4.2, true, null)
 ,('1972d422-7150-09ba-73c8-51ee2c544f67', 'Ab wheel', 'Abdominaux', 4.2, true, null)
 ,('b48862de-a566-0933-dd0b-c5c8cbb40081', 'Hollow body hold', 'Abdominaux', 4.2, true, null)
 ,('0adc27a5-9ce3-9815-dad2-b54fa4783db1', 'Flexion latérale au banc', 'Abdominaux', 4.2, true, null)
 ,('9f1898e5-3b8e-ea4d-06a4-bb2823a42bc6', 'Tractions', 'Poids du corps / Street Workout', 7.5, true, null)
 ,('95014e00-1d37-d8ca-8c38-edb9be3a9de9', 'Muscle-up', 'Poids du corps / Street Workout', 7.5, true, null)
 ,('f85275ef-6f13-6090-34a8-a64e49f0f2d1', 'Dips', 'Poids du corps / Street Workout', 7.5, true, null)
 ,('5407fb94-7ec3-8e5f-f460-badaaa34059a', 'Pompes', 'Poids du corps / Street Workout', 7.5, true, null)
 ,('a9d8aaaf-e758-e5c9-de79-27df4e1eaba2', 'Pompes diamant', 'Poids du corps / Street Workout', 7.5, true, null)
 ,('5fa8f9a1-db9e-e694-2ec9-ae350b0085e0', 'Tractions australiennes', 'Poids du corps / Street Workout', 7.5, true, null)
 ,('68454a82-ae0c-7593-3c18-b746be4e4f24', 'L-sit', 'Poids du corps / Street Workout', 8, true, null)
 ,('d38be93a-7f82-a09d-10eb-46ed68a5446b', 'Front lever', 'Poids du corps / Street Workout', 9, true, null)
 ,('b20d0b3d-821d-0435-f8ee-d67e36c60e7c', 'Back lever', 'Poids du corps / Street Workout', 9, true, null)
 ,('64a8c55d-1c5e-2c94-3cc3-216a3654fc85', 'Handstand push-up', 'Poids du corps / Street Workout', 9, true, null)
on conflict (id) do nothing;

insert into public.exercise_muscle_roles (exercise_id, muscle_id, role) values
  ('c12df4f7-a460-1d07-84ac-d8ce35b0fd9c', 'upper_chest', 'primary')
 ,('c12df4f7-a460-1d07-84ac-d8ce35b0fd9c', 'mid_chest', 'primary')
 ,('a9369ee8-12cc-f4fe-d536-13ccfd4afd1b', 'upper_chest', 'primary')
 ,('a9369ee8-12cc-f4fe-d536-13ccfd4afd1b', 'mid_chest', 'primary')
 ,('79e55bab-97e1-dc30-d8a7-97ecc20c1f27', 'mid_chest', 'primary')
 ,('bc657db8-3d75-dd06-e5f9-5187d87959c0', 'mid_chest', 'primary')
 ,('8d16ccc5-52d5-0a3c-16cc-f250ea37e584', 'mid_chest', 'primary')
 ,('52eacc40-9cf8-3ce7-10a2-ac8655e2cccf', 'mid_chest', 'primary')
 ,('b737af0a-5e89-de30-fe35-f1a1a9a08b91', 'mid_chest', 'primary')
 ,('d10f7b90-bbb8-a91e-d8f4-5ed8010425a9', 'mid_chest', 'primary')
 ,('e676fe2a-823d-6e41-bad6-9270799ea302', 'mid_chest', 'primary')
 ,('e24f35c4-d576-ad68-f358-e5d835582413', 'mid_chest', 'primary')
 ,('061929b0-e876-d551-a978-831c4e4a6b2e', 'upper_chest', 'secondary')
 ,('061929b0-e876-d551-a978-831c4e4a6b2e', 'mid_chest', 'secondary')
 ,('6e5c82e4-43b7-49cc-d713-f10b55d540d3', 'upper_chest', 'secondary')
 ,('6e5c82e4-43b7-49cc-d713-f10b55d540d3', 'mid_chest', 'secondary')
 ,('f7a6ef9f-3c3b-0508-f4b7-c527eb71580b', 'mid_chest', 'secondary')
 ,('3db750a3-36b1-76c9-f38f-60642fa902ef', 'mid_chest', 'secondary')
 ,('3db750a3-36b1-76c9-f38f-60642fa902ef', 'front_delt', 'secondary')
 ,('3db750a3-36b1-76c9-f38f-60642fa902ef', 'triceps', 'secondary')
 ,('472a9190-a34e-5d31-f4a4-efdc6ca235d1', 'mid_chest', 'secondary')
 ,('472a9190-a34e-5d31-f4a4-efdc6ca235d1', 'triceps', 'secondary')
 ,('472a9190-a34e-5d31-f4a4-efdc6ca235d1', 'front_delt', 'secondary')
 ,('c288a105-23fd-abc8-161c-07c5b7442a6a', 'lats', 'primary')
 ,('c288a105-23fd-abc8-161c-07c5b7442a6a', 'biceps', 'secondary')
 ,('c288a105-23fd-abc8-161c-07c5b7442a6a', 'traps', 'secondary')
 ,('c288a105-23fd-abc8-161c-07c5b7442a6a', 'rhomboids', 'secondary')
 ,('c217bc09-fe05-5525-0bdf-bb8ffb826417', 'lats', 'secondary')
 ,('c217bc09-fe05-5525-0bdf-bb8ffb826417', 'biceps', 'secondary')
 ,('c217bc09-fe05-5525-0bdf-bb8ffb826417', 'traps', 'secondary')
 ,('c217bc09-fe05-5525-0bdf-bb8ffb826417', 'rhomboids', 'secondary')
 ,('85633508-9d8f-81da-83ad-48cb6932ab1b', 'lats', 'primary')
 ,('85633508-9d8f-81da-83ad-48cb6932ab1b', 'biceps', 'secondary')
 ,('b3316450-c4b0-92b4-8075-24fe9eeb8196', 'lats', 'primary')
 ,('b3316450-c4b0-92b4-8075-24fe9eeb8196', 'biceps', 'secondary')
 ,('84a4c47c-8c65-2273-26cb-0516e0e0460c', 'lats', 'primary')
 ,('84a4c47c-8c65-2273-26cb-0516e0e0460c', 'traps', 'primary')
 ,('84a4c47c-8c65-2273-26cb-0516e0e0460c', 'rhomboids', 'primary')
 ,('33cc9252-6aa4-c063-2ba5-7b4e3c12860d', 'lats', 'primary')
 ,('33cc9252-6aa4-c063-2ba5-7b4e3c12860d', 'traps', 'primary')
 ,('33cc9252-6aa4-c063-2ba5-7b4e3c12860d', 'rhomboids', 'primary')
 ,('4bee2133-934e-5fc5-8759-2c16835f3ab2', 'lats', 'primary')
 ,('4bee2133-934e-5fc5-8759-2c16835f3ab2', 'traps', 'primary')
 ,('4bee2133-934e-5fc5-8759-2c16835f3ab2', 'rhomboids', 'primary')
 ,('81a892fe-b00b-9bc3-5e8e-21d0624b88a1', 'lats', 'primary')
 ,('81a892fe-b00b-9bc3-5e8e-21d0624b88a1', 'traps', 'primary')
 ,('81a892fe-b00b-9bc3-5e8e-21d0624b88a1', 'rhomboids', 'primary')
 ,('a53f6ae0-b0c0-fdbe-a541-fbc4b9909bb8', 'lats', 'primary')
 ,('7b310e3b-b7ad-933f-3ea0-c0a38d447cdd', 'traps', 'primary')
 ,('f298e620-3c61-8dac-ffe3-8210a0dc4dcb', 'traps', 'primary')
 ,('2873fc2c-082c-034a-f508-9e2addde770a', 'traps', 'primary')
 ,('a9400ccf-a128-7df1-48d1-e58c325c6382', 'erectors', 'primary')
 ,('a9400ccf-a128-7df1-48d1-e58c325c6382', 'glutes', 'secondary')
 ,('a9400ccf-a128-7df1-48d1-e58c325c6382', 'hamstrings', 'secondary')
 ,('a9400ccf-a128-7df1-48d1-e58c325c6382', 'traps', 'secondary')
 ,('28d0764e-89de-72c5-b64c-d8205ff35121', 'erectors', 'primary')
 ,('9a6fa0e0-5226-ec96-5609-abc061ec1620', 'lats', 'secondary')
 ,('9a6fa0e0-5226-ec96-5609-abc061ec1620', 'traps', 'secondary')
 ,('9a6fa0e0-5226-ec96-5609-abc061ec1620', 'rhomboids', 'secondary')
 ,('edb1b64a-665c-23dc-8aed-4f74929dba29', 'lats', 'secondary')
 ,('edb1b64a-665c-23dc-8aed-4f74929dba29', 'triceps', 'secondary')
 ,('595502e9-f837-2fbc-34cc-b17da659aac9', 'front_delt', 'primary')
 ,('354fde39-bbb2-9af3-01e4-a5e3f2c24514', 'front_delt', 'primary')
 ,('ab52539f-7e75-7aa6-9e7b-7fa81b84aa8c', 'front_delt', 'primary')
 ,('772b896e-dd26-d4b5-07f9-25715dcb300e', 'side_delt', 'primary')
 ,('bb42b14b-ebf5-6e8f-d494-ccdfb39a1026', 'side_delt', 'primary')
 ,('a58cc0d9-ef75-70f7-326a-83a7a3674ecb', 'side_delt', 'primary')
 ,('370883a2-713a-e1cc-5efe-cd96d3a981bb', 'rear_delt', 'primary')
 ,('f55ca830-e71f-0ea3-3f07-daf078df85dd', 'rear_delt', 'primary')
 ,('f55ca830-e71f-0ea3-3f07-daf078df85dd', 'traps', 'secondary')
 ,('f55ca830-e71f-0ea3-3f07-daf078df85dd', 'rhomboids', 'secondary')
 ,('6d651f61-eb64-3b39-f88e-c8a702de37db', 'rear_delt', 'secondary')
 ,('1edae2a0-f2f0-7844-0477-f4e7fb01dfd8', 'rear_delt', 'secondary')
 ,('41bb4b82-49ab-6e5e-372e-198f7d3842fb', 'front_delt', 'secondary')
 ,('4dc084d7-0602-1d61-8375-3d605249412b', 'side_delt', 'secondary')
 ,('4dc084d7-0602-1d61-8375-3d605249412b', 'traps', 'secondary')
 ,('0c7ca7e4-f797-1033-f59c-dc8aa9e4aaea', 'front_delt', 'secondary')
 ,('3ca79fb0-d8b1-3ca5-ef1a-d712cf67e49a', 'biceps', 'primary')
 ,('b24cffe6-a60f-14f4-c411-ed7794e1f903', 'biceps', 'primary')
 ,('a764ce72-1cd5-361a-1f5c-e80c692faafe', 'biceps', 'primary')
 ,('9033928d-644f-44e5-980e-c05f32efd975', 'biceps', 'secondary')
 ,('0e42e400-ee35-3cec-c0bc-81ca4b8a2699', 'biceps', 'secondary')
 ,('19dcc78f-4acb-670a-4716-26ef14480c3a', 'biceps', 'secondary')
 ,('e91413e8-9a9b-2618-ba8a-bfcd75d84692', 'triceps', 'primary')
 ,('a90dafea-187a-8496-9ddc-94293f2a3e7d', 'triceps', 'primary')
 ,('7b1be08f-2a43-05e9-09fd-07aa830026d9', 'triceps', 'primary')
 ,('348fe02c-c76b-f634-39d8-0f6f81b8a019', 'triceps', 'secondary')
 ,('961a6e90-9a97-9c99-9e45-225840c37790', 'triceps', 'secondary')
 ,('961a6e90-9a97-9c99-9e45-225840c37790', 'mid_chest', 'secondary')
 ,('6b60f33a-eb19-b21a-c1f5-00e8f391f5ba', 'triceps', 'secondary')
 ,('641bd507-78fb-bf70-aa2b-cad4292ed8a4', 'triceps', 'secondary')
 ,('641bd507-78fb-bf70-aa2b-cad4292ed8a4', 'mid_chest', 'secondary')
 ,('7cfc3cd3-3254-2560-9227-fac64a99cd0b', 'abs_muscle', 'primary')
 ,('93eb818c-29c1-0e90-cbb9-87eed79fc85f', 'abs_muscle', 'primary')
 ,('2d2a96d6-1cf1-8fdc-8b11-5c8cd735fc87', 'abs_muscle', 'primary')
 ,('4e524f1a-acf2-6294-c6cc-6124d809f08d', 'abs_muscle', 'primary')
 ,('8b8eee3c-8f20-1528-0ccf-ecbc0714d437', 'abs_muscle', 'secondary')
 ,('4d3efd22-96f8-eb36-6c66-eb6c4a8ec210', 'abs_muscle', 'secondary')
 ,('8c39cecf-78bd-f7c2-d1e7-7f14f7f3d5ca', 'abs_muscle', 'secondary')
 ,('b6dafd25-ac20-29f4-8b3e-42f6204ff747', 'abs_muscle', 'secondary')
 ,('1972d422-7150-09ba-73c8-51ee2c544f67', 'abs_muscle', 'secondary')
 ,('de5b9d97-a1e5-da39-29a5-4f58ae9d959f', 'abs_muscle', 'secondary')
 ,('382d981d-a6d1-8758-7505-00a5d8fd7a12', 'abs_muscle', 'secondary')
 ,('b7098e34-7b3d-c382-de33-e52ebdbc3656', 'abs_muscle', 'secondary')
 ,('b48862de-a566-0933-dd0b-c5c8cbb40081', 'abs_muscle', 'secondary')
 ,('0adc27a5-9ce3-9815-dad2-b54fa4783db1', 'abs_muscle', 'secondary')
 ,('63a7a51f-59e4-f0a4-d185-36b3a7c3ffc5', 'quads', 'primary')
 ,('63a7a51f-59e4-f0a4-d185-36b3a7c3ffc5', 'glutes', 'primary')
 ,('840a6fe8-8555-14ea-57bb-b9be7ef7f94f', 'quads', 'primary')
 ,('74f2de2b-e0aa-2ffd-c3be-bd0cc5cb0bf8', 'quads', 'primary')
 ,('37c3e6d6-cc24-e932-2258-c44971ed301a', 'quads', 'primary')
 ,('e8571544-d370-2b20-eb00-1fef9eeba996', 'quads', 'primary')
 ,('e8571544-d370-2b20-eb00-1fef9eeba996', 'glutes', 'primary')
 ,('915c03e0-8d2c-1269-ad52-7f44cd7a8070', 'quads', 'primary')
 ,('915c03e0-8d2c-1269-ad52-7f44cd7a8070', 'glutes', 'primary')
 ,('6b06e2e8-68dd-c1db-8001-05c8395130ef', 'hamstrings', 'primary')
 ,('6b06e2e8-68dd-c1db-8001-05c8395130ef', 'glutes', 'primary')
 ,('c698362d-13c5-c3eb-e766-e4dae970e3e8', 'hamstrings', 'primary')
 ,('761a4b61-ba7c-de0f-6a57-06e46df25b01', 'glutes', 'primary')
 ,('5e46c87f-8ae0-86d2-f858-6a259bb59abf', 'calves', 'primary')
 ,('ded5de77-50b2-d373-d6de-cf27146e9691', 'quads', 'secondary')
 ,('a8943372-cb78-981c-84a8-27f08685dc88', 'quads', 'secondary')
 ,('a8943372-cb78-981c-84a8-27f08685dc88', 'glutes', 'secondary')
 ,('5e87d435-0bd6-314b-f52a-1eecbb981e50', 'quads', 'secondary')
 ,('22028389-b3f8-95b2-618a-4faeac7e37da', 'hamstrings', 'secondary')
 ,('22028389-b3f8-95b2-618a-4faeac7e37da', 'erectors', 'secondary')
 ,('b36c7bc2-7730-0611-9f35-c4af4c58c389', 'glutes', 'secondary')
 ,('a428c23b-972b-107f-f8af-1bf868753bd7', 'glutes', 'secondary')
 ,('4aa204fd-ee5c-30af-105a-428569405f3c', 'calves', 'secondary')
 ,('0ee1fa6e-69e7-3e1b-71e1-40e86fc925bd', 'calves', 'secondary')
 ,('4acc71c0-f806-9aea-401a-66720df3ceda', 'adductors', 'secondary')
 ,('30bc3acd-b856-e299-952b-bdc99dc990dc', 'adductors', 'secondary')
on conflict (exercise_id, muscle_id) do nothing;

insert into public.exercise_rank_standards (
  version_id, exercise_id, measurement_type, load_mode, comparability, source, confidence,
  r10_male_70, r10_male_100, r10_female_60, r10_female_90) values
  ('rhythmo-v1', 'c12df4f7-a460-1d07-84ac-d8ce35b0fd9c', '1rm', 'total', 'high', 'strength_level', 'high', 119, 160, 76, 97)
 ,('rhythmo-v1', 'a9369ee8-12cc-f4fe-d536-13ccfd4afd1b', '1rm', 'per_dumbbell', 'high', 'strength_level', 'high', 56, 73, 35, 43)
 ,('rhythmo-v1', '79e55bab-97e1-dc30-d8a7-97ecc20c1f27', '1rm', 'total', 'high', 'strength_level', 'high', 136, 179, 88, 111)
 ,('rhythmo-v1', 'bc657db8-3d75-dd06-e5f9-5187d87959c0', '1rm', 'per_dumbbell', 'high', 'strength_level', 'high', 62, 79, 38, 46)
 ,('rhythmo-v1', '8d16ccc5-52d5-0a3c-16cc-f250ea37e584', '1rm', 'total', 'high', 'strength_level', 'high', 146, 197, 95, 120)
 ,('rhythmo-v1', '52eacc40-9cf8-3ce7-10a2-ac8655e2cccf', '1rm', 'per_dumbbell', 'high', 'strength_level', 'high', 63, 79, 42, 53)
 ,('rhythmo-v1', 'b737af0a-5e89-de30-fe35-f1a1a9a08b91', '1rm', 'total', 'medium', 'strength_level', 'medium', 147, 181, 75, 85)
 ,('rhythmo-v1', 'd10f7b90-bbb8-a91e-d8f4-5ed8010425a9', '10rm', 'total', 'low', 'strength_level', 'medium', 139, 175, 74, 91)
 ,('rhythmo-v1', 'e676fe2a-823d-6e41-bad6-9270799ea302', '10rm', 'per_dumbbell', 'high', 'strength_level', 'high', 43, 54, 23, 27)
 ,('rhythmo-v1', 'e24f35c4-d576-ad68-f358-e5d835582413', '10rm', 'total', 'medium', 'strength_level', 'medium', 77, 98, 43, 51)
 ,('rhythmo-v1', 'c288a105-23fd-abc8-161c-07c5b7442a6a', '1rm', 'added_load', 'high', 'rhythmo_v1_manual', 'medium', 50, 70, 35, 50)
 ,('rhythmo-v1', '85633508-9d8f-81da-83ad-48cb6932ab1b', '5rm', 'total', 'medium', 'strength_level', 'high', 123, 150, 76, 88)
 ,('rhythmo-v1', 'b3316450-c4b0-92b4-8075-24fe9eeb8196', '5rm', 'total', 'medium', 'strength_level', 'high', 123, 150, 76, 88)
 ,('rhythmo-v1', '84a4c47c-8c65-2273-26cb-0516e0e0460c', '5rm', 'total', 'high', 'strength_level', 'high', 127, 166, 78, 91)
 ,('rhythmo-v1', '33cc9252-6aa4-c063-2ba5-7b4e3c12860d', '5rm', 'per_dumbbell', 'high', 'strength_level', 'high', 69, 89, 38, 45)
 ,('rhythmo-v1', '4bee2133-934e-5fc5-8759-2c16835f3ab2', '5rm', 'total', 'medium', 'strength_level', 'high', 128, 160, 79, 94)
 ,('rhythmo-v1', '81a892fe-b00b-9bc3-5e8e-21d0624b88a1', '5rm', 'total', 'medium', 'strength_level', 'medium', 104, 127, 64, 72)
 ,('rhythmo-v1', 'a53f6ae0-b0c0-fdbe-a541-fbc4b9909bb8', '1rm', 'total', 'medium', 'strength_level', 'medium', 91, 116, 51, 60)
 ,('rhythmo-v1', '7b310e3b-b7ad-933f-3ea0-c0a38d447cdd', '5rm', 'total', 'high', 'strength_level', 'high', 205, 274, 134, 170)
 ,('rhythmo-v1', 'f298e620-3c61-8dac-ffe3-8210a0dc4dcb', '5rm', 'per_dumbbell', 'high', 'strength_level', 'high', 77, 97, 38, 45)
 ,('rhythmo-v1', '2873fc2c-082c-034a-f508-9e2addde770a', '5rm', 'total', 'low', 'rhythmo_estimated', 'low', 249, 311, 162, 202)
 ,('rhythmo-v1', 'a9400ccf-a128-7df1-48d1-e58c325c6382', '1rm', 'total', 'high', 'strength_level', 'high', 216, 279, 149, 180)
 ,('rhythmo-v1', '28d0764e-89de-72c5-b64c-d8205ff35121', '5rm', 'total', 'medium', 'strength_level', 'medium', 185, 219, 126, 143)
 ,('rhythmo-v1', '595502e9-f837-2fbc-34cc-b17da659aac9', '5rm', 'total', 'high', 'strength_level', 'high', 75, 100, 46, 57)
 ,('rhythmo-v1', '354fde39-bbb2-9af3-01e4-a5e3f2c24514', '5rm', 'per_dumbbell', 'high', 'strength_level', 'high', 41, 54, 23, 27)
 ,('rhythmo-v1', 'ab52539f-7e75-7aa6-9e7b-7fa81b84aa8c', '5rm', 'total', 'low', 'strength_level', 'low', 112, 146, 57, 67)
 ,('rhythmo-v1', '772b896e-dd26-d4b5-07f9-25715dcb300e', '10rm', 'per_dumbbell', 'high', 'rhythmo_v1_manual', 'medium', 25, 30, 16, 19)
 ,('rhythmo-v1', 'bb42b14b-ebf5-6e8f-d494-ccdfb39a1026', '10rm', 'total', 'medium', 'strength_level', 'medium', 28, 33, 19, 21)
 ,('rhythmo-v1', 'a58cc0d9-ef75-70f7-326a-83a7a3674ecb', '10rm', 'total', 'low', 'strength_level', 'medium', 75, 98, 41, 48)
 ,('rhythmo-v1', '370883a2-713a-e1cc-5efe-cd96d3a981bb', '10rm', 'total', 'medium', 'strength_level', 'medium', 82, 104, 42, 50)
 ,('rhythmo-v1', 'f55ca830-e71f-0ea3-3f07-daf078df85dd', '5rm', 'total', 'medium', 'strength_level', 'medium', 75, 92, 56, 66)
 ,('rhythmo-v1', '3ca79fb0-d8b1-3ca5-ef1a-d712cf67e49a', '5rm', 'total', 'high', 'strength_level', 'high', 63, 79, 40, 49)
 ,('rhythmo-v1', 'b24cffe6-a60f-14f4-c411-ed7794e1f903', '5rm', 'per_dumbbell', 'high', 'strength_level', 'high', 33, 41, 21, 25)
 ,('rhythmo-v1', 'a764ce72-1cd5-361a-1f5c-e80c692faafe', '5rm', 'total', 'high', 'strength_level', 'high', 62, 76, 39, 49)
 ,('rhythmo-v1', 'e91413e8-9a9b-2618-ba8a-bfcd75d84692', '5rm', 'total', 'medium', 'rhythmo_v1_manual', 'medium', 65, 95, 50, 80)
 ,('rhythmo-v1', 'a90dafea-187a-8496-9ddc-94293f2a3e7d', '5rm', 'total', 'medium', 'rhythmo_v1_manual', 'medium', 60, 90, 45, 75)
 ,('rhythmo-v1', '7b1be08f-2a43-05e9-09fd-07aa830026d9', '5rm', 'total', 'medium', 'rhythmo_v1_manual', 'medium', 55, 85, 40, 70)
 ,('rhythmo-v1', '7cfc3cd3-3254-2560-9227-fac64a99cd0b', '10rm', 'total', 'medium', 'strength_level', 'high', 96, 107, 71, 82)
 ,('rhythmo-v1', '93eb818c-29c1-0e90-cbb9-87eed79fc85f', '10rm', 'total', 'low', 'strength_level', 'medium', 109, 133, 67, 76)
 ,('rhythmo-v1', '2d2a96d6-1cf1-8fdc-8b11-5c8cd735fc87', '1rm', 'added_load', 'medium', 'rhythmo_v1_manual', 'medium', 80, 110, 60, 90)
 ,('rhythmo-v1', '4e524f1a-acf2-6294-c6cc-6124d809f08d', '1rm', 'added_load', 'low', 'rhythmo_v1_manual', 'low', 90, 120, 70, 100)
 ,('rhythmo-v1', '63a7a51f-59e4-f0a4-d185-36b3a7c3ffc5', '1rm', 'total', 'high', 'strength_level', 'high', 185, 244, 129, 158)
 ,('rhythmo-v1', '840a6fe8-8555-14ea-57bb-b9be7ef7f94f', '1rm', 'total', 'medium', 'strength_level', 'high', 257, 321, 190, 221)
 ,('rhythmo-v1', '74f2de2b-e0aa-2ffd-c3be-bd0cc5cb0bf8', '1rm', 'total', 'medium', 'strength_level', 'high', 359, 460, 277, 337)
 ,('rhythmo-v1', '37c3e6d6-cc24-e932-2258-c44971ed301a', '10rm', 'total', 'medium', 'strength_level', 'high', 127, 151, 86, 98)
 ,('rhythmo-v1', 'e8571544-d370-2b20-eb00-1fef9eeba996', '1rm', 'total', 'high', 'strength_level', 'high', 128, 170, 77, 90)
 ,('rhythmo-v1', '915c03e0-8d2c-1269-ad52-7f44cd7a8070', '1rm', 'total', 'high', 'strength_level', 'high', 110, 139, 71, 75)
 ,('rhythmo-v1', '6b06e2e8-68dd-c1db-8001-05c8395130ef', '1rm', 'total', 'high', 'strength_level', 'high', 184, 238, 116, 132)
 ,('rhythmo-v1', 'c698362d-13c5-c3eb-e766-e4dae970e3e8', '10rm', 'total', 'medium', 'strength_level', 'high', 80, 100, 50, 59)
 ,('rhythmo-v1', '761a4b61-ba7c-de0f-6a57-06e46df25b01', '1rm', 'total', 'high', 'strength_level', 'high', 257, 335, 199, 226)
 ,('rhythmo-v1', '5e46c87f-8ae0-86d2-f858-6a259bb59abf', '10rm', 'total', 'medium', 'strength_level', 'high', 131, 160, 110, 128)
on conflict (version_id, exercise_id) do nothing;

