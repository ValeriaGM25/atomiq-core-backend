# Plan de prueba Android E2E

## Alcance

Este plan cubre la validación controlada de un cliente Unity Android contra el
backend local de Atomiq. Las pruebas locales de REST, SignalR y MongoDB no
constituyen una prueba Android real. No se deben abrir puertos a Internet ni
desactivar autenticación, TLS o validación de certificados.

## Precondiciones

- Backend en `feat/unity-contract-compatibility`, con los contratos revisados.
- SDK .NET 10 instalado.
- MongoDB local disponible únicamente en `127.0.0.1:27017`.
- Base MongoDB temporal y exclusiva para la ejecución.
- OpenAI desactivado (`OpenAI:Enabled=false`); se espera `OfflineFallback`.
- Token efímero configurado solo en el proceso local y asociado a un `device_id`.
- API escuchando en loopback para las pruebas locales.
- Índices MongoDB inicializados explícitamente y verificados mediante
  `GET /health/readiness`.
- Para Android físico, dispositivo y equipo en una red privada autorizada.
- Cliente Unity con headers personalizados disponibles en negociación y WebSocket.

## Configuración del servidor

Las claves de configuración son:

- `Mongo:ConnectionString`: URI local de MongoDB.
- `Mongo:DatabaseName`: base temporal exclusiva.
- `Authentication:DeviceTokens:<deviceId>`: token efímero del dispositivo.
- `OpenAI:Enabled=false`.
- `ASPNETCORE_URLS=http://127.0.0.1:5119` para pruebas loopback.

Inicializar índices solo en la base temporal autorizada con:

```powershell
dotnet run --project src/Atomiq.Api -- --ensure-mongo-indexes
```

La API no crea índices automáticamente. Si faltan, `/health` puede seguir
devolviendo liveness, pero `/health/readiness` y las operaciones persistentes
dependientes devuelven `503`.

No almacenar estos valores en `appsettings*.json`, Git, Unity ni el APK. No
imprimir tokens ni claves en consola o logs. Retirar las variables temporales y
detener el proceso de la API al terminar.

## Contratos Unity

- Header de autenticación: `X-Atomiq-Device-Token`.
- Claim derivada: `device_id`; el cliente no puede sustituirla enviando un
  `deviceId` arbitrario.
- Hub: `/hubs/aitutor`.
- Invocación: `ExplainReaction(reactionJson, clientSystemPrompt)`.
- Evento: `ReceiveTutorExplanation`.
- Respuesta serializada: `explanation`, `source`, `historyDuplicate`.
- `eventId` debe ser estable al reintentar la misma reacción.
- `outcomeType`: `0=None`, `1=IonicCrystalBond`, `2=ExothermicAlkaliExplosion`,
  `3=ElectrostaticRepulsion`.
- El prompt enviado por Unity es dato no confiable y no controla las
  instrucciones internas del tutor.
- El mensaje de reacción está limitado a 64 KiB.

## Pruebas REST

1. `GET /health` sin autenticación: `200`.
2. Solicitud protegida sin token: `401`.
3. Token inválido: `401`.
4. Token válido: autorización correcta.
5. `GET /api/profile/{deviceId}` crea o devuelve el perfil del dispositivo.
6. `POST /api/profile/xp` con un `eventId` nuevo aplica XP.
7. Repetir el mismo `eventId`: no duplica XP y marca duplicado.
8. `GET /api/reactions/history`: devuelve solo el historial del dispositivo.
9. `POST /api/reactions/explain` con Na/Cl: `200`, explicación no vacía y
   `source=OfflineFallback` o `MemoryCache`.
10. Repetir la explicación con el mismo `eventId`: `historyDuplicate=true`.
11. Repetir con otro `eventId`: crea una nueva entrada.
12. Probar `outcomeType` 1, 2 y 3 y verificar que se conserva la semántica y el
    valor Unity en el historial.
13. Confirmar que el mismo `eventId` aparece una sola vez en MongoDB.

## Pruebas SignalR

1. Conectar autenticado a `/hubs/aitutor` mediante el cliente oficial SignalR.
2. Confirmar handshake JSON y transporte seleccionado.
3. Invocar `ExplainReaction` con el payload Unity.
4. Recibir `ReceiveTutorExplanation` y validar los tres campos de respuesta.
5. Reenviar el mismo `eventId` y confirmar idempotencia.
6. Probar payload inválido, outcome desconocido y mensaje sobredimensionado.
7. Detener y reconectar el cliente con el mismo header.
8. Usar REST como fallback si SignalR no está disponible.

## MongoDB

- Usar únicamente una base temporal de esta ejecución.
- Verificar colecciones `profiles` y `reaction_logs`.
- Crear/verificar los índices requeridos antes de la prueba si el procedimiento
  operativo lo exige.
- Comprobar perfil, XP, reacción, `eventId`, `outcomeType` y
  `unityOutcomeType`.
- No eliminar ni modificar bases o colecciones preexistentes.
- Si existen duplicados de `eventId` que impiden crear el índice único,
  conservarlos, reportarlos y detener la prueba; no resolverlos mediante
  borrado automático.
- Al finalizar, limpiar solo los datos creados por la prueba o conservar la base
  temporal únicamente con autorización para diagnóstico.

## HTTPS/WSS posterior

La prueba loopback HTTP no valida Android. Para Android físico se requiere:

- API accesible mediante un nombre/IP autorizado, sin exposición pública.
- Certificado cuyo nombre coincida con el endpoint usado por Unity.
- Cadena de confianza instalada de forma controlada en el dispositivo o una CA
  empresarial confiable.
- Validación TLS mantenida; no aceptar certificados inválidos en producción.
- Transporte `wss` y headers de autenticación durante negociación y WebSocket.
- Regla de red/firewall aprobada explícitamente; este plan no autoriza crearla.

## Evidencias Android

- Modelo, versión Android y versión de la aplicación Unity.
- URL usada, transporte SignalR y resultado del handshake.
- Resultado de validación del certificado, sin incluir claves privadas.
- Timestamp, `eventId` anonimizado y `device_id` no secreto.
- Códigos HTTP/SignalR y campos `source`/`historyDuplicate`.
- Captura del historial correspondiente en MongoDB sin tokens.
- Evidencia de reconexión, fallback REST y fallback local sin servidor.
- Confirmación de que no hubo llamadas OpenAI ni exposición de secretos.

## Riesgos y bloqueos

- El certificado de desarrollo `CN=localhost` no sirve para un Android que use la
  IP LAN.
- No se deben desactivar TLS, autenticación ni validación de certificados.
- No se deben abrir puertos a Internet ni modificar el firewall sin autorización.
- La prueba local con `TestServer` o loopback no demuestra compatibilidad Android.
- Unity debe confirmar soporte de headers personalizados en negociación y WSS.

## Criterios de aceptación

- Las pruebas automatizadas y manuales locales pasan sin modificar producción.
- API, REST y SignalR rechazan solicitudes no autenticadas.
- Perfil, XP e historial se persisten correctamente en la base temporal.
- Reintentos con el mismo `eventId` son idempotentes.
- Na/Cl produce `OfflineFallback` sin llamadas externas.
- Los outcomes Unity 0..3 se mapean correctamente y 1, 2 y 3 se verifican.
- No se imprimen ni persisten tokens reales.
- El proceso y configuración temporal se retiran al finalizar.
- La aceptación Android queda pendiente hasta completar una prueba con dispositivo
  físico, HTTPS/WSS y certificado confiable.
