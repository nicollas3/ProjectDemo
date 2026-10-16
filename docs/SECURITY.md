# Segurança

Implementado: Identity com hash seguro, política de senha (10+ caracteres), lockout (5 tentativas/15 min), e-mail confirmado obrigatório,
antiforgery global, cookie HttpOnly/SameSite/Secure (Secure sempre fora de Development), CSP + nosniff + X-Frame-Options + Referrer/Permissions-Policy,
rate limiting em `/Account/Login`, autorização por papel/política, isolamento por tenant, auditoria sem dados clínicos, secrets fora do código.

Pendências: SRI/hospedagem local de Bootstrap/Chart.js (hoje via CDN), 2FA, JWT/refresh para mobile, forwarded headers atrás de proxy,
revisão de segurança independente e teste de intrusão antes de produção.

## Etapa 2
- Sem auto-cadastro: contas nascem de convite; mensagens genéricas ("E-mail indisponível") e resposta idêntica em "esqueci minha senha" evitam enumeração de usuários.
- Links de e-mail usam `App:PublicBaseUrl` (obrigatória fora de Development) — sem host header injection.
- Tokens de redefinição: Data Protection do Identity, uso único, codificados em Base64Url.
- Desativar usuário/organização atualiza o SecurityStamp; sessões caem em até 1 minuto.
- `LogEmailSender` (que registra links no log) só é registrado com `Email:UseLogSender=true` — nunca habilitar em produção.

## Etapa 3 (dados clínicos)
- Evolução SOAP: leitura/escrita só para `Doctor`; `ClinicAdmin` (gestão) não lê prontuário (mínimo privilégio).
- Sintomas/crises/medicamentos: médico da organização ou o próprio paciente; para paciente o alvo é sempre ele mesmo (ignora `patientId` da URL).
- Toda entidade clínica tem `OrganizationId` + filtro global + guarda de escrita; `ClinicalNoteAddendum` também.
- Auditoria de listagem/visualização/criação/alteração, apenas com metadados (nunca texto clínico).
- “Assinatura” da evolução é um bloqueio lógico (autor + data/hora UTC). Não equivale a assinatura digital ICP-Brasil.

## Etapa 4 (parte 1)
- Instrumentos "aplicados pelo profissional" só podem ser submetidos por médicos (validado no serviço).
- Resultados: médico da organização ou o próprio paciente; paciente que tenta ler resultado de outro recebe "não encontrado".
- Alertas de segurança do dashboard só são montados para médicos.
- Auditoria sem pontuação/respostas.

## Etapa 4 (partes 2 e 3)
- Upload: validação por assinatura de arquivo + extensão + tamanho; chave de armazenamento gerada pelo sistema e validada por regex (sem path traversal); armazenamento fora de wwwroot; download como anexo com `nosniff`.
- Sem antivírus e sem criptografia em nível de aplicação: pendências para produção.
- Relatório restrito a médicos; auditoria de geração/download sem nomes de arquivo ou conteúdo.
