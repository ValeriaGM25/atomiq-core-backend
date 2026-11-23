# Atomiq

Atomiq es un laboratorio inmersivo de dinámica molecular y reacciones químicas en realidad aumentada. Este repositorio contiene el backend ASP.NET Core que dará soporte a la experiencia Unity para Android y ARCore.

## Tecnologías

- .NET 10 y ASP.NET Core Minimal APIs
- C# con nullable reference types y analyzers habilitados
- Clean Architecture
- xUnit y `WebApplicationFactory`
- Gateway configurable para OpenAI, MongoDB y puntos de extensión de SignalR

SignalR y el motor químico de Unity siguen fuera del alcance de esta fase.

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

El endpoint inicial es `GET /health`. Los contratos de OpenAI y SignalR no se crean como placeholders exitosos.

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

El tutor también está disponible en `/hubs/aitutor` mediante SignalR. El contrato y los requisitos del cliente Unity están documentados en [docs/unity-signalr-integration.md](docs/unity-signalr-integration.md). REST permanece disponible como fallback.

## Tutor pedagógico

`POST /api/reactions/explain` requiere el mismo token del dispositivo y acepta el payload de simulación Unity. Devuelve siempre una explicación breve en español, junto con `source`, que puede ser `OpenAIOnline`, `MemoryCache` u `OfflineFallback`.

OpenAI está desactivado por defecto. Cuando está desactivado, no se realiza ninguna llamada externa y se utiliza el fallback local. El backend no recalcula la química ni transforma los datos de Unity en instrucciones para el modelo.

La caché en memoria usa una clave derivada de todos los campos científicos, resultado, idioma, versión del prompt, modelo y política de activación. Su TTL predeterminado es de 24 horas y el tamaño está limitado.

## Configuración local

La configuración de ejemplo se encuentra en `src/Atomiq.Api/appsettings.json`. Los valores sensibles deben proporcionarse mediante User Secrets o variables de entorno, nunca mediante Git.

MongoDB utiliza las colecciones `profiles` y `reaction_logs`. La cadena de conexión no se versiona:

```bash
dotnet user-secrets set "Mongo:ConnectionString" "mongodb://localhost:27017" --project src/Atomiq.Api
dotnet user-secrets set "Mongo:DatabaseName" "atomiq_local" --project src/Atomiq.Api
```

La creación de índices se expone mediante `MongoIndexInitializer` y no se ejecuta automáticamente al iniciar la API.

### Pruebas de integración MongoDB

Las pruebas reales están en `Atomiq.Infrastructure.IntegrationTests` y requieren una instancia MongoDB local escuchando en loopback. No instalan Docker, no usan Atlas y generan una base temporal única por ejecución:

```bash
dotnet test tests/Atomiq.Infrastructure.IntegrationTests/Atomiq.Infrastructure.IntegrationTests.csproj
```

Por defecto usan `mongodb://127.0.0.1:27017`. Se puede proporcionar otra conexión local mediante `ATOMIQ_MONGO_TEST_CONNECTION`; la suite rechaza hosts no locales. Solo se limpian los documentos creados en la base temporal de la propia ejecución. Si MongoDB no está disponible, estas pruebas fallan explícitamente y no deben reportarse como aprobadas.

Los tokens del stand también deben configurarse fuera del repositorio. Nunca deben incrustarse en el APK ni en `appsettings.json`.

```bash
dotnet user-secrets init --project src/Atomiq.Api
dotnet user-secrets set "OpenAI:ApiKey" "<API_KEY_PROVISTA_POR_OPERADOR>" --project src/Atomiq.Api
dotnet user-secrets set "OpenAI:Enabled" "true" --project src/Atomiq.Api
dotnet user-secrets set "OpenAI:Model" "gpt-4o" --project src/Atomiq.Api
```

`gpt-4o` es el modelo predeterminado y puede cambiarse mediante `OpenAI:Model`. `OpenAI:BaseUrl`, timeout, límite de palabras, TTL y versión del prompt también son configurables. Las pruebas usan HTTP simulado y no ejecutan llamadas reales de pago.

La validación live no se ejecuta con `dotnet test` ni forma parte del arranque de la API. Requiere autorización explícita del operador, una clave vigente almacenada solo en User Secrets del proyecto de herramienta y permite una única solicitud:

```bash
dotnet user-secrets set "OpenAI:ApiKey" "<API_KEY_PROVISTA_POR_OPERADOR>" --project tools/Atomiq.OpenAi.LiveValidation
dotnet user-secrets set "OpenAI:Enabled" "true" --project tools/Atomiq.OpenAi.LiveValidation
dotnet user-secrets set "OpenAI:Model" "gpt-4o" --project tools/Atomiq.OpenAi.LiveValidation
dotnet user-secrets set "OpenAI:TimeoutMilliseconds" "2500" --project tools/Atomiq.OpenAi.LiveValidation
```

No se debe pegar una clave en Git, Unity, APK, logs o argumentos compartidos. La prueba manual se autoriza de forma consciente y temporal:

```powershell
$env:ATOMIQ_ALLOW_OPENAI_LIVE_TEST = "I_UNDERSTAND_ONE_PAID_OPENAI_CALL"
dotnet run --project tools/Atomiq.OpenAi.LiveValidation
Remove-Item Env:ATOMIQ_ALLOW_OPENAI_LIVE_TEST
```

La herramienta imprime únicamente `Source`, latencia, conteo de palabras y explicación; nunca imprime la clave ni headers. `OpenAIOnline` es el único resultado válido de la prueba live. `OfflineFallback`, una respuesta inválida o una clave rechazada terminan con error y no se consideran integración real aprobada. Las llamadas reales pueden generar costes.

## Contratos Unity

Los contratos previstos para el cliente Unity están documentados en [docs/api-contracts.md](docs/api-contracts.md). Deben verificarse con el cliente real antes de implementarse.

## Contribución

Crear ramas con nombres descriptivos desde `main`, mantener cambios pequeños y ejecutar restore, build y test antes de abrir un pull request. No incluir secretos, archivos locales ni conexiones reales.
