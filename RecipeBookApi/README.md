# Recipe Book API
Практична робота 1. Базовий клієнт - бекенд - БД (SQLite).
Тема: Книга кулінарних рецептів.

---

## CI/CD та Delivery (Практична робота 7)

![CI/CD Pipeline](https://github.com/Deiv228/RecipeBookApi/actions/workflows/ci-cd.yml/badge.svg)

### Локальний запуск та тестування
- Відновлення залежностей: `dotnet restore RecipeBookApi.sln`
- Збірка проєкту: `dotnet build RecipeBookApi.sln --configuration Release`
- Запуск тестів: `dotnet test RecipeBookApi.sln --configuration Release`
- Запуск у контейнері: `docker compose up -d --build`

### Обраний підхід до Delivery
- **Реєстр контейнерів (GHCR):** `ghcr.io/deiv228/recipebookapi-api`
- **Автоматичне тегування:** семантичні теги `vX.Y.Z` (за Git-тегами `v*`), тег коміту `sha-<commit>` та `latest`.

### Виконані Style Points
1. **Семантичне тегування (vX.Y.Z + sha-<commit>):** реалізовано через `docker/metadata-action@v5`.
2. **Посилений контроль дозволів та concurrency:** мінімальні `permissions` (`contents: read`, `packages: write`) та `concurrency` з `cancel-in-progress: true`.
3. **Dependabot та Job Summary:** налаштовано `.github/dependabot.yml` (для NuGet і GitHub Actions) та генерацію Markdown-звітів у ``.
