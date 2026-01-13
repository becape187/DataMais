# Guia de Deploy - DataMais

## Deploy Automático via GitHub Actions

O projeto está configurado para fazer deploy automático quando há push na branch `main`.

### Estrutura de Deploy

- **Backend (.NET)**: `/home/becape/datamais.api`
- **Frontend (React)**: `/var/www/datamais`
- **Arquivo de Configuração**: `/home/becape/datamais.env`
- **Serviço Systemd**: `/etc/systemd/system/datamais.service`

### Workflow do GitHub Actions

O workflow (`.github/workflows/deploy.yml`) executa automaticamente:

1. **Checkout do código** do repositório
2. **Build e Deploy** via script `build.sh`:
   - Instala .NET SDK se necessário
   - Compila o backend .NET
   - Compila o frontend React
   - Copia arquivos para os diretórios de produção
   - Configura o serviço systemd
3. **Reinicia o serviço** `datamais.service`

### Script de Build (`build.sh`)

O script `build.sh` realiza:

#### Backend (.NET)
- Verifica/instala .NET 8.0 SDK
- Restaura dependências NuGet
- Compila em modo Release
- Publica a aplicação
- Copia para `/home/becape/datamais.api`
- Copia arquivo de serviço systemd

#### Frontend (React)
- Verifica Node.js instalado
- Instala dependências (`npm ci`)
- Compila para produção (`npm run build`)
- Copia para `/var/www/datamais`

### Configuração do Servidor

#### 1. GitHub Actions Runner

O runner self-hosted já está configurado e rodando como serviço:
```bash
sudo systemctl status actions.runner.becape187-DataMais.datamais.service
```

#### 2. Permissões

O usuário `becape` precisa ter permissões para:
- Escrever em `/home/becape/datamais.api`
- Escrever em `/var/www/datamais`
- Executar `sudo systemctl` (configurar sudoers)

**Configurar sudoers:**
```bash
sudo visudo
```

Adicionar linha:
```
becape ALL=(ALL) NOPASSWD: /usr/bin/systemctl daemon-reload, /usr/bin/systemctl restart datamais.service, /usr/bin/systemctl status datamais.service
```

#### 3. Nginx (para servir o frontend)

Se ainda não configurado, crie `/etc/nginx/sites-available/datamais`:

```nginx
server {
    listen 80;
    server_name datamais.local;

    root /var/www/datamais;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    # API proxy
    location /api {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Ativar site:
```bash
sudo ln -s /etc/nginx/sites-available/datamais /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

### Deploy Manual

Se precisar fazer deploy manual:

```bash
# Dar permissão de execução
chmod +x build.sh

# Executar build
./build.sh

# Recarregar e reiniciar serviço
sudo systemctl daemon-reload
sudo systemctl restart datamais.service
```

### Verificação

Após o deploy:

```bash
# Verificar status do serviço
sudo systemctl status datamais.service

# Ver logs
sudo journalctl -u datamais.service -f

# Verificar se a API está respondendo
curl http://localhost:5000/swagger

# Verificar se o frontend está acessível
curl http://localhost/
```

### Troubleshooting

#### Erro de permissão no sudo

Se o runner não conseguir executar comandos sudo:
1. Verificar configuração do sudoers
2. Testar manualmente: `sudo systemctl status datamais.service`

#### .NET não encontrado

O script tenta instalar automaticamente, mas se falhar:
```bash
# Instalar manualmente
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0
export PATH="$PATH:$HOME/.dotnet"
```

#### Node.js não encontrado

Instalar Node.js:
```bash
curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
sudo apt-get install -y nodejs
```

#### Frontend não carrega

1. Verificar permissões: `sudo chown -R www-data:www-data /var/www/datamais`
2. Verificar configuração do Nginx
3. Verificar logs: `sudo tail -f /var/log/nginx/error.log`

### Estrutura de Diretórios

```
/home/becape/
├── datamais.api/          # Backend compilado
│   ├── DataMais.dll
│   ├── appsettings.json
│   └── ...
├── datamais.env           # Configurações (.env)
└── actions-runner/        # GitHub Actions Runner

/var/www/
└── datamais/              # Frontend compilado
    ├── index.html
    ├── assets/
    └── ...

/etc/systemd/system/
└── datamais.service       # Serviço systemd
```

### Notas

- O arquivo `datamais.env` **não** é sobrescrito durante o deploy
- O serviço systemd é atualizado a cada deploy
- O frontend é servido pelo Nginx (ou outro servidor web)
- A API roda na porta 5000 (configurável no `datamais.env` ou `appsettings.json`)
