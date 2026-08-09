#!/usr/bin/env bash
# Выполняется на сервере (вручную или из GitHub Actions, см. .github/workflows/deploy.yml):
# подтягивает main и пересобирает контейнер приложения. .env рядом с этим файлом
# не коммитится и не трогается — там секреты (пароль Postgres, пароль первого админа).
set -euo pipefail
cd "$(dirname "$0")"

git fetch origin main
git reset --hard origin/main

docker compose -f docker-compose.prod.yml up -d --build
docker image prune -f
