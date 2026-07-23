-- Cold Verdge: просмотр и ручное редактирование инвентаря Maxim.
-- Player ID: 6be45e66-69a3-4b50-b223-3e6ed74c7150

-- 1. Текущее содержимое инвентаря.
SELECT
    p.id AS player_id,
    p.user_name,
    i.item_id,
    i.quantity,
    i.updated_at_utc
FROM players p
LEFT JOIN player_inventory_items i ON i.player_id = p.id
WHERE p.id = '6be45e66-69a3-4b50-b223-3e6ed74c7150'::uuid
ORDER BY i.item_id;

-- 2. Текущая надетая экипировка.
SELECT
    p.user_name,
    e.slot,
    e.item_id,
    e.updated_at_utc
FROM players p
LEFT JOIN player_equipment_items e ON e.player_id = p.id
WHERE p.id = '6be45e66-69a3-4b50-b223-3e6ed74c7150'::uuid
ORDER BY e.slot;

-- 3. Установить точное количество одного существующего предмета.
-- Пример: 5000 патронов.
UPDATE player_inventory_items
SET
    quantity = 5000,
    updated_at_utc = CURRENT_TIMESTAMP
WHERE player_id = '6be45e66-69a3-4b50-b223-3e6ed74c7150'::uuid
  AND item_id = 'ammo_762x51';

-- 4. Создать предмет, если строки ещё нет, либо установить точное количество.
-- Замените item_id и quantity на нужные значения.
INSERT INTO player_inventory_items
(
    id,
    player_id,
    item_id,
    quantity,
    created_at_utc,
    updated_at_utc
)
VALUES
(
    gen_random_uuid(),
    '6be45e66-69a3-4b50-b223-3e6ed74c7150'::uuid,
    'bandage',
    30,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
)
ON CONFLICT (player_id, item_id)
DO UPDATE SET
    quantity = EXCLUDED.quantity,
    updated_at_utc = CURRENT_TIMESTAMP;

-- 5. Восстановить полный админский комплект с точными количествами.
WITH desired_items(item_id, quantity) AS
(
    VALUES
        ('ak', 1),
        ('raider_helmet', 1),
        ('raider_vest', 1),
        ('raider_pants', 1),
        ('raider_boots', 1),
        ('field_backpack', 1),
        ('bandage', 30),
        ('ammo_762x51', 3000)
)
INSERT INTO player_inventory_items
(
    id,
    player_id,
    item_id,
    quantity,
    created_at_utc,
    updated_at_utc
)
SELECT
    gen_random_uuid(),
    '6be45e66-69a3-4b50-b223-3e6ed74c7150'::uuid,
    desired_items.item_id,
    desired_items.quantity,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
FROM desired_items
ON CONFLICT (player_id, item_id)
DO UPDATE SET
    quantity = EXCLUDED.quantity,
    updated_at_utc = CURRENT_TIMESTAMP;

-- 6. Надеть предмет. Допустимые пары:
-- head        -> raider_helmet
-- torso       -> raider_vest
-- legs        -> raider_pants
-- boots       -> raider_boots
-- main_weapon -> ak
-- backpack    -> field_backpack
INSERT INTO player_equipment_items
(
    player_id,
    slot,
    item_id,
    updated_at_utc
)
VALUES
(
    '6be45e66-69a3-4b50-b223-3e6ed74c7150'::uuid,
    'main_weapon',
    'ak',
    CURRENT_TIMESTAMP
)
ON CONFLICT (player_id, slot)
DO UPDATE SET
    item_id = EXCLUDED.item_id,
    updated_at_utc = CURRENT_TIMESTAMP;

-- 7. Снять предмет из конкретного слота.
DELETE FROM player_equipment_items
WHERE player_id = '6be45e66-69a3-4b50-b223-3e6ed74c7150'::uuid
  AND slot = 'main_weapon';

-- 8. Полностью удалить предмет.
-- Сначала убрать его из экипировки, затем из общего инвентаря.
BEGIN;
DELETE FROM player_equipment_items
WHERE player_id = '6be45e66-69a3-4b50-b223-3e6ed74c7150'::uuid
  AND item_id = 'bandage';
DELETE FROM player_inventory_items
WHERE player_id = '6be45e66-69a3-4b50-b223-3e6ed74c7150'::uuid
  AND item_id = 'bandage';
COMMIT;

-- Зарегистрированные item_id:
-- iron_ingot
-- copper_ingot
-- bandage
-- ammo_762x51
-- ak
-- raider_helmet
-- raider_vest
-- raider_pants
-- raider_boots
-- field_backpack
