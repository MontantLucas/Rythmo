-- L'app n'envoie pas l'id. Postgres le génère à l'insertion.

alter table public.weekly_challenge_assignments
  alter column id set default gen_random_uuid();

alter table public.cardio_activities
  alter column id set default gen_random_uuid();
