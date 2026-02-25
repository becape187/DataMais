# Como Configurar Secrets do GitHub para Deploy SSH

## Passo a Passo

### 1. Acessar Configurações do Repositório

1. Vá para o seu repositório no GitHub
2. Clique em **Settings** (Configurações)
3. No menu lateral, clique em **Secrets and variables** → **Actions**

### 2. Adicionar Secrets

Clique em **New repository secret** e adicione cada um dos seguintes:

#### SSH_HOST
- **Nome**: `SSH_HOST`
- **Valor**: IP ou hostname do seu servidor
  - Exemplo: `192.168.1.100` ou `datamais.example.com`

#### SSH_USER
- **Nome**: `SSH_USER`
- **Valor**: Usuário SSH do servidor
  - Exemplo: `becape` ou `root`

#### SSH_PASSWORD
- **Nome**: `SSH_PASSWORD`
- **Valor**: Senha do usuário SSH
  - ⚠️ **Importante**: Se preferir usar chave SSH, deixe este campo vazio e configure `SSH_KEY`

#### SSH_PORT
- **Nome**: `SSH_PORT`
- **Valor**: Porta SSH (geralmente `22`)
  - Exemplo: `22`

#### SSH_KEY (Opcional - Recomendado)
- **Nome**: `SSH_KEY`
- **Valor**: Conteúdo completo da chave privada SSH
  - ⚠️ **Se usar chave SSH, deixe `SSH_PASSWORD` vazio**

### 3. Verificar Secrets Configurados

Você deve ter os seguintes secrets configurados:

```
✅ SSH_HOST
✅ SSH_USER
✅ SSH_PASSWORD (ou SSH_KEY)
✅ SSH_PORT
```

## Usando Chave SSH (Recomendado - Mais Seguro)

### Gerar Chave SSH (se ainda não tiver)

No seu computador local:
```bash
ssh-keygen -t ed25519 -C "github-actions-datamais"
```

### Copiar Chave Pública para o Servidor

```bash
ssh-copy-id -i ~/.ssh/id_ed25519.pub becape@seu-servidor-ip
```

Ou manualmente:
```bash
cat ~/.ssh/id_ed25519.pub | ssh becape@seu-servidor-ip "mkdir -p ~/.ssh && cat >> ~/.ssh/authorized_keys"
```

### Adicionar Chave Privada no GitHub

1. Copie o conteúdo da chave privada:
   ```bash
   cat ~/.ssh/id_ed25519
   ```

2. Cole no secret `SSH_KEY` no GitHub

3. **Deixe `SSH_PASSWORD` vazio** se usar chave SSH

## Testar Conexão SSH

Antes de fazer o deploy, teste a conexão:

```bash
# Com senha
ssh -p 22 becape@seu-servidor-ip

# Com chave
ssh -i ~/.ssh/id_ed25519 -p 22 becape@seu-servidor-ip
```

## Verificar Secrets no GitHub Actions

Após configurar, quando fizer push na branch `main`, o workflow tentará conectar via SSH. Se houver erro de conexão, verifique:

1. ✅ Secrets estão configurados corretamente
2. ✅ Servidor está acessível (ping, telnet na porta SSH)
3. ✅ Usuário e senha/chave estão corretos
4. ✅ Firewall permite conexão SSH

## Estrutura Final dos Secrets

```
SSH_HOST: 192.168.1.100
SSH_USER: becape
SSH_PASSWORD: sua-senha-aqui
SSH_PORT: 22
SSH_KEY: (vazio ou chave privada)
```

**Nota**: Se usar `SSH_KEY`, deixe `SSH_PASSWORD` vazio. O workflow tentará usar a chave primeiro.
