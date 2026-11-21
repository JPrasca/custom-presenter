# BACKLOG — Custom Presenter (Flujo Continuo XP)

El proyecto se desarrolla en **flujo continuo XP** (entregas incrementales pequeñas, verificables y ordenadas por valor y dependencia técnica). Cada Historia de Usuario debe implementarse con TDD, revisión de seguridad y pruebas automatizadas antes de pasar a la siguiente.

> **Estándar obligatorio de pruebas (unitarias e integración):** todas las pruebas deben estructurarse explícitamente bajo el patrón **AAA** (`Arrange` / `Act` / `Assert`) y utilizar el patrón **Builder** para construir datos y escenarios de prueba de forma legible y mantenible.
>
> **Flujo de entrega por Historia (Git + CI):** cada Historia de Usuario se desarrolla en su propia rama (`feature/hu-XX-descripcion`) partiendo de `main`. Al finalizar el ciclo TDD, se ejecuta localmente `./scripts/verify.sh --audit` (equivalente local del pipeline de CI), se sube la rama y se abre un **Pull Request** hacia `main`. El merge a `main` requiere que los checks de [.github/workflows/ci.yml](file:///Users/pro/Desktop/Desk/custom-presenter/.github/workflows/ci.yml) pasen en verde.

---

## Épica 1: Núcleo de Dominio, Estado en Vivo y Salida OBS (`localhost`)
*Objetivo: Establecer la arquitectura hexagonal, el estado en vivo compartido y el micro-servidor transparente para OBS desde el inicio.*

### HU-01: Gestión del estado en vivo (*LiveStateManager*) y comandos base `[COMPLETADA]`
- **Descripción:** Como operador, quiero que exista un gestor central de estado en vivo en C# (.NET 10 LTS) para que cualquier cambio de diapositiva o estado (*Clear*, *Blackout*, *Logo*) se propague de forma determinista y sin bloqueos.
- **Criterios de Aceptación (Given-When-Then):**
  - **Given** que la aplicación inicia con un estado inicial vacío (*idle*), **When** se invoca el caso de uso `ProjectSlide` con un contenido válido, **Then** el `LiveState` se actualiza con la diapositiva activa y emite una notificación por el canal de difusión (`System.Threading.Channels` / suscriptores).
  - **Given** una diapositiva proyectada en vivo, **When** se activa `ToggleClearText`, **Then** el texto en vivo se oculta manteniendo el fondo activo y sin borrar la referencia de la diapositiva seleccionada.
  - **Given** cualquier estado en vivo, **When** se activa `ToggleBlackout`, **Then** todas las salidas reciben la bandera de pantalla negra prioritaria sobre el texto y el fondo.
- **Pruebas requeridas:**
  - Unitarias (Dominio/Aplicación - xUnit): transiciones de `LiveState` (proyectar, limpiar texto, blackout, logo) y prioridad entre banderas.
  - Integración: suscripción múltiple al broadcaster verificando recepción de eventos sin pérdida.

### HU-02: Micro-servidor local (`127.0.0.1:47321`) y vista transparente para OBS `[COMPLETADA]`
- **Descripción:** Como operador, quiero un micro-servidor HTTP/WebSocket local en ASP.NET Core Minimal APIs con una ruta `/obs` de fondo transparente real (canal alfa) para conectarlo como Fuente de Navegador en OBS Studio.
- **Criterios de Aceptación (Given-When-Then):**
  - **Given** que la aplicación está ejecutándose, **When** OBS Studio abre `http://127.0.0.1:47321/obs` (puerto fijo por defecto, configurable en ajustes si está ocupado), **Then** recibe un documento HTML válido con `background: transparent` y se conecta automáticamente al WebSocket `/ws`.
  - **Given** que el operador cambia de diapositiva en la app, **When** el `LiveStateManager` difunde el nuevo estado, **Then** la vista `/obs` actualiza el *lower third* en `< 50 ms` sin recargar la página.
  - **Given** que el texto contiene caracteres especiales HTML (`<script>`, `&`, `"`), **When** se renderiza en `/obs`, **Then** se muestra como texto literal sin ejecutar código (protección XSS).
- **Pruebas requeridas:**
  - Integración: arranque de Kestrel en `127.0.0.1`, petición `GET /obs` (HTTP 200) y conexión WebSocket `/ws` recibiendo estado inicial y actualizaciones.
  - Seguridad: verificar que el servidor solo escucha en `127.0.0.1` y que payloads con etiquetas HTML son escapados en el renderizado.

---

## Épica 2: Salida Nativa Multipantalla y Controles de Operador
*Objetivo: Habilitar la proyección presencial en pantalla secundaria sin bordes y la interfaz base del operador.*

### HU-03: Detección de monitores y ventana de Proyector Nativo sin bordes `[COMPLETADA]`
- **Descripción:** Como operador, quiero seleccionar un monitor conectado y abrir la salida presencial a pantalla completa sin bordes mediante Photino.NET para proyectar en el salón sin mostrar barras de ventana.
- **Criterios de Aceptación (Given-When-Then):**
  - **Given** que hay uno o más monitores conectados, **When** el operador consulta las pantallas disponibles, **Then** recibe la lista con nombre, resolución y posición de cada monitor.
  - **Given** un monitor seleccionado, **When** el operador activa la salida de proyector, **Then** se abre la ventana `projector` sin bordes (*chromeless / fullscreen*) cubriendo el monitor destino y recibiendo actualizaciones de estado en vivo en tiempo real.
- **Pruebas requeridas:**
  - Integración: servicio de listado de monitores y ciclo de vida (abrir/cerrar/posicionar) de la ventana `projector`.
  - Seguridad: verificar que la ventana `projector` no registra handlers IPC de escritura ni acceso al sistema de archivos.

### HU-04: Panel de Operador base (Preview, Live y Atajos de teclado) `[COMPLETADA]`
- **Descripción:** Como operador, quiero una interfaz oscura clara en Vue 3 + TypeScript con controles rápidos de vivo (*Clear*, *Blackout*, *Logo*) y navegación por teclado para operar sin distracciones.
- **Criterios de Aceptación (Given-When-Then):**
  - **Given** la ventana principal del operador abierta sin foco en campos de texto, **When** el operador presiona los botones o atajos de teclado configurados (`Flecha Abajo/Derecha`, `Esc` o `C` para Clear, `B` para Blackout), **Then** el estado en vivo cambia inmediatamente en ambas salidas (Proyector y OBS) y se refleja visualmente en el indicador *Live* del operador.
  - **Given** que el foco del operador está dentro de un `<input>`, `<textarea>` o elemento editable (por ejemplo, buscando un versículo o editando una canción), **When** el operador escribe las letras `C`, `B` o usa flechas de texto, **Then** los atajos globales de una sola tecla son ignorados para no disparar *Clear* o *Blackout* accidentalmente.
- **Pruebas requeridas:**
  - Unitarias (Frontend - Vitest): manejo de atajos de teclado, exclusión cuando el foco está en elementos editables y actualización del store reactivo en Vue 3.
  - Integración: flujo completo comando IPC → `LiveStateManager` → actualización de vista.

---

## Épica 3: Gestión y Proyección de Canciones
*Objetivo: Permitir crear, importar, buscar y proyectar letras de canciones divididas por estrofas.*

### HU-05: CRUD de Canciones y división en diapositivas `[COMPLETADA]`
- **Descripción:** Como operador, quiero crear, editar y eliminar canciones en SQLite (`Microsoft.Data.Sqlite`) dividiendo la letra en estrofas para proyectarlas secuencialmente.
- **Criterios de Aceptación (Given-When-Then):**
  - **Given** un formulario de canción con título y texto separado por líneas en blanco, **When** se guarda la canción, **Then** el dominio la divide en diapositivas ordenadas y la persiste en SQLite.
  - **Given** una canción guardada, **When** se edita o elimina, **Then** los cambios se reflejan de inmediato en la biblioteca local.
- **Pruebas requeridas:**
  - Unitarias: algoritmo de división de estrofas en diapositivas (manejo de líneas vacías múltiples, espacios y límites).
  - Integración: CRUD completo contra `Microsoft.Data.Sqlite` en memoria/temporal.

### HU-06: Búsqueda instantánea FTS5 e importación de canciones (TXT/JSON) `[COMPLETADA]`
- **Descripción:** Como operador, quiero buscar canciones por título o fragmento de letra en milisegundos e importar canciones desde archivos `.txt` o `.json`.
- **Criterios de Aceptación (Given-When-Then):**
  - **Given** una biblioteca con canciones indexadas en SQLite FTS5, **When** el operador escribe palabras clave del título o coro, **Then** obtiene los resultados ordenados en `< 30 ms`.
  - **Given** un archivo `.txt` o `.json` válido `< 50 MB`, **When** se importa, **Then** las canciones se validan y guardan en SQLite; si el archivo excede el límite o tiene formato inválido, se rechaza con un mensaje claro.
- **Pruebas requeridas:**
  - Integración: búsqueda FTS5 con acentos/mayúsculas y caso de importación válida.
  - Seguridad: rechazo de archivos que superen el límite de tamaño o contengan codificación inválida.

---

## Épica 4: Biblias y Textos Predefinidos (Anuncios)
*Objetivo: Incorporar búsqueda rápida de pasajes bíblicos y bloques de anuncios listos para proyectar.*

### HU-07: Importación de Biblias (XML OpenSong/Zefania o JSON) `[COMPLETADA]`
- **Descripción:** Como operador, quiero importar archivos de Biblia locales hacia SQLite para no depender de internet durante los eventos.
- **Criterios de Aceptación (Given-When-Then):**
  - **Given** un archivo XML (OpenSong/Zefania) o JSON válido `< 50 MB`, **When** se ejecuta la importación, **Then** todos los libros, capítulos y versículos se almacenan en SQLite e indexan en FTS5 en una única transacción.
  - **Given** un archivo XML malicioso con entidades externas (XXE), **When** se intenta importar, **Then** `XmlReader` (`DtdProcessing.Prohibit`, `XmlResolver = null`) rechaza el archivo de forma segura sin resolver entidades externas.
- **Pruebas requeridas:**
  - Unitarias/Integración: parsing e inserción transaccional de un fragmento de Biblia de prueba.
  - Seguridad: prueba con payload XXE / *Billion Laughs* verificando que es rechazado sin expansión ni consumo excesivo de memoria.

### HU-08: Navegador y buscador de Biblia (Referencia + Texto)
- **Descripción:** Como operador, quiero buscar versículos escribiendo una referencia (ej. `Juan 3:16`) o palabras clave y proyectarlos en un clic.
- **Criterios de Aceptación (Given-When-Then):**
  - **Given** al menos una versión de Biblia importada, **When** el operador escribe `Salmos 23:1` o selecciona Libro/Capítulo/Versículo, **Then** se muestra el versículo exacto listo para proyectar en Proyector y OBS (incluyendo la cita como pie o encabezado).
  - **Given** múltiples versiones importadas, **When** el operador cambia de versión activa, **Then** la búsqueda y proyección usan la nueva versión seleccionada.
- **Pruebas requeridas:**
  - Unitarias: parser de referencias bíblicas (abreviaturas, rangos como `1-3`, acentos).
  - Integración: consulta por referencia exacta y búsqueda FTS5 en SQLite.

### HU-09: Gestión y proyección de Anuncios / Textos predefinidos
- **Descripción:** Como operador, quiero crear y guardar textos libres o anuncios recurrentes (ej. bienvenida, horarios, avisos) para proyectarlos rápidamente durante el evento.
- **Criterios de Aceptación (Given-When-Then):**
  - **Given** un título y uno o más bloques de texto, **When** el operador guarda el anuncio, **Then** queda persistido en SQLite y disponible para agregarse a la playlist o proyectarse directamente.
- **Pruebas requeridas:**
  - Integración: CRUD de anuncios en SQLite y proyección hacia `LiveStateManager`.

---

## Épica 5: Lista de Servicio (Playlist), Temas y Multimedia
*Objetivo: Orquestar un evento completo de principio a fin con estilos visuales propios y fondos multimedia.*

### HU-10: Lista de Servicio (Playlist) mixta y persistente
- **Descripción:** Como operador, quiero armar una lista ordenada con canciones, pasajes bíblicos y anuncios para recorrer el orden del evento sin buscar cada ítem en vivo.
- **Criterios de Aceptación (Given-When-Then):**
  - **Given** ítems de canciones, versículos y anuncios, **When** el operador los añade a la playlist, los reordena y guarda la lista, **Then** la secuencia persiste en SQLite y permite avanzar secuencialmente de un ítem al siguiente.
- **Pruebas requeridas:**
  - Integración: creación, reordenamiento y carga de playlist con referencias heterogéneas en SQLite.

### HU-11: Temas visuales independientes (Proyector vs. OBS Lower Third)
- **Descripción:** Como operador, quiero personalizar por separado el estilo del proyector (fuente, tamaño, alineación, color) y el del overlay de OBS (posición inferior, caja semitransparente, márgenes) para que ambos se vean profesionales.
- **Criterios de Aceptación (Given-When-Then):**
  - **Given** un panel de configuración de temas, **When** el operador modifica tamaño de fuente, color o posición del *lower third* y guarda, **Then** tanto el Proyector Nativo como la vista `/obs` actualizan su estilo en tiempo real sin reiniciar.
- **Pruebas requeridas:**
  - Unitarias: validación estricta de colores hexadecimales, rangos numéricos de fuente y posiciones permitidas.
  - Integración: persistencia de temas en SQLite y propagación en `LiveState`.

### HU-12: Biblioteca local de fondos multimedia (Imagen y Video)
- **Descripción:** Como operador, quiero importar imágenes (PNG/JPG/WebP) y videos cortos (MP4/WebM) como fondos del proyector de forma segura y sin saturar OBS.
- **Criterios de Aceptación (Given-When-Then):**
  - **Given** un archivo multimedia válido dentro del límite de tamaño, **When** el operador lo importa, **Then** se validan sus *magic bytes*, se copia con nombre UUID al directorio interno `media/` y queda disponible como fondo del Proyector Nativo.
  - **Given** una petición HTTP `GET /media/../../etc/passwd` al micro-servidor local, **When** se procesa el parámetro, **Then** es rechazada con HTTP `400/404` sin acceder fuera del directorio `media/`.
- **Pruebas requeridas:**
  - Integración: importación de imagen/video válido y asignación como fondo de diapositiva.
  - Seguridad: pruebas de *Path Traversal*, archivo con extensión falsa (magic bytes inválidos) y archivo que exceda el tamaño máximo.

### HU-13: Respaldo local (Exportación / Importación JSON)
- **Descripción:** Como operador, quiero exportar mis canciones y anuncios en un archivo JSON para respaldarlos o migrarlos de mi Mac de pruebas al equipo Windows del salón.
- **Criterios de Aceptación (Given-When-Then):**
  - **Given** una biblioteca con canciones y anuncios, **When** el operador exporta el respaldo e importa ese archivo en una instancia limpia, **Then** todas las canciones y anuncios se restauran sin duplicados ni pérdida de formato.
- **Pruebas requeridas:**
  - Integración: ciclo completo de exportación → importación en BD limpia y validación de esquema JSON.
