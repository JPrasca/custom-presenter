# PRD — Custom Presenter (Proyector de Letras, Biblia y Anuncios para Vivo + OBS)

## 1. Visión del producto

**Custom Presenter** es una aplicación de escritorio multiplataforma (macOS, Windows y Linux), ligera y 100 % local, diseñada para la proyección en vivo de letras de canciones, pasajes bíblicos, anuncios predefinidos y fondos multimedia en eventos presenciales, funcionando simultáneamente en la misma máquina que **OBS Studio** sin comprometer los recursos de codificación ni la estabilidad de la transmisión.

### Propuesta de valor
- **Ultra ligera:** basada en Tauri (Rust) + Svelte (TypeScript), evitando la sobrecarga de memoria y CPU de runtimes pesados como JVM o Electron.
- **Doble salida nativa sincronizada:**
  1. **Salida presencial:** ventana nativa a pantalla completa sin bordes dirigida al monitor/proyector del salón.
  2. **Salida OBS (Overlay):** micro-servidor HTTP/WebSocket en `127.0.0.1` (`localhost`) que expone vistas web con fondo transparente real (canal alfa) para conectarse como **Fuente de Navegador** en OBS Studio, ideal para *lower thirds* / subtítulos y compatible con entornos restrictivos como Wayland en Linux.
- **100 % offline y determinista:** sin dependencias de la nube, con búsqueda instantánea local y operación optimizada para teclado.

---

## 2. Usuarios y contexto de uso

- **Usuario principal (Operador de multimedia):** persona encargada de operar la proyección y OBS durante un evento en vivo. Necesita cambiar de estrofa, buscar un versículo o lanzar un anuncio en menos de un segundo, con retroalimentación visual clara de lo que está en vivo (*Live*) y lo que está en vista previa (*Preview*).
- **Entorno de validación y despliegue:**
  - Validación inicial de desarrollo: **macOS**.
  - Entorno principal de producción en vivo: **Windows** (16 GB RAM, resolución Full HD 1920x1080 @ 60 fps).
  - Compatibilidad multiplataforma mantenida para **Linux** (X11/Wayland, WebKitGTK/GStreamer).

---

## 3. Alcance del MVP

### 3.1 Incluido en el MVP
1. **Gestión y proyección de Canciones:** CRUD de canciones con título, autor/tonalidad opcional y letra estructurada en diapositivas/estrofas (ej. Verso, Coro, Puente), más búsqueda instantánea por título o fragmento de letra.
2. **Búsqueda y proyección de Biblia:** importación local de versiones bíblicas (formato XML OpenSong/Zefania o JSON estructurado), navegación rápida por Libro → Capítulo → Versículo y búsqueda de texto completo (FTS).
3. **Anuncios / Textos predefinidos:** creación y guardado de diapositivas o bloques de texto reutilizables (avisos, bienvenidas, datos de contacto, lecturas rápidas) listos para proyectar con un clic.
4. **Lista de servicio (Playlist):** armado de una secuencia ordenada mezclando canciones, pasajes bíblicos y anuncios para un evento específico, con persistencia local.
5. **Doble salida simultánea y sincronizada:**
   - Control de **Salida Nativa** (selección de monitor/pantalla destino, ventana sin bordes a pantalla completa).
   - **Micro-servidor local (`127.0.0.1`)** para **OBS Browser Source** con vista transparente (canal alfa) configurable como *lower third* / subtítulos.
6. **Controles rápidos de vivo (*Live*):**
   - Navegación por teclado (flechas, `Enter`, `Esc`, atajos configurables).
   - Estados globales instantáneos: **Limpiar texto** (deja solo el fondo en el proyector y oculta el *lower third* en OBS), **Pantalla negra** y **Mostrar Logo / Fondo de reposo**.
7. **Temas y multimedia básica:**
   - Configuración de estilos separados o vinculados para **Proyector** (tipografía, tamaño, alineación, sombra/contorno, color o imagen/video de fondo) y **OBS Overlay** (posición inferior/superior/centro, fondo semitransparente o caja de texto, canal alfa real).
   - Biblioteca local de fondos (colores sólidos, imágenes PNG/JPG/WebP y videos en bucle MP4/WebM).

### 3.2 Exclusiones explícitas del MVP
Quedan fuera de esta primera versión para proteger la simplicidad, estabilidad y tiempo de entrega:
- Control remoto desde dispositivos móviles o red local (LAN).
- Vista de retorno / escenario (*Stage Monitor*) independiente con reloj o siguiente estrofa para músicos.
- Sincronización en la nube, cuentas de usuario o colaboración multi-operador en tiempo real.
- Salida por protocolo NDI o captura de video entrante (cámaras).
- Automatización externa por MIDI, OSC o control de luces DMX.
- Editor avanzado de video o transiciones 3D complejas.
- Descarga automática de biblias desde internet (la importación será desde archivos locales).

---

## 4. Requerimientos Funcionales (RF)

