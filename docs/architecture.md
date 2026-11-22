# Arquitectura

Atomiq usa una separación por capas inspirada en Clean Architecture.

## Dependencias

```text
Domain <- Application <- Infrastructure <- Api
```

Domain no conoce frameworks ni otras capas. Application expresa casos de uso y contratos. Infrastructure contiene integraciones externas y options tipadas. Api compone la aplicación y expone HTTP.

La composición actual prepara DI, ProblemDetails, manejo global de excepciones, CORS explícito y los puntos de extensión para SignalR, MongoDB y Gemini sin abrir conexiones externas.
