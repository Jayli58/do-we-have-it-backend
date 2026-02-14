#!/usr/bin/env bash
set -euo pipefail

echo "[init] Creating Inventory table..."

awslocal dynamodb create-table \
  --table-name Inventory \
  --attribute-definitions \
      AttributeName=PK,AttributeType=S \
      AttributeName=SK,AttributeType=S \
      AttributeName=GSI1PK,AttributeType=S \
      AttributeName=GSI1SK,AttributeType=S \
  --key-schema \
      AttributeName=PK,KeyType=HASH \
      AttributeName=SK,KeyType=RANGE \
  --global-secondary-indexes \
      "[\
        {\
          \"IndexName\": \"GSI1\",\
          \"KeySchema\": [\
            {\"AttributeName\":\"GSI1PK\",\"KeyType\":\"HASH\"},\
            {\"AttributeName\":\"GSI1SK\",\"KeyType\":\"RANGE\"}\
          ],\
          \"Projection\": {\"ProjectionType\": \"ALL\"}\
        }\
      ]" \
  --billing-mode PAY_PER_REQUEST \
  >/dev/null 2>&1 || echo "[init] Inventory already exists"

echo "[init] Done."
