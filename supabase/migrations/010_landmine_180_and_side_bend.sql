-- Exercices intégrés : Landmine 180 (rotation 180 barre T) + flexion latérale au banc.
insert into public.exercises (id, name_fr, category, met_approx, is_builtin, created_by) values
  ('8680771f-adb7-d5d9-1f68-9c18b3631cb4', 'Landmine 180', 'Dos', 7.2, true, null)
 ,('0adc27a5-9ce3-9815-dad2-b54fa4783db1', 'Flexion latérale au banc', 'Abdominaux', 4.2, true, null)
on conflict (id) do nothing;
