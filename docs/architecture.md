# Arquitectura

Atomiq usa una separación por capas inspirada en Clean Architecture.

## Dependencias

```text
Domain <- Application <- Infrastructure <- Api
```

Domain no conoce frameworks ni otras capas. Application expresa casos de uso y contratos. Infrastructure contiene integraciones externas y options tipadas. Api compone la aplicación y expone HTTP.

La composición actual prepara DI, ProblemDetails, manejo global de excepciones, CORS explícito y los puntos de extensión para SignalR y OpenAI. MongoDB se registra de forma diferida: no se conecta ni crea índices hasta que se resuelve un repositorio o se invoca explícitamente el inicializador de índices.

## Tutor OpenAI

`ExplainReactionUseCase` coordina el perfil autenticado, `IAiTutorService` y el historial. `AiTutorService` valida estructura y límites, genera una clave completa de caché incluyendo proveedor, modelo y versión del prompt, consulta `IOpenAiClient` solo cuando la política está habilitada y usa fallback local ante indisponibilidad del proveedor.

El cliente OpenAI usa `IHttpClientFactory`, Responses API, timeout configurable y `Authorization: Bearer`; la clave no se incluye en URLs ni logs. Se manejan 401, 403, 429, 5xx, timeout, cancelación, respuestas vacías y JSON inesperado sin interrumpir la experiencia AR: esos casos seleccionan fallback, excepto cancelación explícita del usuario.

La instrucción de sistema pertenece al backend. Los campos de Unity se serializan como datos dentro del prompt y nunca como instrucciones. No se recalculan reacciones ni propiedades químicas.

## Modelo de datos

`UserProfile` usa un `Id` string neutral al proveedor, `DeviceId` único, XP entero no negativo, reacciones descubiertas sin duplicados y fechas UTC. Los `EventId` de XP aplicados se conservan en el documento para que una repetición no incremente dos veces el total.

`ReactionLog` conserva el resultado entregado por Unity: reactantes, producto, ecuación, clasificación termodinámica, `IsValid`, tipo de resultado y explicación opcional. El backend valida formato, identificadores y límites, pero no determina la validez química.

## Persistencia

Los documentos BSON de `Infrastructure.Mongo` están separados del dominio. Los enums se serializan por nombre y los identificadores se mapean explícitamente. Las colecciones son `profiles` y `reaction_logs`.

Los índices definidos son:

- `profiles.deviceId` único.
- `reaction_logs.eventId` único.
- `reaction_logs.deviceId + timestamp` compuesto.
- `reaction_logs.reactantA + reactantB` compuesto.

`MongoIndexInitializer.EnsureIndexesAsync` usa creación idempotente y nunca elimina índices ni datos, pero requiere invocación operativa explícita.

Las pruebas reales de persistencia viven en `Atomiq.Infrastructure.IntegrationTests`. Cada ejecución usa una base `atomiq_integration_<id>` en un host loopback, inspecciona los índices creados y limpia únicamente los documentos de esa base. No prueban Atlas ni producción.

## API y seguridad

La composición HTTP expone perfiles, XP e historial bajo `/api`. Los endpoints requieren autenticación `StandToken`, que resuelve un token configurado externamente a un `device_id`. La API compara esa identidad con cualquier `deviceId` recibido para evitar acceso cruzado.

El rate limiting usa una ventana fija por dispositivo autenticado, con fallback a dirección IP para solicitudes no autenticadas. Los valores son configurables y el límite predeterminado es 60 solicitudes por 60 segundos.

Los endpoints no exponen el identificador interno del perfil. La validación HTTP limita formato y tamaño de identificadores, XP por evento y tamaño de página.

## Consistencia e idempotencia

La actualización de XP es atómica dentro del documento de perfil mediante un filtro por `EventId`, límite de `int.MaxValue` y `$inc`/`$addToSet`. El historial usa un índice único sobre `EventId`. La escritura de historial y la actualización de XP son operaciones separadas; sin una transacción MongoDB soportada no se afirma atomicidad entre ambas. Los reintentos son seguros por separado y el riesgo de una caída entre operaciones queda pendiente para una futura estrategia transaccional o de reconciliación.

La suite de integración confirma la atomicidad de las actualizaciones concurrentes de XP y la idempotencia de reintentos con el mismo `EventId`. Un evento nuevo con contenido idéntico sigue siendo un evento distinto. No existe una transacción entre XP e historial: una caída entre ambas escrituras puede dejar una operación parcialmente aplicada y requiere reconciliación o una futura estrategia transaccional.

## SignalR y Unity

`AITutorHub` es transitorio y no conserva estado mutable de sesión. Usa el mismo `ExplainReactionUseCase` que REST, por lo que mantiene las mismas validaciones, caché, fallback, autenticación e idempotencia. El Hub exige autorización, limita mensajes a 64 KiB y aplica el rate limiting existente.

La caché identifica la explicación por contenido científico y contexto pedagógico, no por el timestamp de ejecución. `EventId` identifica la operación de historial; puede llegar opcionalmente desde Unity o conservar la generación determinista anterior.