| ID | Prioridad | Requerimiento | Descripción |
| :--- | :--- | :--- | :--- |
| **RF-01** | Alta | **Gestión de Canciones** | Crear, editar, eliminar y listar canciones con estrofas delimitadas. Permitir importar canciones desde archivos de texto plano (`.txt`) o JSON. |
| **RF-02** | Alta | **Búsqueda instantánea de Canciones y Biblias** | Filtrar canciones por título/letra y versículos por referencia (`Juan 3:16`) o palabras clave en `< 30 ms`. |
| **RF-03** | Alta | **Importación y lectura de Biblias** | Importar al menos 1–5 versiones bíblicas locales (XML OpenSong/Zefania o JSON) hacia la base de datos SQLite local y alternar entre versiones al proyectar. |
| **RF-04** | Alta | **Textos y Anuncios predefinidos** | Crear, editar, guardar y proyectar bloques de texto libre (ej. anuncios de la semana) con soporte para múltiples líneas o diapositivas. |
| **RF-05** | Alta | **Lista de servicio (Playlist)** | Crear listas de reproducción ordenadas combinando canciones, versículos/pasajes y anuncios; permitir reordenar y guardar/cargar listas. |
| **RF-06** | Alta | **Salida Nativa Multipantalla** | Detectar monitores conectados y abrir/cerrar una ventana sin bordes (*borderless fullscreen*) en la pantalla seleccionada, manteniendo el foco de control en la ventana principal del operador. |
| **RF-07** | Alta | **Micro-servidor local para OBS** | Levantar un servidor HTTP + WebSocket en `127.0.0.1` (puerto fijo `47321` por defecto, configurable si está ocupado) que sirva una ruta dedicada (ej. `/obs`) con fondo `transparent` real (canal alfa) y actualización en tiempo real al cambiar diapositivas. |
| **RF-08** | Alta | **Controles de estado en vivo** | Botones y atajos de teclado para: proyectar diapositiva seleccionada, siguiente/anterior diapositiva, **Limpiar texto** (*Clear Text*), **Pantalla negra** (*Blackout*) y **Logo/Reposo**. |
| **RF-09** | Media | **Temas y estilos independientes** | Configurar estilo visual para Proyector (fuente, tamaño, color, contorno/sombra, fondo) y estilo de *Lower Third* para OBS (posición, márgenes, caja semitransparente opcional, animación suave de entrada/salida). |
| **RF-10** | Media | **Gestión de fondos multimedia** | Importar imágenes (PNG, JPG, WebP) y videos cortos en bucle (MP4 H.264 / WebM VP9) a una carpeta local gestionada y asignarlos como fondo del proyector. |
| **RF-11** | Baja | **Exportación/Respaldo local** | Exportar e importar la biblioteca de canciones y anuncios en un archivo JSON de respaldo. |

---

## 5. Requerimientos No Funcionales (RNF)

### 5.1 Rendimiento y consumo de recursos
- **RNF-01 (RAM):** Consumo total de memoria en reposo o proyectando texto/imágenes `< 150 MB` RAM (sumando proceso Rust y WebViews).
- **RNF-02 (CPU):** Consumo de CPU en reposo `< 2 %` y durante cambios de diapositiva `< 5 %` en hardware objetivo (16 GB RAM, CPU moderna de 4+ núcleos) para no competir con el codificador de OBS.
- **RNF-03 (Latencia de sincronización):** El tiempo desde que el operador presiona una tecla/clic hasta que el cambio se refleja en la Salida Nativa y en el Browser Source de OBS debe ser `< 50 ms`.
- **RNF-04 (Búsqueda):** Consultas de texto completo sobre ~155,000 versículos (5 biblias) y ~5,000 canciones deben resolverse en `< 30 ms` usando SQLite FTS5.
- **RNF-05 (Fluidez de video):** La salida en proyector y OBS debe renderizar a 60 fps estables en 1920x1080 (Full HD) sin provocar *dropped frames* en OBS.

### 5.2 Portabilidad y compatibilidad
- **RNF-06 (Multiplataforma):** Compilación y ejecución nativa en **Windows 10/11**, **macOS** (Apple Silicon e Intel) y **Linux** (X11 y Wayland).
- **RNF-07 (Códecs multimedia):** Soporte prioritario para contenedores/códecs compatibles de forma nativa con WebView2 (Windows), WKWebView (macOS) y WebKitGTK/GStreamer (Linux): imágenes PNG/JPG/WebP y video MP4 (H.264) / WebM (VP9).

### 5.3 Usabilidad
- **RNF-08 (Operación por teclado):** Todas las acciones críticas de vivo (navegar diapositivas, limpiar texto, blackout, buscar) deben ser accesibles sin usar el ratón.
- **RNF-09 (Claridad visual):** Interfaz oscura (*dark mode*) por defecto para no encandilar en cabinas de multimedia, con distinción inequívoca entre *Preview* y *Live*.

### 5.4 Mantenibilidad y observabilidad
- **RNF-10 (Arquitectura desacoplada):** Lógica de dominio e infraestructura en Rust separada estrictamente bajo Clean Architecture / Arquitectura Hexagonal.
- **RNF-11 (Logs locales):** Registro estructurado de eventos y errores en archivo local rotativo (sin telemetría externa) para diagnosticar fallos de servidor local, multimedia o base de datos.

---

## 6. Criterios de Éxito del MVP

El MVP se considerará listo para uso en producción cuando supere la siguiente prueba de aceptación integral:
1. **Prueba de evento en vivo simulado (30–60 minutos):** Ejecutar la aplicación junto a **OBS Studio** grabando/transmitiendo a `1920x1080 @ 60 fps` en Windows (y validado previamente en macOS).
2. Durante la prueba se debe proyectar una playlist completa con al menos **3 canciones**, **5 pasajes bíblicos** y **2 anuncios predefinidos**, alternando fondos de imagen/video y usando *Clear Text* y *Blackout*.
3. **Resultado esperado:**
   - Cero pérdidas de fotogramas (*dropped frames*) atribuibles a la app en OBS.
   - Transparencia real (canal alfa) funcionando en la Fuente de Navegador de OBS sin fondo sólido indeseado.
   - Salida a pantalla completa sin bordes estable en el monitor secundario.
   - Cero bloqueos o cierres inesperados.
