-- Permet au client de peupler le catalogue intégré (is_builtin, created_by null).
-- Sans cette policy, exercises_insert_own bloque l’insert au premier login.

drop policy if exists exercises_insert_builtin on public.exercises;

create policy exercises_insert_builtin on public.exercises
  for insert
  to authenticated
  with check (is_builtin = true and created_by is null);
