# NeuroCare

Plataforma SaaS (multi-tenant) para acompanhamento de pacientes.
**Status: Etapa 1 do MVP (núcleo).

## Estado de verificação (leia primeiro)

## Versão do .NET

**.NET 10 (LTS)** — `net10.0` em `Directory.Build.props`. Se o seu SDK for o .NET 8 (LTS), troque o
`TargetFramework` para `net8.0` e as versões `10.0.*` dos pacotes para `8.0.*`.

## Requisitos

- .NET SDK 10 (ou 8, ver acima)
- SQL Server (LocalDB no Windows, ou container via Docker)
- `dotnet tool install --global dotnet-ef`

## Instalação e execução

```bash
dotnet restore
dotnet build
dotnet test
```

### Banco de dados (SQL Server)

Desenvolvimento no Windows usa LocalDB (`appsettings.Development.json`, sem credenciais).
Outro SQL Server: use User Secrets (nunca coloque senha no código):

```bash
dotnet user-secrets --project src/NeuroCare.Web set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=NeuroCare;User Id=sa;Password=<sua-senha>;TrustServerCertificate=True"
```

### Migrations 

```bash
export NEUROCARE_CONNECTION="<connection string>"   # design-time
dotnet ef migrations add InitialCreate -p src/NeuroCare.Infrastructure -s src/NeuroCare.Web
dotnet ef database update -p src/NeuroCare.Infrastructure -s src/NeuroCare.Web
```

Em Development, `Database:AutoInitialize=true` aplica migrations se existirem; se não existirem, usa `EnsureCreated`.
Ao gerar a primeira migration, **apague o banco de dev criado por `EnsureCreated`** antes de aplicá-la.
Em produção, aplique migrations explicitamente (nunca `AutoInitialize`).

### Executar

```bash
dotnet run --project src/NeuroCare.Web --launch-profile https
```

### Docker (opcional, apenas local)

```bash
cp .env.example .env   # preencha os valores
docker compose up --build   # http://localhost:8080
```

## Usuários de demonstração (FICTÍCIOS)

Senha: valor de `Seed:DemoPassword` (em `appsettings.Development.json` para dev local, ou `SEED_DEMO_PASSWORD` no Docker).

| Usuário | Papel | Organização |
|---|---|---|
| admin@neurocare.local | Administrator | — (sem acesso clínico) |
| clinica@neurocare.local | ClinicAdmin | Clínica Demo A |
| medico@neurocare.local | Doctor | Clínica Demo A |
| medico.b@neurocare.local | Doctor | Clínica Demo B |
| paciente@neurocare.local | Patient | Clínica Demo A |

## Ainda NÃO implementado (próximas etapas)

Exames, anexos, evolução SOAP, medicamentos, sintomas, crises, quedas,
Parkinson/AVC, questionários genéricos, documentos (`IFileStorageService`), timeline, notificações, consentimentos LGPD,
exportação/anonimização, relatórios (`IReportService`), assinaturas/planos, IA (`IAssistantService`), JWT para mobile,
observabilidade além do `/health`. Nada disso foi simulado: ficam para as próximas etapas.

Configuração de IA, armazenamento e e-mail: **não aplicável ainda** (serão adicionadas com seus módulos).

## Roadmap

- **Fase 1 – MVP** (Etapa 1 concluída; restante em andamento)
- **Fase 2** – Aplicativo mobile
- **Fase 3** – Parkinson/Epilepsia/AVC avançados
- **Fase 4** – IA (apoio administrativo, sempre com revisão profissional)
- **Fase 5** – Integrações
- **Fase 6** – Hospitais
- **Fase 7** – Pesquisa clínica
- **Fase 8** – Analytics e NeuroData

Detalhes em `/docs`.

## Banco existente

A Etapa 5 adiciona as tabelas `ClinicQuestionnaires`, `ConsentRecords` e `NotificationDispatches`, além das colunas `ReminderEnabled` e `ReminderTimes` em `Medications`.

Este repositório continua seguindo a decisão original: quando não existem migrations, `DbInitializer` usa `EnsureCreated`, que **não altera um banco já criado**. Portanto:

- para desenvolvimento descartável, recrie o banco e deixe `Database:AutoInitialize=true`; ou
- para preservar dados, gere/aplique uma migration EF Core antes de executar esta versão em uma base existente.

Configuração de notificações:

```json
"Notifications": {
  "Enabled": true,
  "WorkerIntervalSeconds": 60,
  "AppointmentLeadHours": 24,
  "RequireConsent": true
}
```

Lembretes de medicação usam horários locais no formato `HH:mm`, separados por vírgula (ex.: `08:00,20:00`).
