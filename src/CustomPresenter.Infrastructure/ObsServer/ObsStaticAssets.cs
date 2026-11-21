namespace CustomPresenter.Infrastructure.ObsServer;

internal static class ObsStaticAssets
{
    public const string IndexHtml = """
        <!doctype html>
        <html lang="es">
          <head>
            <meta charset="UTF-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1.0" />
            <title>Custom Presenter — OBS Overlay</title>
            <link rel="stylesheet" href="/obs/app.css" />
          </head>
          <body>
            <main id="overlay-root" class="obs-overlay" aria-live="polite">
              <div id="lower-third" class="lower-third hidden">
                <p id="slide-text" class="slide-text"></p>
                <p id="slide-reference" class="slide-reference hidden"></p>
              </div>
            </main>
            <script src="/obs/app.js" defer></script>
          </body>
        </html>
        """;

    public const string AppCss = """
        * {
          box-sizing: border-box;
          margin: 0;
          padding: 0;
        }

        html,
        body {
          width: 100vw;
          height: 100vh;
          overflow: hidden;
          background: transparent !important;
          font-family: system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
        }

        .obs-overlay {
          width: 100vw;
          height: 100vh;
          display: flex;
          align-items: flex-end;
          justify-content: center;
          padding: 3rem 4rem;
          background: transparent;
        }

        .lower-third {
          max-width: 85vw;
          background: rgba(15, 17, 21, 0.82);
          color: #ffffff;
          padding: 1.1rem 2.2rem;
          border-radius: 0.6rem;
          text-align: center;
          transition: opacity 120ms ease-in-out;
        }

        .lower-third.hidden {
          opacity: 0;
          pointer-events: none;
        }

        .slide-text {
          white-space: pre-line;
          font-size: 2.1rem;
          font-weight: 600;
          line-height: 1.3;
        }

        .slide-reference {
          margin-top: 0.4rem;
          font-size: 1.1rem;
          color: #d1d5db;
        }

        .slide-reference.hidden {
          display: none;
        }
        """;

    public const string AppJs = """
        (() => {
          const container = document.getElementById("lower-third");
          const textEl = document.getElementById("slide-text");
          const refEl = document.getElementById("slide-reference");

          function render(state) {
            if (!container || !textEl || !refEl) {
              return;
            }

            const visible = Boolean(state && state.isTextVisible && state.currentSlide);
            if (!visible) {
              container.classList.add("hidden");
              textEl.textContent = "";
              refEl.textContent = "";
              refEl.classList.add("hidden");
              return;
            }

            textEl.textContent = state.currentSlide.text || "";
            if (state.currentSlide.reference) {
              refEl.textContent = state.currentSlide.reference;
              refEl.classList.remove("hidden");
            } else {
              refEl.textContent = "";
              refEl.classList.add("hidden");
            }
            container.classList.remove("hidden");
          }

          function connect() {
            const protocol = window.location.protocol === "https:" ? "wss:" : "ws:";
            const socket = new WebSocket(`${protocol}//${window.location.host}/ws`);

            socket.onmessage = (event) => {
              try {
                const payload = JSON.parse(event.data);
                render(payload);
              } catch {
                // Ignora mensajes malformados sin afectar el overlay.
              }
            };

            socket.onclose = () => {
              window.setTimeout(connect, 1500);
            };
          }

          connect();
        })();
        """;
}
