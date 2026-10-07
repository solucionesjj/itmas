# Constitución del proyecto

**Versión:** 1.0.1 · **Vigente desde:** 2026-10-07 · **Origen:** [SPEC-0000](specs/architecture/SPEC-0000-application-foundation.md)

Esta constitución es la política técnica permanente del proyecto. Toda especificación, plan, cambio de código,
configuración o documentación (hecho por personas o por agentes de IA) debe cumplirla.

Los términos **DEBE**, **NO DEBE**, **DEBERÍA** y **PUEDE** tienen el sentido de RFC 2119.

---

## §1. Supremacía y enmiendas

1. Ninguna especificación puede contradecir esta constitución. Si una spec necesita hacerlo, primero DEBE
   aprobarse una **enmienda**.
2. Una enmienda DEBE: (a) proponerse como ADR en `specs/architecture/` con motivación, alternativas e impacto;
   (b) ser revisada por los agentes de arquitectura y seguridad; (c) incrementar la versión de este documento
   (MAJOR si elimina o relaja una regla, MINOR si añade una, PATCH si aclara) y registrarse en el historial.
3. En caso de conflicto entre documentos, prevalece: constitución → ADRs aceptados → specs → resto de documentación.

## §2. Principios arquitectónicos

1. **Clean Architecture.** El backend DEBE mantener las capas `Domain`, `Application`, `Infrastructure` y `Api`
   con dependencias exclusivamente hacia dentro: `Api → Infrastructure → Application → Domain`.
2. `Domain` NO DEBE depender de ningún otro proyecto ni de paquetes de framework (EF Core, ASP.NET Core, Npgsql).
3. `Application` NO DEBE depender de `Infrastructure`, `Api`, EF Core ni ASP.NET Core. Define **puertos**
   (interfaces); `Infrastructure` los implementa.
4. Los controllers DEBEN ser delgados: validar el contrato, invocar un caso de uso, traducir el resultado.
   NO DEBEN contener lógica de negocio ni acceso a datos.
5. Se DEBE preferir la solución más simple que cumpla la spec. Introducir una librería, patrón o servicio nuevo
   de impacto transversal requiere ADR.
6. Las reglas §2.1–§2.3 están verificadas por `LayerDependencyTests` y NO DEBEN desactivarse.

## §3. Seguridad

1. **Secure by design.** Ante un conflicto entre seguridad y conveniencia, prevalece la seguridad.
2. **Autenticación:** Microsoft Entra ID mediante el patrón BFF. El navegador NO DEBE recibir ni almacenar tokens
   de acceso, refresco o identidad. La sesión vive en una cookie `HttpOnly; Secure; SameSite=Strict`.
3. **Default deny:** todo endpoint DEBE declarar explícitamente su autorización:
   `[AllowAnonymous]`, `[RequirePermission("<recurso>.<acción>")]` (o `<recurso>.<subrecurso>.<acción>`; minúsculas,
   mínimo dos segmentos) o, solo para operaciones sobre la propia identidad, la política `AuthenticatedUser`.
   Un `[Authorize]` sin política está prohibido. Todo permiso usado DEBE estar declarado en el catálogo de permisos.
4. Estar autenticado NO DEBE conceder privilegios sobre recursos de la aplicación. Los permisos se conceden
   explícitamente mediante políticas (ADR-0008) y se evalúan en el backend mediante `IPermissionEvaluator`.
   No se introducen roles como mecanismo de autorización.
5. El frontend NUNCA es una frontera de seguridad. Guards y ocultación de UI son solo experiencia de usuario.
6. Toda entrada externa DEBE validarse en el backend. JSON con propiedades desconocidas se rechaza.
7. Los errores DEBEN devolverse como Problem Details (RFC 9457) y NO DEBEN exponer stack traces, mensajes internos,
   datos de infraestructura ni secretos.
8. Mínimo privilegio en todo: roles de base de datos, contenedores no root, capacidades eliminadas, redes internas,
   puertos no publicados salvo el del frontend.
