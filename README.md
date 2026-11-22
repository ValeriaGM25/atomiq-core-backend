# Atomiq

Atomiq es un laboratorio inmersivo de dinámica molecular y reacciones químicas en realidad aumentada. Este repositorio contiene el backend ASP.NET Core que dará soporte a la experiencia Unity para Android y ARCore.

## Tecnologías

- .NET 10 y ASP.NET Core Minimal APIs
- C# con nullable reference types y analyzers habilitados
- Clean Architecture
- xUnit y `WebApplicationFactory`
- Puntos de extensión preparados para SignalR, MongoDB y Gemini

Gemini, SignalR y el motor químico de Unity siguen fuera del alcance de esta fase.

## Requisitos

- Windows, Linux o macOS
- SDK .NET 10.0.401 o compatible con `global.json`
- Git

## Arquitectura

- `Atomiq.Domain`: modelo y reglas de negocio independientes.
- `Atomiq.Application`: casos de uso y puertos de aplicación; depende de Domain.
- `Atomiq.Infrastructure`: adaptadores técnicos y configuración; depende de Application y Domain.
- `Atomiq.Api`: composición HTTP, middleware, DI y endpoints; depende de Application e Infrastructure.

El dominio contiene `UserProfile` y `ReactionLog`. Application expone casos de uso para perfiles, XP e historial. MongoDB se mantiene en Infrastructure mediante documentos BSON separados de las entidades.

La validación científica y la simulación química corresponden al cliente Unity Android/ARCore, no a este backend.

## Estructura

```text
src/       Domain, Application, Infrastructure y Api
tests/     pruebas por capa
docs/      arquitectura, contratos y desarrollo
```

## Comandos

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet run --project src/Atomiq.Api
```

El endpoint inicial es `GET /health`. Los contratos de Gemini y SignalR no se crean como placeholders exitosos.

## API de perfil y reacciones

Los endpoints de perfil y XP requieren el header `X-Atomiq-Device-Token`. El servidor obtiene el `deviceId` autorizado desde el token y rechaza accesos cruzados entre dispositivos.

```http
GET /api/profile/device-1
X-Atomiq-Device-Token: <token-provisionado>
```

```json
{
  "deviceId": "device-1",
  "currentLevel": 0,
  "totalXp": 25,
  "discoveredReactions": []
}
```

XP se envía con un `EventId` idempotente:

```http
POST /api/profile/xp
Content-Type: application/json
X-Atomiq-Device-Token: <token-provisionado>
```

```json
{
  "deviceId": "device-1",
  "eventId": "reaction-event-001",
  "xpGained": 25
}
```

El historial se consulta con `GET /api/reactions/history?page=1&pageSize=20`. El dispositivo se obtiene de la identidad autenticada y no de un parámetro arbitrario.

## Configuración local

La configuración de ejemplo se encuentra en `src/Atomiq.Api/appsettings.json`. Los valores sensibles deben proporcionarse mediante User Secrets o variables de entorno, nunca mediante Git.

MongoDB utiliza las colecciones `profiles` y `reaction_logs`. La cadena de conexión no se versiona:

```bash
dotnet user-secrets set "Mongo:ConnectionString" "mongodb://localhost:27017" --project src/Atomiq.Api
dotnet user-secrets set "Mongo:DatabaseName" "atomiq_local" --project src/Atomiq.Api
```

La creación de índices se expone mediante `MongoIndexInitializer` y no se ejecuta automáticamente al iniciar la API.

Los tokens del stand también deben configurarse fuera del repositorio. Nunca deben incrustarse en el APK ni en `appsettings.json`.

```bash
dotnet user-secrets init --project src/Atomiq.Api
dotnet user-secrets set "Gemini:ApiKey" "valor-local" --project src/Atomiq.Api
```

## Contratos Unity

Los contratos previstos para el cliente Unity están documentados en [docs/api-contracts.md](docs/api-contracts.md). Deben verificarse con el cliente real antes de implementarse.

## Contribución

Crear ramas con nombres descriptivos desde `main`, mantener cambios pequeños y ejecutar restore, build y test antes de abrir un pull request. No incluir secretos, archivos locales ni conexiones reales.
