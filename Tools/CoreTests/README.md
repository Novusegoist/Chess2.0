# Тесты ядра

Проверяют правила (`Assets/Scripts/Core`) и бота без запуска Unity.

```
dotnet run --project Tools/CoreTests -c Release
```

Турнир уровней бота друг против друга (10 партий на пару, несколько минут):

```
dotnet run --project Tools/CoreTests -c Release -- arena 10
```
