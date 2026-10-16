# API (Etapa 1, somente leitura)

Autenticação: cookie do Identity (401/403 em JSON para `/api`). JWT para mobile está no roadmap.

- `GET /api/patients?term=` — perfil `ClinicalStaff`; retorna `PatientListItemDto[]`
- `GET /api/patients/{id}` — `PatientDetailsDto` (CPF mascarado)
- `GET /api/appointments?from=&to=&doctorId=` — clínicos veem a organização; paciente vê apenas as próprias

Erros: `ProblemDetails` (404/403) e `ValidationProblemDetails` (400). A API nunca retorna entidades do EF.
Endpoints de escrita e os demais módulos (`medications`, `symptoms`, `seizures`, `questionnaires`, `documents`, `notifications`) virão com seus módulos.
