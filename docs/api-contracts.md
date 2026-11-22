# Contratos previstos

Estos contratos son una referencia inicial para la integración con Unity. Todavía deben verificarse con el cliente real de Unity antes de implementarse o versionarse como API estable.

## Salud

- `GET /health`

## Tutor de IA

- Hub SignalR: `/hubs/aitutor`
- Método cliente: `ExplainReaction(string reactionJson, string clientSystemPrompt)`
- Evento: `ReceiveTutorExplanation`
- HTTP: `POST /api/reactions/explain`

## Perfil y experiencia

- `GET /api/profile/{deviceId}`
- `POST /api/profile/xp`
- `GET /api/reactions/history?page=1&pageSize=20`

Los endpoints de perfil, XP e historial están implementados. Los contratos de Gemini y SignalR siguen siendo previstos y no están expuestos.

## Autenticación y autorización

Los endpoints bajo `/api` requieren:

```http
X-Atomiq-Device-Token: <token-provisionado-fuera-del-repositorio>
```

El token se configura en el servidor mediante `Authentication:DeviceTokens`. La claim autenticada `device_id` determina el perfil autorizado. Un `deviceId` de ruta o body debe coincidir con esa claim; el identificador por sí solo nunca actúa como credencial.

El cliente debe usar HTTPS en entornos conectados. La autenticación actual es una política de prototipo para stands y requiere un mecanismo seguro de provisión de tokens en Unity. No se debe incrustar un token permanente en el APK.

## JSON de perfil

`GET /api/profile/{deviceId}` devuelve únicamente datos públicos del perfil:

```json
{
  "deviceId": "device-1",
  "currentLevel": 0,
  "totalXp": 25,
  "discoveredReactions": ["reaction-001"]
}
```

## JSON de XP

`POST /api/profile/xp` recibe:

```json
{
  "deviceId": "device-1",
  "eventId": "reaction-event-001",
  "xpGained": 25
}
```

La respuesta incluye `applied` y `duplicateEvent`. Un `EventId` repetido no vuelve a sumar XP.

## Historial

`GET /api/reactions/history` devuelve `items`, `page`, `pageSize` y `hasNextPage`. `pageSize` está limitado a 100 y los resultados se ordenan por timestamp descendente.

## Errores

- `400`: entrada inválida, mediante ProblemDetails.
- `401`: falta autenticación válida.
- `403`: el token no corresponde al dispositivo solicitado.
- `404`: perfil inexistente al modificar XP.
- `429`: límite de solicitudes excedido.

`clientSystemPrompt` es una entrada no confiable. Nunca debe controlar ni reemplazar las instrucciones internas del tutor; deberá tratarse como datos con límites, validación y políticas del servidor.

## Datos proporcionados por Unity

`ReactionLog.IsValid` representa el resultado del motor local de Unity. El backend valida la forma del mensaje, identificadores, fechas y límites numéricos, pero no introduce una nueva lógica científica para declarar una reacción válida. Los nombres y valores numéricos de `ChemicalSpeciesId` y `ReactionOutcomeType` deben confirmarse con el código real de Unity; MongoDB los persiste por nombre, no por ordinal.
