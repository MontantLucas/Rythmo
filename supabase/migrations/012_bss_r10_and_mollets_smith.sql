-- BSS R10 manuels + Mollets smith classifiant (mollets primary, 5RM).

update public.exercise_rank_standards
set source = 'rhythmo_v1_manual',
    confidence = 'medium',
    r10_male_70 = 140,
    r10_male_100 = 160,
    r10_female_60 = 100,
    r10_female_90 = 120
where version_id = 'rhythmo-v1'
  and exercise_id = 'e8571544-d370-2b20-eb00-1fef9eeba996';

insert into public.exercise_muscle_roles (exercise_id, muscle_id, role)
values ('4aa204fd-ee5c-30af-105a-428569405f3c', 'calves', 'primary')
on conflict (exercise_id, muscle_id) do update set role = 'primary';

insert into public.exercise_rank_standards (
  version_id, exercise_id, measurement_type, load_mode, comparability, source, confidence,
  r10_male_70, r10_male_100, r10_female_60, r10_female_90)
values (
  'rhythmo-v1', '4aa204fd-ee5c-30af-105a-428569405f3c',
  '5rm', 'total', 'medium', 'rhythmo_v1_manual', 'medium',
  131, 160, 110, 128)
on conflict (version_id, exercise_id) do update set
  measurement_type = excluded.measurement_type,
  load_mode = excluded.load_mode,
  comparability = excluded.comparability,
  source = excluded.source,
  confidence = excluded.confidence,
  r10_male_70 = excluded.r10_male_70,
  r10_male_100 = excluded.r10_male_100,
  r10_female_60 = excluded.r10_female_60,
  r10_female_90 = excluded.r10_female_90;
