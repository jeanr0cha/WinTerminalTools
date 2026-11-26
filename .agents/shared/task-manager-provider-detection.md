# 🚨 Detecção de Provedor — Task Manager (PASSO 0 OBRIGATÓRIO)

> Fragmento canônico de detecção de provedor reutilizado por comandos que operam
> com tasks. Citar como `.agents/shared/task-manager-provider-detection.md`.
> A lógica de detecção/parsing vive no adapter: [`.agents/utils/task-manager/detector.md`](.agents/utils/task-manager/detector.md).

**⚠️ CRÍTICO — EXECUTAR ANTES DE QUALQUER OUTRA AÇÃO. NUNCA assumir o provedor.**

1. **Obter o provider sem ler o `.env`**: `bash .agents/utils/task-manager/env-check.sh --provider` (e `--check` para a
   presença das chaves) — **nunca** abrir o `.env` com a ferramenta de leitura de arquivo, que entrega os segredos ao modelo. Extrair `TASK_MANAGER_PROVIDER`
   (valores: `jira` | `clickup` | `asana` | `linear` | `zoho` | `none`). A resposta `ilegivel` (rc 4) quer dizer que o
   `.env` existe e não pôde ser lido, em geral por sandbox: avise, **não** trate como ausente nem assuma `none`. A resposta
   `worktree-sem-env` (rc 5) quer dizer que você está numa worktree e o `.env` mora no checkout principal: rode com `--env`.
2. **Validar a variável obrigatória do provedor ativo:**

   | Provedor | Variável obrigatória | Adapter (transporte: REST API default; MCP opcional) |
   |----------|----------------------|----------------------------|
   | `jira` | `JIRA_HOST`, `JIRA_EMAIL`, `JIRA_API_TOKEN` | `.agents/utils/task-manager/adapters/jira.md` |
   | `clickup` | `CLICKUP_API_TOKEN` | `.agents/utils/task-manager/adapters/clickup.md` |
   | `asana` | `ASANA_ACCESS_TOKEN` | `.agents/utils/task-manager/adapters/asana.md` |
   | `linear` | `LINEAR_API_KEY` | `.agents/utils/task-manager/adapters/linear.md` |
   | `zoho` | `ZOHO_CLIENT_ID`, `ZOHO_CLIENT_SECRET`, `ZOHO_PORTAL_ID` | `.agents/utils/task-manager/adapters/zoho.md` |
   | `none` / ausente | — | modo offline (sessões locais em `.agents/sessions/`) |

3. **(Quando o comando recebe um `task-id`)** Validar compatibilidade do task-id
   com o provedor ativo via `detectProviderFromTaskId` / `validateProviderMatch` —
   se houver incompatibilidade, avisar o usuário antes de prosseguir.
4. **Fallback gracioso:** se a variável obrigatória faltar, avisar em pt-BR qual
   variável está ausente, sugerir a configuração no `.env` (ver `.agents/utils/task-manager/README.md`) e seguir em **modo offline**
   (usar apenas o contexto local da sessão, sem chamadas de API). Não inventar
   valores nem assumir outro provedor.

> Detalhes de detecção, parsing do `.env` e validação de ID:
> [`.agents/utils/task-manager/detector.md`](.agents/utils/task-manager/detector.md).
