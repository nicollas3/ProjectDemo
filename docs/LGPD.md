# LGPD

Etapa 1: minimização (CPF mascarado na interface, logs sem dados clínicos), controle de acesso por organização/papel, trilha de auditoria.

Próximas etapas: entidade `Consent` (finalidade, versão, revogação), exportação de dados, anonimização quando juridicamente aplicável.
As bases legais **não** serão fixadas em código como "consentimento para tudo"; devem ser configuradas conforme orientação jurídica.
Este documento não substitui parecer jurídico.

## Etapa 5 implementada

- `ConsentRecord`: finalidade, texto/versionamento, concessão e revogação.
- Catálogo versionado de finalidades (`ConsentPurposeCatalog`); alteração de texto exige nova versão.
- Portal do paciente em **Privacidade e LGPD** para consultar/revogar consentimentos.
- Exportação JSON do titular com cadastro, consultas, evoluções, medicamentos, sintomas, crises, quedas, questionários, metadados de documentos e consentimentos.
- Lembretes por e-mail podem exigir consentimento ativo `notifications-email` (`Notifications:RequireConsent=true`).
- A exportação gera auditoria e não inclui credenciais/senhas/tokens do Identity.

A revogação de consentimento não implica exclusão automática de registros sujeitos a obrigação legal/regulatória de retenção.
