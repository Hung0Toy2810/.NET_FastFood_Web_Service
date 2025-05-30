version: "3.8"

services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-lts
    container_name: sqlserver
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=YourPassword123
    ports:
      - "1433:1433"
    networks:
      - backend
    volumes:
      - sqlserver_data:/var/opt/mssql

  minio:
    image: quay.io/minio/minio
    container_name: minio
    environment:
      - MINIO_ROOT_USER=minioadmin
      - MINIO_ROOT_PASSWORD=minioadmin
    command: server /data --console-address ":9001"
    ports:
      - "9000:9000"  # S3 API
      - "9001:9001"  # Web Console
    networks:
      - backend
    volumes:
      - minio_data:/data

  redis:
    image: redis:7.0
    container_name: redis
    command: ["redis-server", "--requirepass", "Admin"]
    environment:
      - REDIS_PASSWORD=Admin
    ports:
      - "6380:6379"
    networks:
      - backend
    volumes:
      - redis_data:/data

networks:
  backend:
    driver: bridge

volumes:
  sqlserver_data:
  minio_data:
  redis_data: