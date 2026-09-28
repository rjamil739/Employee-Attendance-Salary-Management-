# User Creator

Run from the repository root. If `--password` is omitted, the tool asks for it without displaying it.

## Administrator

```powershell
dotnet run --project ".\tools\UserCreator\UserCreator.csproj" -- --username admin --display-name "System Administrator" --role ADMIN
```

## HR Manager

All branches:

```powershell
dotnet run --project ".\tools\UserCreator\UserCreator.csproj" -- --username hrmanager --display-name "HR Manager" --role HR_MANAGER --all-branches
```

One branch (the branch must already exist):

```powershell
dotnet run --project ".\tools\UserCreator\UserCreator.csproj" -- --username hrmanager --display-name "HR Manager" --role HR_MANAGER --branch-code HO
```
