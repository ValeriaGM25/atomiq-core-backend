# Desarrollo

## Flujo local

1. Restaurar dependencias con `dotnet restore`.
2. Compilar con `dotnet build`.
3. Ejecutar pruebas con `dotnet test`.
4. Revisar `git diff --check` y confirmar que no hay secretos.

La API se ejecuta con `dotnet run --project src/Atomiq.Api`. La configuración sensible debe utilizar User Secrets o variables de entorno.

## Ramas

`main` contiene la línea principal. Las contribuciones deben realizarse en ramas descriptivas y revisarse mediante pull request. No se deben hacer pushes forzados ni incluir credenciales.
