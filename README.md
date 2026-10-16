# NeuroCare

Plataforma SaaS (multi-tenant) para acompanhamento longitudinal de pacientes de neurologia.
**Status: Etapa 1 do MVP (núcleo). Escrita sem acesso a compilador — veja "Estado de verificação".**

## Estado de verificação (leia primeiro)

Esta base foi gerada em um ambiente **sem .NET SDK e sem acesso ao NuGet**. Portanto:

- ❌ A solução **não foi compilada** e os **testes não foram executados** por quem a gerou.
- ❌ **Migrations não foram geradas** (exigem `dotnet ef`). Sem migrations, o app usa `EnsureCreated` em desenvolvimento.
- ❌ Responsividade, rotas e login **não foram testados em navegador**.
- ✅ O código foi revisado manualmente e há verificação estática básica (chaves balanceadas, arquivos esperados).

Primeiro passo no seu computador: `dotnet build && dotnet test`. Pequenos erros de compilação são possíveis; corrija-os antes de avançar.

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

### Migrations (recomendado gerar agora)

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

## Usuários de demonstração (FICTÍCIOS — trocar/remover antes de produção)

Senha: valor de `Seed:DemoPassword` (em `appsettings.Development.json` para dev local, ou `SEED_DEMO_PASSWORD` no Docker).

| Usuário | Papel | Organização |
|---|---|---|
| admin@neurocare.local | Administrator | — (sem acesso clínico) |
| clinica@neurocare.local | ClinicAdmin | Clínica Demo A |
| medico@neurocare.local | Doctor | Clínica Demo A |
| medico.b@neurocare.local | Doctor | Clínica Demo B |
| paciente@neurocare.local | Patient | Clínica Demo A |

Teste manual de isolamento: entre como `medico@` e confirme que "Paulo Fernandes" (Org B) não aparece; `medico.b@` vê só ele.

## O que a Etapa 1 entrega

- ASP.NET Core Identity (Guid), papéis, login/logout, alterar senha, bloqueio por tentativas, e-mail confirmado obrigatório
- Organizações + **multi-tenancy**: filtro global do EF Core por `OrganizationId`, guarda de escrita no `SaveChanges`, verificação nos serviços (`AccessGuard`)
- Pacientes (CRUD, CPF validado e mascarado, busca, ativar/desativar) e página de detalhes com abas
- Consultas (agendar, editar, confirmar, concluir, falta, cancelar; conflito de horário)
- Dashboard do médico (saudação, cards, consultas de hoje, gráfico Chart.js, pacientes recentes)
- Portal do paciente (`/PatientPortal`): consultas próprias
- Auditoria (login, visualização/criação/alteração de paciente, consultas) sem dados clínicos
- API somente leitura com DTOs: `/api/patients`, `/api/appointments`
- Segurança: antiforgery global, cookies HttpOnly/Secure, CSP e demais headers, rate limiting no login, `/health`
- Testes: CPF, domínio, isolamento entre organizações, autorização (paciente A × B, sem permissão), auditoria, consultas

## Etapa 2 – contas, e-mail e administração (também NÃO compilada)

- **Esqueci minha senha / redefinir senha** (`/Account/ForgotPassword`, `/Account/ResetPassword`): resposta idêntica exista o e-mail ou não, rate limiting, token de uso único.
- **Cadastro por convite** (sem auto-cadastro, adequado a dados de saúde): o usuário criado recebe um e-mail com link para definir a senha; ao definir, o e-mail passa a confirmado.
- **Administrator** (`/Organizations`): lista, cria (com o primeiro ClinicAdmin, que recebe convite), desativa/reativa organizações. Desativar encerra as sessões da organização e bloqueia novos logins.
- **ClinicAdmin** (`/Users`): cria médicos e outros administradores da clínica, desativa/reativa, reenvia convite. Não vê nem altera usuários de outras organizações; não desativa a si mesmo.
- **Convite ao portal do paciente**: botão na página do paciente (exige e-mail cadastrado).
- **E-mail**: `Email:Smtp:*` + `Email:From` (senha via User Secrets ou variável de ambiente). Em desenvolvimento, `Email:UseLogSender=true` **grava o e-mail (com o link) no log** em vez de enviar; veja o console para pegar o link do convite. Fora de Development, `App:PublicBaseUrl` é obrigatória (evita host header injection nos links).

```bash
dotnet user-secrets --project src/NeuroCare.Web set "Email:Smtp:Host" "smtp.exemplo.com"
dotnet user-secrets --project src/NeuroCare.Web set "Email:From" "no-reply@exemplo.com"
dotnet user-secrets --project src/NeuroCare.Web set "Email:Smtp:User" "<usuario>"
dotnet user-secrets --project src/NeuroCare.Web set "Email:Smtp:Password" "<senha>"
```

Limitações conhecidas: o token de redefinição vale 24 h (padrão do Identity); `SmtpClient` (System.Net.Mail) é simples e funcional, mas MailKit é recomendado em produção;
sem 2FA; criação de usuário e do registro de médico não são atômicas (falha rara deixaria um usuário sem registro de médico).

## Etapa 3 – módulo clínico (também NÃO compilada)

