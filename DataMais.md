# DataMais - Sistema de Gestão de Ensaios Hidráulicos

## Visão Geral

O DataMais é um sistema completo para gestão de ensaios hidráulicos, composto por uma API backend em ASP.NET Core e uma aplicação web frontend em React. O sistema permite gerenciar clientes, cilindros, sensores, ensaios e relatórios, além de fornecer controle hidráulico em tempo real.

## Arquitetura do Projeto

O projeto é dividido em duas partes principais:

### 1. Backend (DataMais)
- **Tecnologia**: ASP.NET Core 8.0 (C#)
- **Tipo**: API REST
- **Localização**: `DataMais/`

### 2. Frontend (DataMaisWeb)
- **Tecnologia**: React 19 + TypeScript
- **Build Tool**: Vite 7
- **Localização**: `DataMaisWeb/`

---

## Estrutura do Backend (DataMais)

### Tecnologias e Dependências

- **.NET 8.0**: Framework principal
- **InfluxDB.Client** (v4.18.0): Banco de dados de séries temporais para dados de sensores
- **Npgsql.EntityFrameworkCore.PostgreSQL** (v8.0.0): Integração com PostgreSQL para dados relacionais
- **NModbus4** (v2.1.0): Comunicação Modbus para sensores e equipamentos
- **Swashbuckle.AspNetCore** (v6.6.2): Documentação automática da API (Swagger)

### Estrutura de Diretórios

```
DataMais/
├── Program.cs                 # Ponto de entrada da aplicação
├── DataMais.csproj            # Arquivo de projeto .NET
├── appsettings.json           # Configurações da aplicação
├── appsettings.Development.json  # Configurações de desenvolvimento
├── Properties/
│   └── launchSettings.json    # Configurações de execução
└── DataMais.http              # Arquivo de testes HTTP
```

### Arquitetura de Dados

O sistema utiliza uma arquitetura híbrida de bancos de dados:

#### InfluxDB - Séries Temporais
- **Uso**: Armazenamento de dados de sensores em tempo real
- **Dados armazenados**:
  - Leituras de pressão (sensor 1 e sensor 2)
  - Leituras de carga (ton 1 e ton 2)
  - Dados de ensaios em tempo real
  - Histórico de medições durante ensaios
- **Vantagens**:
  - Otimizado para séries temporais
  - Alta performance para inserção e consulta de dados temporais
  - Retenção automática de dados
  - Queries eficientes para gráficos e análises

#### PostgreSQL - Dados Relacionais
- **Uso**: Armazenamento de dados estruturados e relacionais
- **Dados armazenados**:
  - Clientes
  - Cilindros e suas configurações
  - Sensores e suas configurações
  - Usuários
  - Metadados de ensaios
  - Relatórios e documentos

### Funcionalidades do Backend

- API REST para gerenciamento de:
  - Clientes
  - Cilindros
  - Sensores
  - Ensaios
  - Relatórios
  - Usuários
- **Integração com InfluxDB** para armazenamento de séries temporais de sensores
- Comunicação Modbus para leitura de sensores em tempo real
- Banco de dados PostgreSQL para dados relacionais e configurações

---

## Estrutura do Frontend (DataMaisWeb)

### Tecnologias e Dependências

#### Dependências Principais
- **React** (v19.2.3): Biblioteca UI
- **React DOM** (v19.2.3): Renderização React
- **React Router DOM** (v7.11.0): Roteamento de páginas
- **Axios** (v1.13.2): Cliente HTTP para comunicação com API
- **Recharts** (v3.6.0): Biblioteca de gráficos

#### Dependências de Desenvolvimento
- **Vite** (v7.2.4): Build tool e dev server
- **TypeScript** (v5.9.3): Tipagem estática
- **@vitejs/plugin-react** (v4.3.4): Plugin React para Vite

### Estrutura de Diretórios

```
DataMaisWeb/
├── src/
│   ├── main.tsx              # Ponto de entrada da aplicação React
│   ├── App.tsx                # Componente raiz com rotas
│   ├── index.css              # Estilos globais e variáveis CSS
│   │
│   ├── components/            # Componentes reutilizáveis
│   │   ├── Layout.tsx         # Layout principal com sidebar e menu
│   │   └── Layout.css          # Estilos do layout
│   │
│   └── pages/                 # Páginas da aplicação
│       ├── Dashboard.tsx      # Dashboard principal
│       ├── Ensaio.tsx          # Página de ensaios
│       ├── Clientes.tsx        # Lista de clientes
│       ├── DetalhesCliente.tsx # Detalhes do cliente
│       ├── ConfiguracaoCilindro.tsx  # Configuração de cilindros
│       ├── Sensores.tsx        # Lista de sensores
│       ├── ConfiguracaoSensor.tsx    # Configuração de sensores
│       ├── Relatorios.tsx      # Lista de relatórios
│       ├── VisualizarRelatorio.tsx    # Visualização de relatório
│       ├── ComentariosDesvio.tsx      # Comentários de desvios
│       └── GestaoUsuarios.tsx  # Gestão de usuários
│
├── public/                    # Arquivos estáticos
│   └── modec-logo.png        # Logo da empresa
│
├── index.html                 # HTML principal
├── vite.config.ts             # Configuração do Vite
├── tsconfig.json              # Configuração do TypeScript
└── package.json               # Dependências e scripts
```

### Rotas da Aplicação

| Rota | Componente | Descrição |
|------|------------|-----------|
| `/` | Dashboard | Redireciona para `/dashboard` |
| `/dashboard` | Dashboard | Página inicial com visão geral |
| `/ensaio` | Ensaio | Gerenciamento de ensaios |
| `/clientes` | Clientes | Lista de clientes com busca |
| `/clientes/:id` | DetalhesCliente | Detalhes do cliente, relatórios e cilindros |
| `/clientes/:clienteId/cilindros/:cilindroId` | ConfiguracaoCilindro | Configuração de cilindro (novo ou edição) |
| `/sensores` | Sensores | Lista de sensores |
| `/sensores/:id/configuracao` | ConfiguracaoSensor | Configuração de sensor |
| `/relatorios` | Relatorios | Lista de relatórios |
| `/relatorios/:id` | VisualizarRelatorio | Visualização detalhada de relatório |
| `/ensaio/comentarios/:eventoId` | ComentariosDesvio | Comentários de desvios em ensaios |
| `/usuarios` | GestaoUsuarios | Gestão de usuários do sistema |

### Componentes Principais

#### Layout Component
O componente `Layout.tsx` fornece a estrutura base da aplicação:
- **Sidebar**: Menu de navegação lateral fixo
- **Top Bars**: Barras coloridas no topo (vermelho e azul MODEC)
- **Controles Hidráulicos**: Painel fixo na parte inferior do sidebar com:
  - Display de carga (2 sensores) em toneladas
  - Display de pressão (2 sensores) em bar
  - Botões de controle: Avançar, Recuar, Ligar/Desligar
- **Main Content**: Área principal onde as páginas são renderizadas

### Funcionalidades Principais

#### 1. Gestão de Clientes
- Lista de clientes com busca por nome, CNPJ, contato ou email
- Visualização de detalhes do cliente
- Lista de relatórios do cliente
- Lista de cilindros cadastrados do cliente

#### 2. Gestão de Cilindros
- Cadastro e edição de cilindros
- Parâmetros do cilindro:
  - Informações básicas (nome, descrição, códigos, modelo, fabricante)
  - Dimensões (diâmetro interno, comprimento e diâmetro da haste)
  - Pressões máximas (suportada e segurança para áreas A e B)
  - Parâmetros de ensaio Câmara A (pré-carga, carga nominal, tempos, percentuais)
  - Parâmetros de ensaio Câmara B (mesmos parâmetros da Câmara A)
- Visualização de relatórios do cilindro

#### 3. Controle Hidráulico
- Display em tempo real de:
  - Carga em toneladas (2 sensores)
  - Pressão em bar (2 sensores)
- Controles:
  - Avançar/Recuar
  - Ligar/Desligar sistema

#### 4. Gestão de Ensaios
- Execução e monitoramento de ensaios
- Registro de desvios e comentários

#### 5. Gestão de Sensores
- Lista e configuração de sensores
- Integração com equipamentos via Modbus

#### 6. Relatórios
- Geração e visualização de relatórios de ensaios
- Histórico de relatórios por cliente e cilindro

#### 7. Gestão de Usuários
- Cadastro e gestão de usuários do sistema

### Estilos e Design

O sistema utiliza variáveis CSS para manter consistência visual:

```css
--modec-red: #E31E24
--modec-navy: #003366
--modec-navy-dark: #001F3F
--modec-navy-light: #004080
--bg-light: #F5F7FA
--text-primary: #1A1A1A
--text-secondary: #666666
--border-color: #E0E0E0
--white: #FFFFFF
```

### Scripts Disponíveis

```bash
# Desenvolvimento
npm run dev          # Inicia servidor de desenvolvimento

# Build
npm run build        # Compila para produção

# Preview
npm run preview      # Visualiza build de produção
```

---

## Como Executar

### Backend (DataMais)

#### Desenvolvimento

```bash
cd DataMais
dotnet run
```

A API estará disponível em `https://localhost:5001` (ou porta configurada) com Swagger em `/swagger`.

#### Produção (Linux)

O arquivo de configuração está localizado em `/home/becape/datamais.env`.

Para instalar como serviço systemd, consulte `DataMais/INSTALL.md`.

**Comandos rápidos:**
```bash
# Instalar serviço
sudo cp datamais.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable datamais.service
sudo systemctl start datamais.service

# Verificar status
sudo systemctl status datamais.service

# Ver logs
sudo journalctl -u datamais.service -f
```

### Frontend (DataMaisWeb)

```bash
cd DataMaisWeb
npm install          # Instala dependências (primeira vez)
npm run dev         # Inicia servidor de desenvolvimento
```

A aplicação estará disponível em `http://localhost:5173` (porta padrão do Vite).

---

## Deploy

O deploy é feito automaticamente via GitHub Actions usando SSH quando há push na branch `main`.

**Configuração necessária:**
- Secrets do GitHub configurados (SSH_HOST, SSH_USER, SSH_PASSWORD, SSH_PORT, SSH_KEY)
- Servidor Linux acessível via SSH
- Diretórios de produção criados no servidor

Consulte `DEPLOY.md` para instruções detalhadas de configuração.

## Próximos Passos

- [ ] Implementar integração completa com API backend
- [ ] Adicionar autenticação e autorização
- [ ] Implementar comunicação WebSocket para dados em tempo real
- [ ] Adicionar testes unitários e de integração
- [ ] Documentação de API completa

---

## Arquitetura de Dados Detalhada

### InfluxDB - Séries Temporais

O **InfluxDB** é utilizado exclusivamente para armazenar séries temporais de dados de sensores durante os ensaios. Esta escolha arquitetural oferece:

#### Estrutura de Dados no InfluxDB

**Measurement: `sensores`**
- **Tags** (indexadas):
  - `sensor_id`: ID do sensor
  - `sensor_tipo`: tipo (pressao, carga)
  - `cilindro_id`: ID do cilindro associado
  - `ensaio_id`: ID do ensaio em execução
- **Fields** (valores):
  - `valor`: valor da leitura (float)
  - `unidade`: unidade de medida (string)
- **Timestamp**: timestamp automático da leitura

**Measurement: `ensaios`**
- **Tags**:
  - `ensaio_id`: ID do ensaio
  - `cliente_id`: ID do cliente
  - `cilindro_id`: ID do cilindro
- **Fields**:
  - `carga_ton_1`: carga sensor 1 (float)
  - `carga_ton_2`: carga sensor 2 (float)
  - `pressao_sensor_1`: pressão sensor 1 (float)
  - `pressao_sensor_2`: pressão sensor 2 (float)
  - `status`: status do ensaio (string)
- **Timestamp**: timestamp da leitura

#### Benefícios do InfluxDB

1. **Performance**: Otimizado para inserção e consulta de dados temporais
2. **Escalabilidade**: Suporta milhões de pontos de dados por segundo
3. **Retenção**: Políticas automáticas de retenção de dados
4. **Queries**: Linguagem InfluxQL/Flux otimizada para séries temporais
5. **Downsampling**: Capacidade de reduzir resolução de dados antigos automaticamente

### PostgreSQL - Dados Relacionais

O **PostgreSQL** armazena todos os dados relacionais e de configuração:

- **Tabelas principais**:
  - `clientes`: informações dos clientes
  - `cilindros`: configurações e parâmetros dos cilindros
  - `sensores`: configurações dos sensores
  - `ensaios`: metadados dos ensaios (referência para dados no InfluxDB)
  - `relatorios`: relatórios gerados
  - `usuarios`: usuários do sistema

## Configuração do InfluxDB

O token de acesso do InfluxDB gerado após a instalação está documentado em `DataMais/INFLUX_SETUP.md`.

**Token padrão após instalação:**
```
MkpRB5OIOlb9xQTZetpDE4ZCDB2hezbqlSDYNzmqMzenvRaPtxAX2iMHZAUTwhTQv8ty6yNIfJnPhlbXZPEiIA==
```

⚠️ **Importante**: Este token deve ser configurado no arquivo `.env` e mantido seguro. Consulte `INFLUX_SETUP.md` para mais detalhes.

## Notas Técnicas

- O frontend está preparado para comunicação com a API, mas atualmente utiliza dados mockados
- A integração com **InfluxDB** está configurada no backend para armazenamento de séries temporais
- A integração com **Modbus** está configurada para leitura de sensores em tempo real
- O sistema utiliza **PostgreSQL** para dados relacionais e **InfluxDB** para séries temporais
- O controle hidráulico no sidebar está sempre visível em todas as páginas
- Dados de sensores são escritos no InfluxDB em alta frequência durante ensaios
- Consultas históricas e gráficos utilizam dados do InfluxDB