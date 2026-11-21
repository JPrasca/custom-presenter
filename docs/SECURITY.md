# SECURITY — Custom Presenter

## 1. Alcance y Postura de Seguridad

Aunque **Custom Presenter** es una aplicación 100 % local y sin autenticación de usuarios en la nube, expone:
1. Un **micro-servidor HTTP/WebSocket local** (ASP.NET Core Minimal APIs) para OBS Studio.
2. **Importación y lectura de archivos externos** (Biblias XML/JSON, canciones TXT/JSON, imágenes y videos).
3. **Renderizado HTML en múltiples WebViews** (Operador, Proyector en Photino.NET y OBS Browser Source).

La postura de seguridad se basa en **mínimo privilegio**, **defensa en profundidad** y **aislamiento estricto a `localhost`**.

---

## 2. Modelo de Amenazas STRIDE Simplificado

| Categoría | Amenaza Identificada | Riesgo | Mitigación en el Diseño |
| :--- | :--- | :--- | :--- |
| **S — Spoofing** | Procesos o dispositivos en la red local conectándose al micro-servidor para inyectar contenido o leer datos. | Medio | Enlace estricto de Kestrel a `127.0.0.1` (`IPAddress.Loopback` únicamente; nunca `0.0.0.0` ni `+` en el MVP). |
| **T — Tampering** | Manipulación maliciosa de archivos de Biblia/Canciones importados (XSS en letras o XML bombs/XXE) o alteración de rutas de archivos (*Path Traversal*). | Alto | Sanitización/escape estricto de texto antes de renderizar (prohibido `v-html` en Vue 3 con entrada de usuario), `XmlReader` con `DtdProcessing.Prohibit` y `XmlResolver = null`, y validación canónica de rutas. |
| **R — Repudiation** | Fallos silenciosos durante un evento en vivo sin rastro de qué acción o archivo causó el error. | Bajo | Registro local estructurado (*logging* con `ILogger` a archivo local rotativo) de errores de importación, fallos de WebSocket y cambios críticos de estado. |
| **I — Information Disclosure** | Exposición de archivos arbitrarios del sistema de archivos del usuario a través del endpoint `/media/{id}` del micro-servidor local. | Alto | Lista blanca de extensiones, resolución por ID opaco (`Guid`) mapeado en SQLite en lugar de rutas directas del sistema de archivos, y validación `Path.GetFullPath()` dentro del directorio `media/` de la app. |
| **D — Denial of Service** | Importación de archivos multimedia o XML gigantes que agoten la RAM/CPU y tumben OBS en plena transmisión. | Alto | Límites estrictos de tamaño de archivo al importar (ej. XML/JSON `< 50 MB`, imágenes `< 20 MB`, videos `< 500 MB`) y validación de estructura antes de procesar. |
| **E — Elevation of Privilege** | Ejecución de código arbitrario vía XSS en WebView con acceso a comandos nativos de Photino.NET. | Alto | Content Security Policy (CSP) estricta en respuestas HTTP/HTML, y aislamiento del puente IPC (`WebMessageReceived`): la ventana `projector` no registra handlers de escritura, borrado ni acceso a sistema de archivos. |

---

## 3. Controles de Sanitización de Entradas y Archivos

### 3.1 Prevención de XSS en Proyector y OBS
- Todo texto proveniente de canciones, biblias o anuncios se renderiza exclusivamente mediante interpolación segura de Vue 3 (`{{ slide.text }}`), **prohibiendo** el uso de `v-html`.
- Los saltos de línea se renderizan mediante CSS (`white-space: pre-line;`) o división segura en nodos de texto.
- Las configuraciones de tema (colores, tamaños, fuentes) se validan contra esquemas estrictos en C# (ej. colores hexadecimales `^#[0-9a-fA-F]{6,8}$`, rangos numéricos acotados) antes de aplicarse como variables CSS.

### 3.2 Seguridad en Importación de Archivos
- **Biblias y Canciones (XML/JSON/TXT):**
  - Tamaño máximo permitido por archivo: `50 MB`.
  - Uso de `XmlReader` en .NET configurado explícitamente con `DtdProcessing = DtdProcessing.Prohibit`, `XmlResolver = null` y `MaxCharactersInDocument` acotado, mitigando XXE y *Billion Laughs*.
  - Validación UTF-8 estricta y rechazo de campos con longitud anómala.
- **Multimedia (Imágenes y Videos):**
  - Validación combinada de **extensión** y **magic bytes** (cabecera binaria real del archivo) en C# para aceptar únicamente `PNG`, `JPEG`, `WebP`, `MP4` y `WebM`.
  - Los archivos importados se copian al directorio interno gestionado por la aplicación con un nombre generado (`<guid>.<ext>`), eliminando cualquier carácter especial o ruta del nombre original.

### 3.3 Prevención de Path Traversal
- El endpoint `GET /media/{id}` de ASP.NET Core Minimal APIs solo acepta un **`Guid` válido** (restricción de ruta `{id:guid}`).
- Busca el archivo únicamente dentro del directorio `AppData/media/`, verifica que `Path.GetFullPath(targetPath)` comience exactamente con `Path.GetFullPath(mediaRoot)` (comparación ordinal) y rechaza enlaces simbólicos fuera de él.

---

## 4. Seguridad Local, Permisos y Dependencias

### 4.1 Hardening de Photino.NET y ASP.NET Core
- **Puente IPC segmentado:**
  - Ventana `operator`: suscrita a comandos de gestión de biblioteca, playlist, importación y estado en vivo.
  - Ventana `projector`: **solo recepción** de mensajes de estado (`SendWebMessage`); no procesa comandos destructivos ni diálogos de sistema desde WebView.
- **CSP (Content Security Policy):** Deshabilitar `eval()`, scripts inline no autorizados y conexiones salientes (`connect-src 'self' http://127.0.0.1:47321 ws://127.0.0.1:47321`).
- **Consultas SQL parametrizadas:** Todas las consultas a `Microsoft.Data.Sqlite` usan parámetros enlazados (`@param`), prohibiendo la concatenación de cadenas SQL.
- **Sin secretos en disco:** No se almacenan credenciales ni tokens externos.

### 4.2 Auditoría de Dependencias y SAST (DevSecOps)
En la fase de bootstrap (`minidev-ops`) y durante todo el desarrollo se integrarán de forma obligatoria:
- **Backend (.NET 10 LTS / C#):**
  - Analizadores Roslyn nativos con `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` y `<AnalysisMode>All</AnalysisMode>` (o `Recommended`) en `Directory.Build.props`.
  - `dotnet format --verify-no-changes` (formato estricto).
  - `dotnet list package --vulnerable --include-transitive` (auditoría de vulnerabilidades NuGet).
- **Frontend (Vue 3 / TypeScript):**
  - `eslint` con `eslint-plugin-vue`, `typescript-eslint` y `eslint-plugin-security`.
  - `vue-tsc --noEmit` con TypeScript estricto (`strict: true`).
  - `npm audit` para dependencias de Node.
- **Prohibiciones en código:** Prohibido `unsafe` en código propio de C# (`AllowUnsafeBlocks = false`), prohibido `v-html` en Vue y prohibido SQL dinámico sin parametrizar.
