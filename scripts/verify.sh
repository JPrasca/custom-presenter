#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

RUN_AUDIT=false
if [[ "${1:-}" == "--audit" ]]; then
  RUN_AUDIT=true
fi

echo "==> [1/6] Limpiando procesos testhost residuales (si existen)..."
pkill -f testhost >/dev/null 2>&1 || true

echo "==> [2/6] Verificando formato .NET..."
dotnet format CustomPresenter.sln --verify-no-changes

echo "==> [3/6] Compilando solución .NET 10..."
dotnet build CustomPresenter.sln

echo "==> [4/6] Ejecutando pruebas .NET (secuencial -m:1)..."
dotnet test CustomPresenter.sln -m:1 --no-build --logger "console;verbosity=normal"

echo "==> [5/6] Verificando frontend Vue 3 (lint, formato, tipos y Vitest)..."
npm --prefix "$ROOT_DIR/frontend" run lint
npm --prefix "$ROOT_DIR/frontend" run format:check
npm --prefix "$ROOT_DIR/frontend" run check
npm --prefix "$ROOT_DIR/frontend" test

if [[ "$RUN_AUDIT" == true ]]; then
  echo "==> [6/6] Ejecutando auditoría de vulnerabilidades (NuGet y npm)..."
  dotnet list CustomPresenter.sln package --vulnerable --include-transitive
  npm --prefix "$ROOT_DIR/frontend" audit --omit=dev
else
  echo "==> [6/6] Auditoría de red omitida (usa './scripts/verify.sh --audit' para incluirla)."
fi

echo "✅ Todas las verificaciones pasaron correctamente."
