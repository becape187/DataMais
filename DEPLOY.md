# Guia de Deploy - DataMais

## Deploy Automático via GitHub Actions (SSH)

O projeto está configurado para fazer deploy automático quando há push na branch `main` usando SSH para conectar ao servidor Linux.

### Estrutura de Deploy

- **Backend (.NET)**: `/home/becape/datamais.api`
- **Frontend (React)**: `/var/www/datamais`
- **Arquivo de Configuração**: `/home/becape/datamais.env`
- **Serviço Systemd**: `/etc/systemd/system/datamais.service`

### Configuração de Secrets no GitHub

Configure os seguintes secrets no repositório GitHub:

**Settings → Secrets and variables → Actions → New repository secret**

1. **SSH_HOST**: IP ou hostname do servidor (ex: `192.168.1.100`)
2. **SSH_USER**: Usuário SSH (ex: `becape`)
3. **SSH_PASSWORD**: Senha do usuário SSH
4. **SSH_PORT**: Porta SSH (padrão: `22`)

**Como configurar:**
1. Acesse: `https://github.com/seu-usuario/DataMais/settings/secrets/actions`
2. Clique em "New repository secret"
3. Adicione cada secret acima

### Workflow do GitHub Actions

O workflow (`.github/workflows/deploy.yml`) executa automaticamente:

1. **Checkout do código** no runner do GitHub (Ubuntu)
2. **Setup .NET SDK** e Node.js no runner
3. **Build do backend** (.NET) no runner
4. **Build do frontend** (React) no runner
5. **Deploy via SCP**:
   - Copia backend compilado para `/home/becape/datamais.api`
   - Copia frontend compilado para `/var/www/datamais`
6. **Configuração via SSH**:
   - Copia arquivo de serviço systemd
   - Ajusta permissões
   - Reinicia o serviço

### Fluxo de Deploy

```
Push no GitHub
    ↓
GitHub Actions Runner (Ubuntu)
    ↓
Compila Backend (.NET)
    ↓
Compila Frontend (React)
    ↓
SCP → Servidor Linux (via SSH)
    ↓
SSH → Configura e reinicia serviço
```

### Configuração do Servidor

#### 1. Permissões SSH

O usuário SSH precisa ter permissões para:
- Escrever em `/home/becape/datamais.api`
- Escrever em `/var/www/datamais`
- Executar `sudo systemctl` (configurar sudoers)

**Configurar sudoers:**
```bash
sudo visudo
```

Adicionar linha (substitua `becape` pelo seu usuário SSH):
```
becape ALL=(ALL) NOPASSWD: /usr/bin/cp /etc/systemd/system/datamais.service, /usr/bin/chown, /usr/bin/chmod, /usr/bin/systemctl daemon-reload, /usr/bin/systemctl restart datamais.service, /usr/bin/systemctl status datamais.service
```

#### 2. Criar diretórios

```bash
sudo mkdir -p /home/becape/datamais.api
sudo mkdir -p /var/www/datamais
sudo chown -R becape:becape /home/becape/datamais.api
sudo chown -R www-data:www-data /var/www/datamais
```

#### 3. Nginx (para servir o frontend)

Crie `/etc/nginx/sites-available/datamais`:

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

Se precisar fazer deploy manual via SSH:

```bash
# No seu computador local
ssh usuario@servidor

# No servidor
cd /caminho/do/projeto
git pull origin main
cd DataMais
dotnet publish -c Release -o /home/becape/datamais.api
cd ../DataMaisWeb
npm ci
npm run build
sudo cp -r dist/* /var/www/datamais/
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

#### Erro de conexão SSH

1. Verificar se o servidor está acessível:
   ```bash
   ping seu-servidor-ip
   ```

2. Testar conexão SSH manualmente:
   ```bash
   ssh usuario@servidor-ip -p porta
   ```

3. Verificar se os secrets estão configurados corretamente no GitHub

#### Erro de permissão no sudo

1. Verificar configuração do sudoers
2. Testar manualmente: `sudo systemctl status datamais.service`

#### Erro ao copiar arquivos

1. Verificar se os diretórios existem e têm permissões corretas
2. Verificar se o usuário SSH tem acesso aos diretórios

### Estrutura de Diretórios no Servidor

```
/home/becape/
├── datamais.api/          # Backend compilado
│   ├── DataMais.dll
│   ├── appsettings.json
│   └── ...
└── datamais.env           # Configurações (.env)

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
- O build acontece no runner do GitHub, não no servidor
- Os arquivos compilados são copiados via SCP para o servidor