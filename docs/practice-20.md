# Практика 2.0 — подготовка тестовых данных

## Что требует методичка

Скачать исходники Azure DevOps Demo Generator, собрать и опубликовать утилиту на .NET 8 вместе с шаблонами, авторизоваться в Azure DevOps и создать проект по шаблону **Parts Unlimited**. Это подготовка окружения для следующих работ.

## Как выполнено на GitHub

Используется существующий проект StudyFlow. Генератор для GitHub написан на C#/.NET 8: [исходный код](../tools/DemoGenerator/Program.cs), [проект сборки](../tools/DemoGenerator/DemoGenerator.csproj), [шаблон](../tools/DemoGenerator/template.json), [запуск](../.github/workflows/seed-demo.yml).

Тестовые данные взяты из **Gen-PartsUnlimited**, версия шаблона 2.0, официального [репозитория Microsoft](https://github.com/microsoft/AzDevOpsDemoGenerator/tree/f5802c5769c21d29b1fdf48d0f0d8c0ee25be017/src/ADOGenerator/Templates/Gen-PartsUnlimited). Версия зафиксирована коммитом `f5802c5769c21d29b1fdf48d0f0d8c0ee25be017`; лицензия MIT сохранена внутри шаблона.

| Данные шаблона | Представление на GitHub |
|---|---|
| 16 Epic, 22 Feature, 18 Product Backlog Item | Issues с метками типа |
| 29 Task, 2 Bug | Issues с метками типа |
| 71 Test Case, 12 Test Suite, 1 Test Plan | Тестовые карточки с шагами на русском |
| 19 иерархических связей | Настоящие parent/sub-issue связи GitHub |
| Sprint 1–6 | Milestones с учебными датами; Sprint 1 начинается 26.10.2026, каждый длится 2 недели |
| Состояние, приоритет, effort, remaining work | Метки состояния и таблица полей в описании |
| Azure PAT | Временный встроенный `GITHUB_TOKEN` запуска, без личного PAT |
| Publish утилиты с шаблоном | Самодостаточная сборка Linux x64 и `template.json` в артефакте запуска |

Все **171 карточка** помечены [`demo:parts-unlimited`](https://github.com/mirea-studyflow-bandukov-ilya/studyflow/issues?q=is%3Aissue%20label%3Ademo%3Aparts-unlimited). Для собственных задач можно использовать фильтр `-label:demo:parts-unlimited`. Названия, описания и шаги тестов переведены на русский. Для сверки доступны [исходный шаблон](../tools/DemoGenerator/template.json) и [перевод](../tools/DemoGenerator/localization-ru.json). Историческое состояние Done означает состояние **демонстрационных данных**, а не реализованную функцию StudyFlow.

Это перенос назначения работы на GitHub, а не запуск Azure-генератора против GitHub. Azure service endpoints, старые Azure build/release definitions, расширения и облачная инфраструктура не создаются. Сам интернет-магазин Parts Unlimited не переносится в StudyFlow: здесь размещены исходники нашего генератора и тестовый backlog; приложение StudyFlow и его сборку будем делать в следующих работах. Тестовые карточки не заменяют специализированные Azure Test Plans, но содержат исходные данные для показа и дальнейших упражнений.

## Как повторить

1. Открыть **Actions → Подготовка учебных данных · практика 2.0 → Run workflow**.
2. `dry-run` проверяет шаблон без изменений. `apply` создаёт недостающие метки, спринты, карточки и связи, затем проверяет импорт.
3. Открыть итог запуска и скачать артефакт `demo-generator`: он содержит исполняемую сборку, шаблон и перевод. При импорте добавляется `seed-result.json` с соответствием исходных ID номерам карточек.

Повторный запуск находит карточки по устойчивому маркеру Source ID, не создаёт дубликаты и не сбрасывает изменения, сделанные в следующих практиках. Одновременные запуски поставлены в очередь. Данные личных аккаунтов, старые Azure назначения, подключения и вложения не импортируются.

## Что показать на сдаче

Успешную сборку и публикацию утилиты в Actions, тестовые Issues разных типов, Milestones Sprint 1–6 и parent/sub-issue связь. Объяснить: генератор экономит ручное заполнение окружения; шаблон задаёт начальные данные; повторяемость позволяет воспроизвести подготовку без дубликатов.

## Проверенный результат

- [Первый успешный запуск](https://github.com/mirea-studyflow-bandukov-ilya/studyflow/actions/runs/42893289442): сборка .NET 8, публикация утилиты с шаблоном, создание 171 карточки и 19 связей.
- [Спринты Sprint 1–6](https://github.com/mirea-studyflow-bandukov-ilya/studyflow/milestones).
- [Эпик «Корзина покупок» и четыре дочерние возможности](https://github.com/mirea-studyflow-bandukov-ilya/studyflow/issues/144).
- Отдельная проверка через публичный REST API подтвердила все исходные ID, названия, типы, состояния и назначения спринтов: 147 открытых и 24 закрытых демонстрационных карточки.
- [Повторный успешный запуск](https://github.com/mirea-studyflow-bandukov-ilya/studyflow/actions/runs/42894381654): проверены те же 171 карточка и 19 связей, новых карточек создано **0**.
