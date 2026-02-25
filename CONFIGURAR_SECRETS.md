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
  - ⚠️ **Obrigatório**: Este é o método de autenticação usado

#### SSH_PORT
- **Nome**: `SSH_PORT`
- **Valor**: Porta SSH (geralmente `22`)
  - Exemplo: `22`

### 3. Verificar Secrets Configurados

Você deve ter os seguintes secrets configurados:

```
✅ SSH_HOST
✅ SSH_USER
✅ SSH_PASSWORD
✅ SSH_PORT
```

## Testar Conexão SSH

Antes de fazer o deploy, teste a conexão:

```bash
ssh -p 22 becape@seu-servidor-ip
```

Digite a senha quando solicitado.

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
```

**Nota**: O workflow usa autenticação por senha. Certifique-se de que a senha está correta e que o servidor permite autenticação por senha via SSH.
