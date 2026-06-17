#!/bin/sh
set -eu

connection_string="${ConnectionStrings__paymentdb:-}"

if [ -z "$connection_string" ]; then
  echo "ConnectionStrings__paymentdb was not provided. PaymentDB datasource cannot be provisioned." >&2
  exit 1
fi

extract_value() {
  key="$1"
  printf '%s' "$connection_string" | tr ';' '\n' | awk -F= -v key="$key" '$1 == key { print substr($0, length(key) + 2) }'
}

host="$(extract_value Host)"
port="$(extract_value Port)"
database="$(extract_value Database)"
username="$(extract_value Username)"
password="$(extract_value Password)"

if [ "$host" = "localhost" ] || [ "$host" = "127.0.0.1" ]; then
  host="host.docker.internal"
fi

if [ -z "$port" ]; then
  port="5432"
fi

cat > /etc/grafana/provisioning/datasources/paymentdb-runtime.yml <<EOF
apiVersion: 1

datasources:
  - name: PaymentDB
    uid: paymentdb
    type: postgres
    access: proxy
    url: ${host}:${port}
    user: ${username}
    secureJsonData:
      password: ${password}
    jsonData:
      database: ${database}
      sslmode: disable
      postgresVersion: 1500
      timescaledb: false
EOF
