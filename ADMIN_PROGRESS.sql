-- Просмотр уровня, опыта, очков характеристик и навыков игрока Maxim.
SELECT
    id,
    user_name,
    level,
    current_experience,
    experience_to_next_level,
    free_attribute_points,
    strength,
    endurance,
    agility,
    perception,
    intelligence,
    survival,
    pistols_experience,
    submachine_guns_experience,
    assault_rifles_experience,
    shotguns_experience,
    sniper_rifles_experience,
    machine_guns_experience,
    throwables_experience,
    medicine_experience,
    progress_updated_at_utc
FROM players
WHERE normalized_user_name = 'MAXIM';

-- ПРИМЕР: установить 5-й уровень, 40/300 XP и 12 свободных очков.
-- Для формулы игры порог следующего уровня равен: 100 + (level - 1) * 50.
-- UPDATE players
-- SET
--     level = 5,
--     current_experience = 40,
--     experience_to_next_level = 300,
--     free_attribute_points = 12,
--     progress_updated_at_utc = CURRENT_TIMESTAMP
-- WHERE normalized_user_name = 'MAXIM';

-- ПРИМЕР: изменить распределённые характеристики.
-- UPDATE players
-- SET
--     strength = 12,
--     endurance = 11,
--     agility = 10,
--     perception = 10,
--     intelligence = 10,
--     survival = 10,
--     free_attribute_points = 2,
--     progress_updated_at_utc = CURRENT_TIMESTAMP
-- WHERE normalized_user_name = 'MAXIM';

-- ПРИМЕР: изменить опыт владения штурмовыми винтовками и медицины.
-- Допустимый диапазон каждого навыка: от 0 до 15000.
-- UPDATE players
-- SET
--     assault_rifles_experience = 2500,
--     medicine_experience = 750,
--     progress_updated_at_utc = CURRENT_TIMESTAMP
-- WHERE normalized_user_name = 'MAXIM';
