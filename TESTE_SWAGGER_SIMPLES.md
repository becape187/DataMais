# 🚀 Teste Rápido no Swagger - Ligar/Desligar Motor

## 📍 Passo 1: Acessar o Swagger

Abra no navegador:
- **Local:** `http://localhost:5109/swagger`
- **Produção:** `https://modec.automais.cloud/swagger`

---

## 🔍 Passo 2: Encontrar os IDs dos Registros

### 2.1. Buscar o registro BOTAO_LIGA_MOTOR

1. No Swagger, encontre o endpoint: **`GET /api/ModbusConfig/search`**
2. Clique nele para expandir
3. Clique em **"Try it out"**
4. No campo `nome`, digite: `BOTAO_LIGA_MOTOR`
5. Clique em **"Execute"**
6. **Copie o ID** que aparece na resposta (exemplo: `22`)

### 2.2. Buscar o registro BOTAO_DESLIGA_MOTOR

1. No mesmo endpoint, mude o `nome` para: `BOTAO_DESLIGA_MOTOR`
2. Clique em **"Execute"**
3. **Copie o ID** que aparece na resposta (exemplo: `21`)

**💡 Dica:** Os endereços Modbus são 13 (liga) e 12 (desliga), mas você precisa do **ID do banco de dados**, não do endereço Modbus!

---

## ⚡ Passo 3: Ligar o Motor

1. No Swagger, encontre o endpoint: **`POST /api/ModbusConfig/{id}/write`**
2. Clique nele para expandir
3. Clique em **"Try it out"**
4. No campo `id`, cole o **ID do BOTAO_LIGA_MOTOR** (que você copiou no passo 2.1)
5. No campo **Request body**, cole:
```json
{
  "valor": true
}
```
6. Clique em **"Execute"**
7. Se der certo, você verá:
```json
{
  "message": "Valor escrito com sucesso",
  "valor": true
}
```

---

## ⏹️ Passo 4: Desligar o Motor

1. No mesmo endpoint **`POST /api/ModbusConfig/{id}/write`**
2. Clique em **"Try it out"** novamente
3. No campo `id`, cole o **ID do BOTAO_DESLIGA_MOTOR** (que você copiou no passo 2.2)
4. No campo **Request body**, mantenha:
```json
{
  "valor": true
}
```
5. Clique em **"Execute"**
6. Se der certo, você verá a mesma mensagem de sucesso

---

## 📋 Resumo Rápido

| Ação | Endpoint | ID (buscar antes) | Body |
|------|----------|-------------------|------|
| **Ligar Motor** | `POST /api/ModbusConfig/{id}/write` | ID do BOTAO_LIGA_MOTOR | `{"valor": true}` |
| **Desligar Motor** | `POST /api/ModbusConfig/{id}/write` | ID do BOTAO_DESLIGA_MOTOR | `{"valor": true}` |

---

## 🎯 Exemplo Prático

Suponha que você encontrou:
- **BOTAO_LIGA_MOTOR** = ID `22`
- **BOTAO_DESLIGA_MOTOR** = ID `21`

### Para Ligar:
```
POST /api/ModbusConfig/22/write
Body: {"valor": true}
```

### Para Desligar:
```
POST /api/ModbusConfig/21/write
Body: {"valor": true}
```

---

## ⚠️ Importante

- Os **endereços Modbus** (12 e 13) são diferentes dos **IDs do banco**
- Você precisa usar os **IDs do banco de dados** no Swagger
- O valor sempre é `true` para ativar os botões
- Verifique os logs do backend se algo não funcionar

---

## 🐛 Troubleshooting

**Problema:** "Configuração Modbus não encontrada"
- ✅ Verifique se digitou o ID correto
- ✅ Use o endpoint de busca primeiro para encontrar o ID

**Problema:** "Registro Modbus está inativo"
- ✅ Verifique no banco se o registro tem `Ativo = true`

**Problema:** "Erro ao escrever valor"
- ✅ Verifique se o dispositivo Modbus está acessível
- ✅ Verifique os logs do backend para mais detalhes
