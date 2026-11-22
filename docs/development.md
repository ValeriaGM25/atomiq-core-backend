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

## Autenticación local

Configurar tokens únicamente mediante User Secrets o variables de entorno. El formato de configuración usa el identificador de dispositivo como clave:

```bash
dotnet user-secrets set "Authentication:DeviceTokens:device-1" "token-local-no-versionado" --project src/Atomiq.Api
```

El cliente debe enviar `X-Atomiq-Device-Token`. No se habilita un bypass de autenticación en Development. Para las pruebas HTTP se utilizan tokens efímeros y repositorios en memoria exclusivos del proceso de pruebas.

La API usa HTTPS fuera de Development y Testing. El rate limiting predeterminado es de 60 solicitudes por dispositivo durante 60 segundos; se puede ajustar mediante `RateLimiting:PermitLimit` y `RateLimiting:WindowSeconds`.

Los errores de validación, autenticación, autorización y rate limiting se expresan con estados HTTP diferenciados. Las excepciones no devuelven trazas ni secretos.

## Gemini y modo sin conexión

Gemini permanece desactivado con `Gemini:Enabled=false`. Para activar una cuenta local, proporcionar la clave mediante User Secrets o una variable de entorno equivalente:

```bash
dotnet user-secrets set "Gemini:ApiKey" "token-local-no-versionado" --project src/Atomiq.Api
dotnet user-secrets set "Gemini:Enabled" "true" --project src/Atomiq.Api
```

La configuración incluye `BaseUrl`, `Model`, `TimeoutMilliseconds`, `MaxOutputWords`, `CacheTtlHours`, `CacheSizeLimit`, `Language` y `PromptVersion`. El candidato `gemini-3.5-flash-lite` no se considera garantizado: debe comprobarse su disponibilidad en la cuenta antes de activar llamadas.

Sin API key o con Gemini desactivado, `POST /api/reactions/explain` usa explicaciones locales para Na/Cl, Na/H2O y un mensaje genérico para resultados no confirmados. No se implementó un fallback específico para H2O porque su reto aún debe confirmarse con Unity.

Las pruebas usan `HttpMessageHandler` simulado y no realizan llamadas reales a Gemini. La experiencia AR local no depende de la disponibilidad del backend o del proveedor.

## SignalR

El Hub se expone en `/hubs/aitutor` y requiere `X-Atomiq-Device-Token`. El cliente SignalR debe gestionar la negociación, handshake JSON, framing `0x1E`, pings y reconexión; no se deben enviar mensajes WebSocket manuales desde la aplicación.

Las pruebas incluyen cliente SignalR con Long Polling y una prueba de handshake/invocación WebSocket contra `TestServer`. No sustituyen una prueba con Unity Android/ARCore. Si el cliente Unity no permite headers personalizados durante la negociación o WebSocket, debe confirmarse una alternativa basada en el mecanismo oficial de access token antes de cambiar el contrato.

## Pruebas

Las pruebas unitarias, de casos de uso y de serialización BSON no requieren MongoDB. La integración real con MongoDB queda pendiente de un contenedor de desarrollo; Docker no está disponible en el entorno actual, por lo que no se ejecutan pruebas contra Atlas ni producción.

## Ramas

`main` contiene la línea principal. Las contribuciones deben realizarse en ramas descriptivas y revisarse mediante pull request. No se deben hacer pushes forzados ni incluir credenciales.
