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

Los endpoints de negocio anteriores son contratos previstos y no están implementados como respuestas HTTP en esta fase. Los casos de uso internos de perfiles e historial no se exponen todavía.

`clientSystemPrompt` es una entrada no confiable. Nunca debe controlar ni reemplazar las instrucciones internas del tutor; deberá tratarse como datos con límites, validación y políticas del servidor.

## Datos proporcionados por Unity

`ReactionLog.IsValid` representa el resultado del motor local de Unity. El backend valida la forma del mensaje, identificadores, fechas y límites numéricos, pero no introduce una nueva lógica científica para declarar una reacción válida. Los nombres y valores numéricos de `ChemicalSpeciesId` y `ReactionOutcomeType` deben confirmarse con el código real de Unity; MongoDB los persiste por nombre, no por ordinal.
