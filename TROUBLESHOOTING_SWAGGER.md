# Troubleshooting - Swagger 404

## Problema
Erro 404 ao acessar `https://modec.automais.cloud/swagger`

## Diagnóstico Rápido

Se `curl http://localhost:5000/swagger/v1/swagger.json` retorna 404, o problema é que **o código no servidor não tem Swagger habilitado** (versão antiga).

**Solução**: Fazer deploy novamente do código atualizado que tem Swagger habilitado.

## Verificações

### 1. Verificar se a API está rodando

```bash
# No servidor, verificar status do serviço
sudo systemctl status datamais.service

# Verificar se está escutando na porta 5000
sudo netstat -tlnp | grep 5000
# Deve mostrar: 0.0.0.0:5000 ou 127.0.0.1:5000
```

### 2. Testar Swagger diretamente na API (sem nginx)

```bash
# No servidor, testar diretamente
curl http://localhost:5000/swagger/v1/swagger.json

# Se funcionar, a API está OK. O problema é no nginx.
# Se não funcionar, verificar logs da API:
sudo journalctl -u datamais.service -f
```

### 3. Verificar configuração do nginx

```bash
# Verificar se a configuração foi aplicada
sudo cat /etc/nginx/sites-available/modec | grep -A 10 "location /swagger"

# Verificar se o nginx está usando a configuração correta
sudo nginx -t

# Ver logs do nginx em caso de erro
sudo tail -f /var/log/nginx/error.log
```

### 4. Verificar se o nginx está fazendo proxy corretamente

```bash
# Testar proxy do nginx localmente
curl -H "Host: modec.automais.cloud" http://localhost/swagger/v1/swagger.json

# Ou testar via HTTPS (se tiver certificado local)
curl -k https://localhost/swagger/v1/swagger.json
```

## Solução

### Passo 1: Aplicar configuração do nginx

```bash
# 1. Fazer backup
sudo cp /etc/nginx/sites-available/modec /etc/nginx/sites-available/modec.backup

# 2. Editar configuração
sudo nano /etc/nginx/sites-available/modec

# 3. Garantir que o bloco location /swagger está assim:
location /swagger {
    proxy_pass http://127.0.0.1:5000/swagger;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Forwarded-Host $host;
    proxy_set_header X-Forwarded-Port $server_port;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_connect_timeout 60s;
    proxy_send_timeout 60s;
    proxy_read_timeout 60s;
    proxy_cache_bypass $http_upgrade;
    proxy_no_cache $http_upgrade;
}

# 4. Testar configuração
sudo nginx -t

# 5. Se OK, recarregar
sudo systemctl reload nginx
```

### Passo 2: Verificar ordem das location blocks

A ordem é CRÍTICA no nginx. Deve ser:

1. `location /swagger` - primeiro
2. `location /api` - segundo  
3. `location ~* \.(js|css|...)$` - terceiro (assets)
4. `location /` - por último

### Passo 3: Testar novamente

```bash
# No servidor
curl http://localhost:5000/swagger/v1/swagger.json

# Via nginx (local)
curl -H "Host: modec.automais.cloud" http://localhost/swagger/v1/swagger.json

# Via navegador
https://modec.automais.cloud/swagger
```

## Alternativa: Acessar Swagger via /api/swagger

Se o problema persistir, você pode configurar o Swagger para estar em `/api/swagger`:

### Opção 1: Mudar rota do Swagger no código

No `Program.cs`, mudar:
```csharp
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DataMais API v1");
    c.RoutePrefix = "api/swagger"; // Mudar para api/swagger
});
```

E no nginx, adicionar:
```nginx
location /api/swagger {
    proxy_pass http://127.0.0.1:5000/api/swagger;
    # ... resto da configuração
}
```

### Opção 2: Manter /swagger mas ajustar proxy

Garantir que o proxy_pass está correto:
```nginx
location /swagger {
    proxy_pass http://127.0.0.1:5000/swagger;  # Com /swagger no final
    # ...
}
```

## Debug Avançado

### Ver requisições no nginx

```bash
# Ativar access log temporariamente
sudo tail -f /var/log/nginx/access.log

# Acessar https://modec.automais.cloud/swagger
# Ver o que aparece no log
```

### Ver requisições na API

```bash
# Ver logs da API em tempo real
sudo journalctl -u datamais.service -f

# Acessar https://modec.automais.cloud/swagger
# Ver se a requisição chega na API
```

## Problema: Código Antigo no Servidor

Se ambos os testes retornam 404:
- `curl http://localhost:5000/swagger/v1/swagger.json` → 404
- `curl http://10.99.0.2:5000/swagger/v1/swagger.json` → 404

Isso confirma que o **código no servidor é uma versão antiga** sem Swagger habilitado.

**Solução**: Fazer deploy do código atualizado:
```bash
git add .
git commit -m "Habilitar Swagger em produção"
git push origin main
# Aguardar deploy automático via GitHub Actions
```

## Checklist Final

- [ ] API está rodando (`systemctl status datamais.service`)
- [ ] API responde em `http://localhost:5000/swagger/v1/swagger.json` (se 404, fazer deploy)
- [ ] Configuração do nginx foi aplicada (com `/swagger` no proxy_pass)
- [ ] Ordem das location blocks está correta
- [ ] Nginx foi recarregado (`systemctl reload nginx`)
- [ ] Nenhum erro no `nginx -t`
- [ ] Logs do nginx não mostram erros
