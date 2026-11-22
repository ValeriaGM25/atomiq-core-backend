# Desarrollo

## Flujo local

1. Restaurar dependencias con `dotnet restore`.
2. Compilar con `dotnet build`.
3. Ejecutar pruebas con `dotnet test`.
4. Revisar `git diff --check` y confirmar que no hay secretos.

La API se ejecuta con `dotnet run --project src/Atomiq.Api`. La configuración sensible debe utilizar User Secrets o variables de entorno.

## MongoDB local

La Fase 2 no crea recursos externos automáticamente. Para una instancia local, configurar la conexión fuera de Git:

```bash
dotnet user-secrets set "Mongo:ConnectionString" "mongodb://localhost:27017" --project src/Atomiq.Api
dotnet user-secrets set "Mongo:DatabaseName" "atomiq_local" --project src/Atomiq.Api
```

No se ejecuta `MongoIndexInitializer` durante el arranque. Los índices deben crearse mediante un paso operativo explícito y solo sobre la base autorizada.

## Pruebas

Las pruebas unitarias, de casos de uso y de serialización BSON no requieren MongoDB. La integración real con MongoDB queda pendiente de un contenedor de desarrollo; Docker no está disponible en el entorno actual, por lo que no se ejecutan pruebas contra Atlas ni producción.

## Ramas

`main` contiene la línea principal. Las contribuciones deben realizarse en ramas descriptivas y revisarse mediante pull request. No se deben hacer pushes forzados ni incluir credenciales.
