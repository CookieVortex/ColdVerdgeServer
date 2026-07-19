# Отправка ColdVerdgeServer в приватный GitHub

## 1. Локальные секреты

Файл `.env` намеренно отсутствует в архиве. Создайте его локально:

```powershell
Copy-Item .env.example .env
notepad .env
```

Замените `CHANGE_ME...` на длинный локальный пароль. `.env` уже внесён в `.gitignore`.

Строка подключения ASP.NET Core должна оставаться в `dotnet user-secrets`, а не в файлах репозитория.

## 2. Проверка проекта

```powershell
cd C:\UnityProjects\ColdVerdgeServer
dotnet tool restore
dotnet restore
dotnet build
dotnet test
```

## 3. Инициализация Git

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Initialize-Git.ps1
```

Скрипт проверит типичные секреты, создаст ветку `main`, добавит файлы и создаст первый коммит.

## 4. GitHub

Создайте пустой репозиторий `ColdVerdgeServer` с видимостью **Private**. На GitHub не добавляйте README, `.gitignore` или License.

Затем:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Initialize-Git.ps1 -RemoteUrl "https://github.com/ВАШ_ЛОГИН/ColdVerdgeServer.git"
```

## 5. Последующие изменения

```powershell
git status
git add .
git commit -m "Краткое описание изменения"
git push
```

Перед каждым коммитом проверяйте `git status`. Файлы `.env`, `bin`, `obj` и локальные секреты не должны отображаться среди добавляемых файлов.
