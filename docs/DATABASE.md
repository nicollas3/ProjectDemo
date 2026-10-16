# Banco de dados (SQL Server)

Tabelas (Etapa 1): Organizations, Doctors, Patients, Appointments, AuditLogs + tabelas do Identity (AspNetUsers com `OrganizationId`, `FullName`, `Active`).

- Enums gravados como texto. `Patients.Cpf` (char 11, somente dígitos) é **único por organização**.
- FKs com `Restrict`. `Appointments`: CHECK de duração 5–240 min.
- Índices: `OrganizationId`, `(OrganizationId, Cpf)` único, `ResponsibleDoctorId`, `CreatedAt`, `PatientId`, `(DoctorId, StartsAtUtc)`, `(OrganizationId, StartsAtUtc)`, `AuditLogs(OccurredAt)`.
- `CreatedAt/UpdatedAt` preenchidos automaticamente no `SaveChanges`.
- Entidades planejadas (MedicalRecord, Medication, SeizureEvent, Questionnaire*, Document, Consent, Plan, Subscription etc.) entram nas próximas etapas.

## Etapa 3
Tabelas: ClinicalNotes (SOAP, status, SignedAtUtc), ClinicalNoteAddenda, Medications, SymptomRecords (CHECK intensidade 0–10), SeizureEvents (CHECK duração 1–86400 s).
Todas com `OrganizationId` indexado, `CreatedAt` indexado e índices `(PatientId, OccurredAtUtc)` para o histórico. FKs `Restrict` (prontuário não é apagado em cascata).

## Etapa 4 (parte 1)
FallEvents (circunstância, lesão, atendimento) e QuestionnaireResponses (chave do instrumento, respostas em JSON, pontuação, faixa, SafetyFlag).
Índice `(OrganizationId, SafetyFlag, AnsweredAtUtc)` apoia o alerta do dashboard. Definições dos instrumentos ficam em código (`QuestionnaireCatalog`).

## Etapa 4 (partes 2 e 3)
PatientDocuments: metadados (título, categoria, nome original sanitizado, tipo detectado, tamanho, SHA-256, `StorageKey` único, exclusão lógica). O binário fica no armazenamento de arquivos (`Storage:RootPath`), fora do banco.
