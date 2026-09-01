# Расписание школы

Приложение для составления расписания уроков: кабинеты, учителя, классы,
учебный план, назначения учителей на предметы. Windows-десктоп (WPF).

## Как запустить (просто, без установки .NET)

1. Откройте страницу релизов: **https://github.com/levkastergit/sheldue/releases/latest**
2. Скачайте `SchoolSchedule-v1.0-win-x64.zip`
3. Распакуйте архив **целиком** в отдельную папку (все файлы из архива
   должны лежать рядом друг с другом — там не только `.exe`, но и
   несколько нужных ему `.dll`)
4. Запустите `SchoolSchedule.App.exe`

Требуется Windows 10/11 64-бит. Больше ничего ставить не нужно —
.NET и все зависимости уже внутри архива.

## Как запустить из исходного кода (для разработки)

Нужен установленный [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

Дважды кликните `run.bat` в корне репозитория — он сам соберёт и
запустит приложение. Либо вручную:

```
dotnet run --project src\SchoolSchedule.App\SchoolSchedule.App.csproj -c Release
```

## Структура решения

- `src/SchoolSchedule.Core` — доменные модели
- `src/SchoolSchedule.Data` — EF Core + SQLite, миграции
- `src/SchoolSchedule.Scheduling` — генерация расписания (Google.OrTools) — в разработке
- `src/SchoolSchedule.App` — WPF-приложение (MVVM)
- `src/SchoolSchedule.Tests` — интеграционные тесты (xUnit)

Данные хранятся локально в `%LocalAppData%\SchoolSchedule\school.db`
(SQLite), создаётся автоматически при первом запуске.

## Тесты

```
dotnet test src\SchoolSchedule.Tests
```
