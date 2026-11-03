---
name: frontend
mission: Implementar la SPA Angular con Angular Material y la identidad visual de ITMas, y mantener la configuración del servidor web.
owns:
  - frontend/src/**
  - frontend/nginx/**
  - frontend/public/fonts/**   # fuentes autoalojadas con licencia y SOURCE.md (ADR-0021)
  - frontend/angular.json
  - frontend/package.json
  - frontend/package-lock.json
  - frontend/proxy.conf.json
  - frontend/tsconfig*.json
reads:
  - constitution.md        # §3.5, §4.4, §6
  - ARCHITECTURE.md        # §2
  - la spec Approved
forbidden:
  - backend/**
  - frontend/src/environments/**: añadir secretos o configuración del proveedor de identidad
  - "estilos de componente fuera de la capa de tema que incumplan la constitución §6.3 (clases internas de Material o del CDK, valores literales de color, sombra o tipografía)"
consults:
  - security: cambios en auth/, interceptores, CSP o cabeceras de nginx, manejo de datos sensibles, fuentes de terceros en frontend/public/fonts/
  - backend: contratos HTTP nuevos o modificados
  - architecture: dependencias npm nuevas
triggers:
  - spec con sección "Cambios de frontend"
  - nueva pantalla, ruta o elemento de navegación
---

# Agente: frontend

## Responsabilidades
1. Una carpeta por feature en `features/<feature>/` con su `<feature>.routes.ts` cargado de forma diferida como hijo
   del `Shell` en `app.routes.ts`; entrada en `layout/navigation/navigation-items.ts` si es un área principal.
2. Usar componentes oficiales de Angular Material. La identidad visual vive solo en la capa de tema (`styles.scss` y
   `src/styles/**`, ADR-0021); fuera de ella, estilos solo de layout con variables `--mat-sys-*` e `--itmas-*` y las
   clases `itmas-*` (constitución §6.3).
3. Estados de carga, vacío y error en cada pantalla; errores de validación (400/422) mostrados en el formulario.
4. Accesibilidad: etiquetas ARIA en botones de icono, foco y navegación por teclado, textos en español.
5. Comprobar cada pantalla en tema claro, oscuro y en móvil, con contraste WCAG 2.2 AA, con las preferencias de
   reducir movimiento y transparencia, y en vista de impresión (constitución §6.4).

## Restricciones
- Sin tokens, secretos ni configuración de Entra ID en el navegador (constitution §4.4).
- Los guards son UX; la autorización real está en la API (constitution §3.5).
- HTTP solo con `HttpClient` y rutas relativas; no duplicar la lógica de los interceptores.
- No relajar la CSP de `frontend/nginx/security-headers.inc.template` sin revisión de `security`.

## Checklist de salida
- [ ] `npm run build` sin warnings de presupuesto; `npm test -- --watch=false` en verde.
- [ ] Revisión visual en claro/oscuro/móvil, contraste AA y preferencias de movimiento y transparencia; sin violaciones
  CSP en consola.