9. Las protecciones existentes (CSRF, cabeceras, CSP, rate limiting, CORS cerrado, validación de `returnUrl`,
   filtrado de hosts) NO DEBEN relajarse sin enmienda.
10. Toda spec DEBE incluir una sección de consideraciones de seguridad (amenazas, datos sensibles, permisos).

## §4. Secretos y configuración

1. NO DEBE existir ningún secreto en el repositorio: contraseñas, client secrets, claves privadas, certificados,
   tokens, API keys ni cadenas de conexión con credenciales. Tampoco en ejemplos, tests o comentarios.
2. La configuración sigue el flujo **código → configuración (sin secretos) → variables de entorno → runtime del contenedor**.
3. Toda variable nueva DEBE documentarse en `.env.example` (sin valor real) y en `README.md`.
4. El frontend NO DEBE contener secretos ni configuración del proveedor de identidad. Solo valores públicos.
5. Los secretos NO DEBEN pasarse por línea de comandos ni escribirse en logs, imágenes o capas de Docker.
6. Los tests DEBEN usar valores generados aleatoriamente o marcadores evidentes (`placeholder-not-a-secret`).

## §5. Acceso a datos

1. PostgreSQL es la única base de datos; el acceso desde .NET DEBE hacerse con EF Core (proveedor Npgsql).
2. NO DEBE construirse SQL concatenando o interpolando entrada. Cualquier SQL explícito DEBE ser parametrizado
   (`FromSql` con interpolación segura o `NpgsqlParameter`) y justificarse en la spec.
3. Cada tipo persistido DEBE tener su `IEntityTypeConfiguration<T>` en `Infrastructure/Persistence/Configurations/`.
   Las entidades de dominio NO DEBEN llevar atributos de persistencia.
4. Todo cambio de esquema DEBE hacerse con una migración EF Core derivada de una spec, revisando el SQL generado.
   Una migración aplicada en un entorno compartido NO DEBE modificarse; se corrige con otra migración.
5. Las migraciones las aplica únicamente el servicio `migrator`. La API NO DEBE ejecutar DDL ni migrar al arrancar.
6. La API usa un rol sin DDL. Conceder privilegios adicionales requiere spec de base de datos y revisión de seguridad.
7. No se crean entidades ficticias. Las entidades técnicas se identifican claramente como infraestructura.

## §6. Frontend

1. Angular (versión estable soportada) con componentes standalone, signals y rutas con carga diferida por feature.
2. La UI DEBE usar **Angular Material siguiendo Material Design 3 estándar**: componentes oficiales siempre que exista
   uno adecuado; paletas, tipografía, densidad y elevaciones por defecto del tema definido en `styles.scss`.
3. NO DEBE crearse un sistema de diseño propio ni sobrescribir tokens, colores, tipografías o el comportamiento de
   los componentes Material. El CSS de componentes se limita a layout y usa variables `--mat-sys-*`.
4. Los temas claro y oscuro DEBEN seguir funcionando para toda pantalla nueva.
5. Estructura obligatoria: `core/` (singletons transversales), `auth/`, `layout/`, `shared/` (presentación
   reutilizable), `features/<feature>/` (una carpeta por área). `shared` y `core` NO DEBEN depender de `features`.
6. Las llamadas HTTP DEBEN usar `HttpClient` con rutas relativas del mismo origen; los errores se gestionan con los
   interceptores existentes y Problem Details.
7. Textos de UI en español y accesibles (etiquetas ARIA en botones de icono, contraste del tema, navegación por teclado).

## §7. API

1. REST sobre JSON, rutas `/api/v<n>/<recurso>` en plural y en kebab-case; `/auth/*` reservado al BFF.
2. Los contratos HTTP (DTOs) viven en `Api/Contracts/` y NO DEBEN exponer entidades de dominio ni de persistencia.
3. Cada endpoint DEBE: declarar autorización (§3.3), validar la entrada, devolver Problem Details en error y estar
   descrito en la spec (método, ruta, contrato, códigos de estado, permiso).
