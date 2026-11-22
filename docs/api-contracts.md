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

Los endpoints de negocio anteriores son contratos previstos y no están implementados como respuestas exitosas en esta fase.

`clientSystemPrompt` es una entrada no confiable. Nunca debe controlar ni reemplazar las instrucciones internas del tutor; deberá tratarse como datos con límites, validación y políticas del servidor.
