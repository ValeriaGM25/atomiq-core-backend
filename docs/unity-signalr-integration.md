# Integración Unity con SignalR

## Estado

El Hub está implementado en `/hubs/aitutor`. Esta integración fue probada con el cliente SignalR de .NET y un transporte WebSocket contra `TestServer`. No se ejecutó una prueba end-to-end con el cliente Unity real porque su código no forma parte de este repositorio.

## Contrato

Método del cliente:

```text
ExplainReaction(string reactionJson, string clientSystemPrompt)
```

Evento del servidor:

```text
ReceiveTutorExplanation(string tutorResponseJsonOrText)
```

El evento contiene actualmente un JSON serializado:

```json
{
  "explanation": "El sodio transfiere un electrón al cloro...",
  "source": "OfflineFallback",
  "historyDuplicate": false
}
```

El parámetro `clientSystemPrompt` se conserva por compatibilidad, pero se trata como dato no confiable y no modifica la instrucción interna del tutor.

El `reactionJson` usa el mismo payload que `POST /api/reactions/explain`. Puede incluir opcionalmente `eventId` para que un reintento REST/SignalR conserve la misma identidad:

```json
{
  "eventId": "unity-event-001",
  "reactantA": "Na",
  "reactantB": "Cl",
  "productFormula": "NaCl",
  "productDisplayName": "Cloruro de Sodio",
  "reactionEquation": "Na+ + Cl- -> NaCl (s)",
  "isValid": true,
  "outcomeType": 1,
  "deltaEnthalpyKjMol": -411.2,
  "electronsTransferred": 1,
  "thermodynamicClassification": "Exotérmica",
  "timestampUnixMs": 1732068000000
}
```

Si `eventId` no está presente, el backend conserva la generación determinista histórica como compatibilidad provisional.

## Autenticación

La conexión debe autenticarse mediante el header:

```http
X-Atomiq-Device-Token: <token-provisionado>
```

El servidor deriva `device_id` de la identidad autenticada. No se acepta un `deviceId` enviado por el cliente como sustituto de autenticación.

El cliente Unity debe confirmar que su implementación permite headers personalizados durante la negociación HTTP y el transporte WebSocket. No se deben enviar tokens en query strings ni incrustarlos permanentemente en el APK.

## Protocolo y transporte

Se utiliza el protocolo JSON oficial de SignalR. El cliente SignalR gestiona handshake, `invocationId`, separador de registros `0x1E`, ping, cierre y reconexión. La aplicación no construye un protocolo WebSocket propio.

El servidor limita el mensaje recibido a 64 KiB y aplica rate limiting por identidad. La duración de la llamada al proveedor OpenAI usa el timeout configurado existente.

## Fallback REST

`POST /api/reactions/explain` permanece disponible para clientes que no puedan usar SignalR. Ambos caminos reutilizan `ExplainReactionUseCase`, `IAiTutorService`, caché, fallback y persistencia idempotente.

La experiencia AR local debe continuar aunque el Hub, MongoDB u OpenAI no estén disponibles. En ese caso la explicación puede proceder de `OfflineFallback` o la operación puede informar un fallo de persistencia sin ocultarlo.

## Pendiente con Unity

- Confirmar headers durante WebSocket y negociación.
- Confirmar el parser Unity para el evento JSON serializado.
- Confirmar si Unity enviará `eventId` para reintentos.
- Ejecutar prueba end-to-end con el cliente Unity Android/ARCore.
