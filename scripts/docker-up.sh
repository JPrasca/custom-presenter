#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

echo "==> Construyendo e iniciando Custom Presenter en Docker..."
docker compose up -d --build --wait

echo ""
echo "Custom Presenter listo en:"
echo "  - Operador:  http://127.0.0.1:47321/"
echo "  - Proyector: http://127.0.0.1:47321/projector"
echo "  - OBS:       http://127.0.0.1:47321/obs"
echo ""
echo "Para ver logs:  docker compose logs -f"
echo "Para detener:   ./scripts/docker-down.sh"
