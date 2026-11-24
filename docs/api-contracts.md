# Contratos previstos

Estos contratos son una referencia inicial para la integración con Unity. Todavía deben verificarse con el cliente real de Unity antes de implementarse o versionarse como API estable.

## Salud

- `GET /health`

## Tutor de IA

- Hub SignalR: `/hubs/aitutor`
- Método cliente: `ExplainReaction(string reactionJson, string clientSystemPrompt)`
- Evento: `ReceiveTutorExplanation`
- HTTP: `POST /api/reactions/explain`

El endpoint HTTP y el Hub SignalR están implementados. La compatibilidad end-to-end con Unity aún debe verificarse con el cliente real.

Payload compatible con Unity:

```json
{
  "reactantA": "Na",
  "reactantB": "Cl",
  "productFormula": "NaCl",
  "productDisplayName": "Cloruro de Sodio (Red Cristalina)",
  "reactionEquation": "Na⁺ + Cl⁻ → NaCl (s)",
  "isValid": true,
  "outcomeType": 1,
  "deltaEnthalpyKjMol": -411.2,
  "electronsTransferred": 1,
  "thermodynamicClassification": "Exotérmica",
  "timestampUnixMs": 1732068000000,
  "eventId": "unity-event-001"
}
```

Respuesta mínima compatible:

```json
{
  "explanation": "Explicación pedagógica.",
  "source": "OfflineFallback",
  "historyDuplicate": false
}
```

`source`, `historyDuplicate` y `eventId` son extensiones opcionales. La entalpía, ecuación y validez son datos declarados por Unity; el ejemplo no afirma que la entalpía corresponda exactamente al proceso visualizado. El backend no implementa un validador químico centralizado.

La explicación se limita a 25 palabras. OpenAI recibe una instrucción controlada por el backend mediante Responses API y el payload se marca como dato no confiable; ningún campo textual de Unity puede modificar las instrucciones internas. Cuando el proveedor está disponible, `source` es `OpenAIOnline`; `MemoryCache` y `OfflineFallback` conservan el contrato público existente.

### OutcomeType de Unity

`outcomeType` llega como número y usa exclusivamente los valores públicos de Unity:

| Unity | Valor | Dominio persistido |
| --- | ---: | --- |
| `None` | 0 | `Unknown` |
| `IonicCrystalBond` | 1 | `Valid` |
| `ExothermicAlkaliExplosion` | 2 | `Valid` |
| `ElectrostaticRepulsion` | 3 | `NoReaction` |

El traductor se aplica en la frontera REST/SignalR. Los valores desconocidos se rechazan; nunca se convierten mediante un cast numérico al enum del dominio. `isValid` es independiente y se conserva exactamente como lo declara Unity. Por ello se aceptan combinaciones potencialmente inconsistentes, como `ExothermicAlkaliExplosion` con `isValid=false` o `ElectrostaticRepulsion` con `isValid=true`, sin recalcular química ni imponer una validación no definida por Unity.

El historial puede incluir `unityOutcomeType` como metadata numérica opcional para conservar el tipo específico original. Los documentos existentes sin ese campo siguen siendo válidos. La respuesta de explicación no cambia:

```json
{
  "explanation": "Explicación pedagógica.",
  "source": "OfflineFallback",
  "historyDuplicate": false
}
```

### Chat con el tutor

- HTTP: `POST /api/tutor/chat` (requiere `X-Atomiq-Device-Token` y comparte el rate limit `mobile-api`).
- Conversación libre de química para el estudiante. No usa MongoDB ni guarda historial en el servidor: el cliente envía los últimos mensajes en cada petición.

```json
{
  "message": "¿Por qué el sodio reacciona con el agua?",
  "history": [
    { "role": "student", "text": "Hola" },
    { "role": "tutor", "text": "¡Hola! ¿Qué quieres saber?" }
  ],
  "challenge": "Sodio (Na) + agua (H₂O)",
  "lastReaction": "2Na + 2H₂O → 2NaOH + H₂↑"
}
```

| Campo | Regla |
|---|---|
| `message` | Obligatorio, máximo 400 caracteres. |
| `history` | Opcional, máximo 8 mensajes; `role` es `student` o `tutor`; `text` máximo 600 caracteres. |
| `challenge`, `lastReaction` | Opcionales, máximo 200 caracteres. Contexto del laboratorio AR. |

```json
{
  "reply": "El sodio cede su electrón y reacciona con el agua: forma NaOH, libera hidrógeno y mucho calor.",
  "source": "OpenAIOnline"
}
```

- Con OpenAI habilitado responde gpt-4o con instrucciones de tutor (español, máximo 80 palabras, solo química, sin instrucciones para experimentos peligrosos reales). El mensaje, el historial y el contexto se envían como datos no confiables, nunca como instrucciones.
- Si OpenAI está deshabilitado, falla, excede el tiempo, devuelve marcado (`<`, `>`) o pasa de 90 palabras, responde una base local por palabras clave con `source = "OfflineFallback"`.
- Las respuestas del chat no se guardan en caché.
- Errores: `400` con `ValidationProblem` si los campos no cumplen las reglas; `401` sin token válido; `429` al exceder el rate limit.

## Perfil y experiencia

- `GET /api/profile/{deviceId}`
- `POST /api/profile/xp`
- `GET /api/reactions/history?page=1&pageSize=20`

Los endpoints de perfil, XP, historial y explicación HTTP están implementados. SignalR está expuesto en `/hubs/aitutor` con el contrato descrito en [docs/unity-signalr-integration.md](unity-signalr-integration.md).

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
