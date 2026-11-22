# Arquitectura

Atomiq usa una separación por capas inspirada en Clean Architecture.

## Dependencias

```text
Domain <- Application <- Infrastructure <- Api
```

Domain no conoce frameworks ni otras capas. Application expresa casos de uso y contratos. Infrastructure contiene integraciones externas y options tipadas. Api compone la aplicación y expone HTTP.

La composición actual prepara DI, ProblemDetails, manejo global de excepciones, CORS explícito y los puntos de extensión para SignalR y Gemini. MongoDB se registra de forma diferida: no se conecta ni crea índices hasta que se resuelve un repositorio o se invoca explícitamente el inicializador de índices.

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

## API y seguridad

La composición HTTP expone perfiles, XP e historial bajo `/api`. Los endpoints requieren autenticación `StandToken`, que resuelve un token configurado externamente a un `device_id`. La API compara esa identidad con cualquier `deviceId` recibido para evitar acceso cruzado.

El rate limiting usa una ventana fija por dispositivo autenticado, con fallback a dirección IP para solicitudes no autenticadas. Los valores son configurables y el límite predeterminado es 60 solicitudes por 60 segundos.

Los endpoints no exponen el identificador interno del perfil. La validación HTTP limita formato y tamaño de identificadores, XP por evento y tamaño de página.

## Consistencia e idempotencia

La actualización de XP es atómica dentro del documento de perfil mediante un filtro por `EventId`, límite de `int.MaxValue` y `$inc`/`$addToSet`. El historial usa un índice único sobre `EventId`. La escritura de historial y la actualización de XP son operaciones separadas; sin una transacción MongoDB soportada no se afirma atomicidad entre ambas. Los reintentos son seguros por separado y el riesgo de una caída entre operaciones queda pendiente para una futura estrategia transaccional o de reconciliación.
