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

### Integración MongoDB

La suite `Atomiq.Infrastructure.IntegrationTests` ejecuta operaciones reales contra MongoDB. Requiere MongoDB local en `127.0.0.1`, `localhost` o `::1`; no se instala Docker ni MongoDB automáticamente:

```bash
dotnet test tests/Atomiq.Infrastructure.IntegrationTests/Atomiq.Infrastructure.IntegrationTests.csproj
```

La conexión predeterminada es `mongodb://127.0.0.1:27017`. Para una instancia local alternativa, usar `ATOMIQ_MONGO_TEST_CONNECTION`. La suite crea `atomiq_integration_<id>` por ejecución, verifica el ping antes de probar, crea los índices explícitamente y elimina únicamente sus documentos al finalizar. No se debe configurar una URI de Atlas, producción o un host remoto. La ausencia de MongoDB produce un fallo de integración, no un resultado PASS ni un fallback simulado.

## Autenticación local

Configurar tokens únicamente mediante User Secrets o variables de entorno. El formato de configuración usa el identificador de dispositivo como clave:

```bash
dotnet user-secrets set "Authentication:DeviceTokens:device-1" "token-local-no-versionado" --project src/Atomiq.Api
```

El cliente debe enviar `X-Atomiq-Device-Token`. No se habilita un bypass de autenticación en Development. Para las pruebas HTTP se utilizan tokens efímeros y repositorios en memoria exclusivos del proceso de pruebas.

La API usa HTTPS fuera de Development y Testing. El rate limiting predeterminado es de 60 solicitudes por dispositivo durante 60 segundos; se puede ajustar mediante `RateLimiting:PermitLimit` y `RateLimiting:WindowSeconds`.

Los errores de validación, autenticación, autorización y rate limiting se expresan con estados HTTP diferenciados. Las excepciones no devuelven trazas ni secretos.

## OpenAI y modo sin conexión

OpenAI permanece desactivado con `OpenAI:Enabled=false`. Para activar una cuenta local, proporcionar la clave mediante User Secrets o una variable de entorno equivalente. La clave debe haber sido revocada si se expuso anteriormente y nunca debe almacenarse en Git, Unity ni el APK:

```bash
dotnet user-secrets set "OpenAI:ApiKey" "<API_KEY_PROVISTA_POR_OPERADOR>" --project src/Atomiq.Api
dotnet user-secrets set "OpenAI:Enabled" "true" --project src/Atomiq.Api
dotnet user-secrets set "OpenAI:Model" "gpt-4o" --project src/Atomiq.Api
dotnet user-secrets set "OpenAI:TimeoutMilliseconds" "2500" --project src/Atomiq.Api
```

La variable de entorno equivalente para la clave es `OpenAI__ApiKey`; no debe aparecer en archivos versionados ni en el APK.

La configuración incluye `BaseUrl`, `Model`, `ApiKey`, `TimeoutMilliseconds`, `MaxOutputWords`, `CacheTtlHours`, `CacheSizeLimit`, `Language` y `PromptVersion`. El modelo predeterminado es `gpt-4o` y el cliente usa `POST /v1/responses` con `Authorization: Bearer`.

Sin API key o con OpenAI desactivado, `POST /api/reactions/explain` usa explicaciones locales para Na/Cl, Na/H2O y un mensaje genérico para resultados no confirmados. Los errores 401, 403, 429, 5xx, timeout, JSON inesperado y respuesta vacía seleccionan fallback; una cancelación explícita se propaga.

Las pruebas usan `HttpMessageHandler` simulado y no realizan llamadas reales a OpenAI. La experiencia AR local no depende de la disponibilidad del backend o del proveedor. Para desactivar el proveedor sin detener la experiencia AR, usar `dotnet user-secrets set "OpenAI:Enabled" "false"` o eliminar la clave; el servicio seleccionará `OfflineFallback`.

## Validación live controlada de OpenAI

La herramienta `tools/Atomiq.OpenAi.LiveValidation` está separada de la API y no se incluye en `dotnet test`. Está desactivada si no existe el opt-in exacto `ATOMIQ_ALLOW_OPENAI_LIVE_TEST=I_UNDERSTAND_ONE_PAID_OPENAI_CALL`. Antes de usarla, el operador debe obtener autorización explícita, confirmar que la clave nueva no es la clave revocada y guardarla únicamente en User Secrets de la herramienta:

```bash
dotnet user-secrets set "OpenAI:ApiKey" "<API_KEY_PROVISTA_POR_OPERADOR>" --project tools/Atomiq.OpenAi.LiveValidation
dotnet user-secrets set "OpenAI:Enabled" "true" --project tools/Atomiq.OpenAi.LiveValidation
dotnet user-secrets set "OpenAI:Model" "gpt-4o" --project tools/Atomiq.OpenAi.LiveValidation
dotnet user-secrets set "OpenAI:TimeoutMilliseconds" "2500" --project tools/Atomiq.OpenAi.LiveValidation
```

Con autorización, ejecutar como máximo una vez:

```powershell
$env:ATOMIQ_ALLOW_OPENAI_LIVE_TEST = "I_UNDERSTAND_ONE_PAID_OPENAI_CALL"
dotnet run --project tools/Atomiq.OpenAi.LiveValidation
Remove-Item Env:ATOMIQ_ALLOW_OPENAI_LIVE_TEST
```

La herramienta usa un payload Na/Cl, valida la respuesta de Responses API, limita la explicación a 25 palabras, diferencia `OpenAIOnline` de `OfflineFallback` e informa la latencia. No registra la clave, headers de autorización ni contenido de headers. Una clave inválida, timeout, 401/403, JSON inválido o fallback produce un resultado no aprobado. La prueba puede generar costes y no debe ejecutarse contra producción de la aplicación.

## SignalR

El Hub se expone en `/hubs/aitutor` y requiere `X-Atomiq-Device-Token`. El cliente SignalR debe gestionar la negociación, handshake JSON, framing `0x1E`, pings y reconexión; no se deben enviar mensajes WebSocket manuales desde la aplicación.

Las pruebas incluyen cliente SignalR con Long Polling y una prueba de handshake/invocación WebSocket contra `TestServer`. No sustituyen una prueba con Unity Android/ARCore. Si el cliente Unity no permite headers personalizados durante la negociación o WebSocket, debe confirmarse una alternativa basada en el mecanismo oficial de access token antes de cambiar el contrato.

## Pruebas

Las pruebas de `Atomiq.Domain.Tests`, `Atomiq.Application.Tests` y `Atomiq.Api.Tests` no requieren MongoDB real; las últimas usan `TestServer` y repositorios en memoria. Las pruebas de `Atomiq.Infrastructure.IntegrationTests` sí requieren la instancia local descrita arriba y cubren conexión, perfiles, índices únicos, concurrencia de XP, idempotencia, overflow, historial, paginación, serialización BSON, cancelación y errores de conexión.

## Ramas

`main` contiene la línea principal. Las contribuciones deben realizarse en ramas descriptivas y revisarse mediante pull request. No se deben hacer pushes forzados ni incluir credenciales.