4. Los cambios incompatibles requieren una nueva versión de la ruta (`v2`).
5. El documento OpenAPI DEBE generarse sin errores (`/openapi/v1.json` en Development).

## §8. Docker e infraestructura

1. Todo el sistema DEBE poder levantarse con `docker compose up --build`.
2. Imágenes multi-stage, base mínima (Alpine/chiseled), usuario no root, sin herramientas de build en runtime.
3. Cada servicio DEBE tener health check significativo; las dependencias se expresan con `condition: service_healthy`
   o `service_completed_successfully`.
4. Solo el frontend publica un puerto (en `127.0.0.1` en local). La base de datos vive en una red `internal`.
5. Los contenedores DEBERÍAN ejecutarse con `read_only`, `cap_drop: [ALL]` y `no-new-privileges` cuando sea posible.
6. Ningún secreto en Dockerfiles, `docker-compose.yml` ni argumentos de build.

## §9. Testing

1. Todo cambio de comportamiento DEBE venir con pruebas automatizadas en el nivel adecuado:
   unitarias (dominio, casos de uso), integración (HTTP + PostgreSQL real), frontend (servicios, guards, componentes).
2. Cada criterio de aceptación de una spec DEBE estar cubierto por al menos una prueba o una verificación documentada.
3. Toda regla de autorización nueva DEBE tener pruebas del caso permitido y del denegado (401 y 403).
4. Las pruebas NO DEBEN depender de servicios externos (Entra ID, Internet). Testcontainers sí es válido.
5. La suite completa DEBE pasar antes de integrar un cambio. Está prohibido desactivar o borrar pruebas para que pase.

## §10. Documentación

1. La documentación es parte del cambio: README, ARCHITECTURE, SECURITY y DEVELOPMENT DEBEN actualizarse en el
   mismo cambio que los invalida.
2. Toda decisión arquitectónica relevante DEBE registrarse como ADR.
3. Las specs implementadas DEBEN actualizar su estado e indicar los archivos y pruebas que las materializan.

## §11. Evolución mediante SDD

1. Ninguna funcionalidad o cambio relevante comienza modificando código. El flujo obligatorio es:
   **Idea → Specification → Architecture Analysis → Implementation Plan → Implementation → Tests → Validation → Documentation**.
2. Cada cambio DEBE ser trazable a una spec (`SPEC-NNNN`) o ADR (`ADR-NNNN`), referenciado en el commit/PR.
3. Las specs siguen las plantillas de `specs/_templates/` y el ciclo `Draft → Approved → In Progress → Implemented`
   (o `Superseded`/`Rejected`).
4. NO DEBEN inventarse requisitos de negocio: lo no especificado se pregunta, no se supone.
5. Los agentes de IA DEBEN respetar los límites de `agents/` (archivos que pueden modificar, agentes a consultar).
6. Correcciones menores sin cambio de comportamiento (erratas, dependencias de parche, refactors locales) PUEDEN
   hacerse sin spec, referenciando el motivo en el commit.

## §12. Estándares de código

1. Backend: `TreatWarningsAsErrors`, analizadores .NET, *nullable* habilitado, versiones centralizadas en
   `Directory.Packages.props`, NuGet Audit activo, `async` de extremo a extremo con `CancellationToken`.
2. Logging con `LoggerMessage` (source-generated) y plantillas estructuradas; nunca interpolación de cadenas.
3. Frontend: TypeScript `strict`, Prettier, sin `any` implícito, un componente por archivo.
4. Código y comentarios en inglés; documentación y UI en español.
5. Las dependencias nuevas DEBEN estar mantenidas activamente, tener licencia compatible y justificarse en la spec.

---

## Historial

| Versión | Fecha | Cambio |
|---|---|---|
| 1.0.0 | 2026-10-07 | Versión inicial (fundación). |
| 1.0.1 | 2026-10-07 | §3.3: convención de nombres `<recurso>.<acción>` y catálogo obligatorio; §3.4: políticas, sin roles (SPEC-0001, ADR-0008). |
