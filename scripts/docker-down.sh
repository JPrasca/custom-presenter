#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if [[ "${1:-}" == "--clean" ]]; then
  echo "==> Deteniendo Custom Presenter y eliminando volumen de datos..."
  docker compose down -v
else
  echo "==> Deteniendo Custom Presenter (conservando datos en volumen)..."
  docker compose down
fi
