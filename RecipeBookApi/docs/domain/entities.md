# Опис доменних сутностей (Entities)

## 1. Recipe (Рецепт)
* `Id` (Guid) — Унікальний ідентифікатор
* `Title` (string) — Назва рецепта
* `Description` (string) — Короткий опис
* `Instructions` (string) — Покроковий процес приготування
* `CookingTimeMinutes` (int) — Час приготування у хвилинах
* `AuthorId` (Guid) — Ідентифікатор автора
* `Ingredients` (List<Ingredient>) — Список інгредієнтів

## 2. Ingredient (Інгредієнт)
* `Id` (Guid) — Унікальний ідентифікатор
* `Name` (string) — Назва продукту
* `Amount` (double) — Кількість
* `Unit` (string) — Одиниця виміру (грам, мл, шт)

## 3. User (Користувач)
* `Id` (Guid) — Унікальний ідентифікатор
* `Username` (string) — Логін / ім'я користувача
* `Email` (string) — Електронна пошта