# Configuração do InfluxDB

## Token de Acesso

Após a instalação do InfluxDB, foi gerado o seguinte token de acesso:

```
MkpRB5OIOlb9xQTZetpDE4ZCDB2hezbqlSDYNzmqMzenvRaPtxAX2iMHZAUTwhTQv8ty6yNIfJnPhlbXZPEiIA==
```

## Configuração

Este token deve ser configurado no arquivo `.env` do projeto:

```env
INFLUX_TOKEN=MkpRB5OIOlb9xQTZetpDE4ZCDB2hezbqlSDYNzmqMzenvRaPtxAX2iMHZAUTwhTQv8ty6yNIfJnPhlbXZPEiIA==
```

## Segurança

⚠️ **IMPORTANTE**: 
- Este token fornece acesso completo ao InfluxDB
- Mantenha o arquivo `.env` seguro e não o commite no repositório
- O arquivo `.env` está no `.gitignore` para evitar exposição acidental
- Em produção, considere usar um gerenciador de segredos

## Verificação

Para verificar se o token está funcionando, você pode testar a conexão:

```bash
# Via API do InfluxDB
curl --request GET \
  'http://localhost:8086/api/v2/buckets' \
  --header 'Authorization: Token MkpRB5OIOlb9xQTZetpDE4ZCDB2hezbqlSDYNzmqMzenvRaPtxAX2iMHZAUTwhTQv8ty6yNIfJnPhlbXZPEiIA=='
```

## Geração de Novo Token

Se precisar gerar um novo token:

```bash
# Via CLI do InfluxDB
influx auth create \
  --org datamais \
  --all-access
```

Ou via interface web do InfluxDB em `http://localhost:8086`
