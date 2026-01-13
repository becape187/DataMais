# Guia de Instalação - DataMais

## Configuração do Arquivo de Ambiente

O arquivo de configuração está localizado em:
```
/home/becape/datamais.env
```

### Estrutura do Arquivo

O arquivo `datamais.env` deve conter as seguintes variáveis:

```env
# PostgreSQL Configuration
POSTGRES_HOST=localhost
POSTGRES_PORT=5432
POSTGRES_DATABASE=datamais
POSTGRES_USER=postgres
POSTGRES_PASSWORD=sua_senha_aqui

# InfluxDB Configuration
INFLUX_URL=http://localhost:8086
INFLUX_TOKEN=MkpRB5OIOlb9xQTZetpDE4ZCDB2hezbqlSDYNzmqMzenvRaPtxAX2iMHZAUTwhTQv8ty6yNIfJnPhlbXZPEiIA==
INFLUX_ORG=datamais
INFLUX_BUCKET=sensores

# Modbus Configuration
MODBUS_TIMEOUT_MS=5000
MODBUS_RETRY_COUNT=3
MODBUS_POOLING_INTERVAL_MS=100
```

## Instalação do Serviço Systemd

### 1. Copiar o arquivo de serviço

```bash
sudo cp datamais.service /etc/systemd/system/
```

### 2. Ajustar permissões (se necessário)

O serviço está configurado para rodar como `root` e ler o arquivo de configuração de `/home/becape/datamais.env`.

Se preferir usar um usuário específico, edite o arquivo `/etc/systemd/system/datamais.service`:

```ini
User=datamais
Group=datamais
```

E ajuste as permissões:

```bash
sudo chown datamais:datamais /opt/datamais
sudo chmod 600 /home/becape/datamais.env
```

### 3. Recarregar systemd

```bash
sudo systemctl daemon-reload
```

### 4. Habilitar o serviço para iniciar no boot

```bash
sudo systemctl enable datamais.service
```

### 5. Iniciar o serviço

```bash
sudo systemctl start datamais.service
```

### 6. Verificar status

```bash
sudo systemctl status datamais.service
```

### 7. Ver logs

```bash
# Logs em tempo real
sudo journalctl -u datamais.service -f

# Últimas 100 linhas
sudo journalctl -u datamais.service -n 100
```

## Comandos Úteis

```bash
# Parar o serviço
sudo systemctl stop datamais.service

# Reiniciar o serviço
sudo systemctl restart datamais.service

# Desabilitar inicialização automática
sudo systemctl disable datamais.service

# Verificar se está rodando
sudo systemctl is-active datamais.service
```

## Configuração do Caminho do .env

O serviço está configurado para ler o arquivo de configuração de:
```
/home/becape/datamais.env
```

Se precisar usar um caminho diferente, você pode:

1. **Editar o arquivo de serviço** (`/etc/systemd/system/datamais.service`):
   ```ini
   EnvironmentFile=/caminho/alternativo/datamais.env
   ```

2. **Ou definir variável de ambiente**:
   ```ini
   Environment=DATAMAIS_ENV_FILE=/caminho/alternativo/datamais.env
   ```

3. **Recarregar e reiniciar**:
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl restart datamais.service
   ```

## Verificação

Após iniciar o serviço, verifique se a API está respondendo:

```bash
# Verificar se a porta está aberta
sudo netstat -tlnp | grep dotnet

# Testar endpoint (se Swagger estiver habilitado)
curl http://localhost:5000/swagger
```

## Troubleshooting

### Serviço não inicia

1. Verificar logs:
   ```bash
   sudo journalctl -u datamais.service -n 50
   ```

2. Verificar se o arquivo .env existe e tem permissões corretas:
   ```bash
   ls -la /home/becape/datamais.env
   ```

3. Verificar se o executável existe:
   ```bash
   ls -la /opt/datamais/DataMais.dll
   ```

### Erro de conexão com banco de dados

1. Verificar se o PostgreSQL está rodando:
   ```bash
   sudo systemctl status postgresql
   ```

2. Testar conexão:
   ```bash
   psql -h localhost -U postgres -d datamais
   ```

3. Verificar credenciais no arquivo `datamais.env`

### Erro de conexão com InfluxDB

1. Verificar se o InfluxDB está rodando:
   ```bash
   sudo systemctl status influxdb
   ```

2. Testar conexão:
   ```bash
   curl http://localhost:8086/health
   ```

3. Verificar token no arquivo `datamais.env`
