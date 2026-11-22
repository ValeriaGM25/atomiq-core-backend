# Atomiq

Atomiq es un laboratorio inmersivo de dinámica molecular y reacciones químicas en realidad aumentada. Este repositorio contiene el backend ASP.NET Core que dará soporte a la experiencia Unity para Android y ARCore.

## Tecnologías

- .NET 10 y ASP.NET Core Minimal APIs
- C# con nullable reference types y analyzers habilitados
- Clean Architecture
- xUnit y `WebApplicationFactory`
- Puntos de extensión preparados para SignalR, MongoDB y Gemini

Esta fase no implementa lógica de Gemini, SignalR, MongoDB ni gamificación.

## Requisitos

- Windows, Linux o macOS
- SDK .NET 10.0.401 o compatible con `global.json`
- Git

## Arquitectura

- `Atomiq.Domain`: modelo y reglas de negocio independientes.
- `Atomiq.Application`: casos de uso y puertos de aplicación; depende de Domain.
- `Atomiq.Infrastructure`: adaptadores técnicos y configuración; depende de Application y Domain.
- `Atomiq.Api`: composición HTTP, middleware, DI y endpoints; depende de Application e Infrastructure.

La validación científica y la simulación química corresponden al cliente Unity Android/ARCore, no a este backend.

## Estructura

```text
src/       Domain, Application, Infrastructure y Api
tests/     pruebas por capa
docs/      arquitectura, contratos y desarrollo
```

## Comandos

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet run --project src/Atomiq.Api
```

El endpoint inicial es `GET /health`. Los endpoints de negocio no se crean como placeholders exitosos.

## Configuración local

La configuración de ejemplo se encuentra en `src/Atomiq.Api/appsettings.json`. Los valores sensibles deben proporcionarse mediante User Secrets o variables de entorno, nunca mediante Git.

```bash
dotnet user-secrets init --project src/Atomiq.Api
dotnet user-secrets set "Gemini:ApiKey" "valor-local" --project src/Atomiq.Api
```

## Contratos Unity

Los contratos previstos para el cliente Unity están documentados en [docs/api-contracts.md](docs/api-contracts.md). Deben verificarse con el cliente real antes de implementarse.

## Contribución

Crear ramas con nombres descriptivos desde `main`, mantener cambios pequeños y ejecutar restore, build y test antes de abrir un pull request. No incluir secretos, archivos locales ni conexiones reales.
