# Практика 4.1 — непрерывная интеграция StudyFlow

По методичке нужно настроить сборку ASP.NET-проекта из основной ветки, восстановить пакеты NuGet, выполнить сборку и тесты, сохранить результаты и просмотреть журналы. Затем нужно закоммитить косметическую правку страницы и проверить, что конвейер запустился автоматически.

Для демонстрации создана учебная главная страница StudyFlow на ASP.NET Core 8 и Razor Pages. В GitHub Actions конвейер задан YAML-файлом и выполняется на GitHub-hosted `ubuntu-24.04`. Код и тесты находятся в основном репозитории StudyFlow. [Страница](../src/StudyFlow.Web/Pages/Index.cshtml) · [Тесты](../tests/StudyFlow.Web.Tests/HomePageTests.cs) · [Конвейер](../.github/workflows/studyflow-ci.yml)

| Параметр | Значение |
| --- | --- |
| Источник | Ветка `main`, коммит конкретного запуска |
| Переменные | `BUILD_CONFIGURATION=Release`, `WEB_PROJECT`, `TEST_PROJECT` |
| Триггеры | Ручной запуск, `push` в `main` и pull request в `main` при изменении приложения или тестов |
| Шаги | Получение исходников, .NET SDK 8, `restore`, `build`, `test`, `publish`, выгрузка файлов |
| Результаты | `studyflow-web` со сборкой, `studyflow-test-results` с отчётом TRX и таблица тестов в Summary |
| Хранение | Для этих артефактов 14 дней. В настройках репозитория указан срок хранения истории и логов 90 дней |

## Два проверенных запуска

| Запуск | Событие | Коммит | Итог |
| --- | --- | --- | --- |
| [№1](https://github.com/mirea-studyflow-bandukov-ilya/studyflow/actions/runs/43275095603) | `workflow_dispatch` | `656c750` | Success, 3 теста, 2 артефакта |
| [№2](https://github.com/mirea-studyflow-bandukov-ilya/studyflow/actions/runs/43275348853) | `push` | `0a6af56` | Success, 3 теста, 2 артефакта |

Коммит №2 меняет заголовок с «Учебные задачи» на «План учебных задач» в `Index.cshtml`. На вкладке Actions он появился автоматически, а Summary показывает `Trigger: push` и соответствующий SHA. [Правка заголовка](https://github.com/mirea-studyflow-bandukov-ilya/studyflow/commit/0a6af56cfd816d1f0ab7b6da67d2f3398e0dd64d) · [История запусков](https://github.com/mirea-studyflow-bandukov-ilya/studyflow/actions/workflows/studyflow-ci.yml)

Тесты проверяют ответ главной страницы `200` с русскоязычным HTML, наличие ссылки на документацию и ответ `404` для неизвестного маршрута. В обоих запусках прошли три теста из трёх.

Для защиты сначала открыть историю с двумя зелёными запусками. Затем в запуске №2 показать событие `push`, коммит, таблицу тестов и два артефакта. В задании **Build and test ASP.NET Core** можно раскрыть журнал любого шага. Изменения определения сборки видны в истории YAML-файла. [Сводка автоматического запуска](https://github.com/mirea-studyflow-bandukov-ilya/studyflow/actions/runs/43275348853) · [История YAML](https://github.com/mirea-studyflow-bandukov-ilya/studyflow/commits/main/.github/workflows/studyflow-ci.yml)

При наличии .NET SDK 8 приложение можно запустить командой `dotnet run --project src/StudyFlow.Web/StudyFlow.Web.csproj`. Сборка из артефакта запускается командой `dotnet StudyFlow.Web.dll` из распакованного каталога с ASP.NET Core Runtime 8.