- **Evolução clínica SOAP** (`/ClinicalNotes`): rascunho → **assinatura** → imutável; correções por **adendo** (qualquer médico da organização). Só o autor edita/assina. Somente médicos leem evoluções (ClinicAdmin e pacientes não).
- **Medicamentos** (`/Medications`): médico prescreve, suspende, retoma e finaliza; bloqueia duplicidade de nome em uso/suspenso; paciente só consulta os próprios. **Não há checagem de interações medicamentosas.**
- **Sintomas** (`/Symptoms`) e **Crises** (`/Seizures`): diários registrados pelo médico ou pelo próprio paciente (intensidade 0–10; duração em segundos), com gráfico (Chart.js). Sem exclusão (registro clínico). O sistema apenas registra e exibe — **não interpreta nem sugere diagnóstico**.
- **Linha do tempo** (`/Timeline`): consultas, evoluções (só médico), medicamentos, sintomas e crises.
- **Regra de segurança central:** quando o usuário é paciente, o alvo é sempre ele mesmo (o `patientId` da URL é ignorado), o que impede ler/gravar dados de outro paciente.
- Auditoria de leitura e escrita clínica, sem conteúdo clínico nos logs.
- **Nova migration necessária:** esta etapa adiciona tabelas (`ClinicalNotes`, `ClinicalNoteAddenda`, `Medications`, `SymptomRecords`, `SeizureEvents`). Se você usa `EnsureCreated`, apague o banco de desenvolvimento e rode de novo; se usa migrations, gere `dotnet ef migrations add ClinicalModule`.

Pendências desta etapa: vínculo evolução↔consulta, registro de adesão (“tomei o remédio”), quedas, exames, questionários/escalas, anexos, exportação de prontuário e assinatura digital com certificado (hoje “assinar” é um travamento lógico com data/autor, sem valor de assinatura eletrônica qualificada).

## Etapa 4 (parte 1) – quedas e questionários com pontuação (também NÃO compilada)

- **Quedas** (`/Falls`): diário (situação, local, lesão, atendimento médico) registrado pelo médico ou pelo próprio paciente, com gráfico mensal; aparece na linha do tempo.
- **Questionários** (`/Questionnaires`): motor genérico de pontuação por soma, com histórico e gráfico de evolução. Catálogo atual (instrumentos de uso livre): **PHQ-9**, **GAD-7**, **Rankin modificada (mRS)** e **Hoehn e Yahr simplificada** (os dois últimos são aplicados pelo profissional).
- **Item de segurança do PHQ-9:** resposta positiva ao item 9 (pensamentos de se ferir) gera (a) mensagem de apoio ao paciente com CVV 188 e SAMU 192 e (b) alerta no dashboard do **médico**. **Não há notificação automática** (e-mail/push) nem avaliação de risco pelo sistema — isso depende do módulo de notificações e de um protocolo clínico definido pela clínica.
- A pontuação mostra valor e faixa; **não é diagnóstico**. Auditoria registra apenas metadados (sem pontuação ou respostas).
- **Instrumentos licenciados** (MDS-UPDRS, MoCA, MMSE etc.) **não foram incluídos** por direitos autorais; só devem ser adicionados com autorização do detentor.
- Limitação: o catálogo é definido em código (adicionar um instrumento exige deploy). Um construtor de questionários por organização (tabelas no banco) fica para etapa futura. Pontuações por soma simples; escalas com regras especiais (subescalas, pesos, itens invertidos) exigirão extensão do motor.
- **Nova migration:** tabelas `FallEvents` e `QuestionnaireResponses`.

## Etapa 4 (partes 2 e 3) – documentos e relatório (também NÃO compilada)

- **Documentos** (`/Documents`): upload de PDF, JPG e PNG (até 10 MB; `Storage:MaxFileBytes`) por médico ou pelo próprio paciente.
  - Tipo validado **pelo conteúdo** (assinatura do arquivo) e pela extensão — um executável renomeado para `.pdf` é recusado.
  - O nome enviado **nunca** vira caminho: o arquivo é gravado com uma chave gerada pelo sistema (`org/paciente/guid`), fora de `wwwroot`.
  - Download sempre como anexo; hash SHA-256 guardado; exclusão **lógica** (somente médico; o arquivo físico é mantido para retenção).
  - Acesso por organização; paciente só vê os próprios; administrador da clínica não acessa documentos clínicos.
  - Configure `Storage:RootPath` com caminho absoluto em volume persistente **com backup** (no Docker já há o volume `neurocare-uploads`).
- **Relatório** (`/Reports`, só médico): resumo do prontuário (identificação, medicamentos em uso, últimas evoluções assinadas, sintomas, crises, quedas, escalas, documentos) em página própria para impressão. O botão **Imprimir / Salvar como PDF** usa o recurso do navegador (não há geração de PDF no servidor). O relatório apenas consolida dados registrados, sem interpretação.
- Documentos entram na linha do tempo. **Nova migration:** tabela `PatientDocuments`.

Limitações: arquivos **não são criptografados pela aplicação** (use criptografia de disco/volume) e **não há antivírus** (integrar um scanner antes de produção); upload na memória (adequado ao limite de 10 MB); se o banco falhar após a gravação do arquivo pode sobrar um arquivo órfão; só PDF/JPG/PNG (sem DICOM nem Word); relatório sem assinatura digital nem PDF gerado no servidor.

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

## Etapa 5 — banco existente

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
