---
name: engineer-pre-pr
description: Validação completa antes do PR. Verifica padrões e qualidade.
---

# Pre-PR - Validação Completa Antes do Pull Request

> **Workspace com vários repos (Liz):** rode o checklist abaixo **em cada repo listado em
> `## Repos afetados` do `context.md`**, com os comandos daquele repo (`ws.sh get <repo> test|lint|build`).
> Se a mudança atravessa repos, confira também o contrato entre eles (rota, payload, tipos) dos dois lados.

Estamos nos aproximando de finalizar o trabalho nesta branch e nos preparar para um pull request. Agora, é hora de fazer verificações finais e limpezas para garantir que estamos alinhados com nossos padrões e objetivos.

## 🔄 **Auto-Update do Task Manager**

Mecanismo de sincronização: `.agents/shared/task-manager-auto-update.md` (provedor
ativo via adapter; comentário formatado por provider; timestamp + status; offline
→ gerar relatório local, sem persistir).

**Gatilho deste comando:** durante a preparação para PR.

### **✅ Específico do pre-PR:**
- Tag `ready-for-pr` quando todas as verificações passam; `needs-fixes` se alguma falha.
- Progresso estimado ~90% (quase pronto).

### **💬 Payload do comentário:**
Resultado da validação de critérios de aceitação (completo? cobertura? critérios
pendentes?), checks técnicos (meta specs, code review, testes) e indicador `readyForPR`.

## Checklist de Preparação:

### ✅ Validação de Critérios de Aceitação:
1. **Extrair critérios** - Ler checkboxes da description da task/subtask
2. **Validar cobertura** - Confirmar que TODOS os checkboxes estão marcados `[x]`
3. **Gerar relatório** - Criar lista de critérios validados
4. **Bloquear se incompleto** - Se algum critério não estiver marcado, indicar no comentário

### 🔧 Validações Técnicas (fan-out paralelo):

Os quatro agentes abaixo são **independentes** — execute-os como uma **orquestração em paralelo** (fan-out) e depois **consolide** o feedback num relatório único (fan-in). Padrão na skill `onion-orchestration` e na KB `agent-orchestration`.

**Fan-out (paralelo)** — dispare simultaneamente, cada um com saída estruturada:
- `branch-metaspec-checker` — alinhamento da branch com as meta-specs do projeto.
- `branch-code-reviewer` — qualidade do código, pronto para lançar.
- `branch-documentation-writer` — documentação do projeto atualizada.
- `branch-test-planner` — testes finalizados para a branch.

**Fan-in (consolidação)** — mescle os quatro retornos num **relatório único** de pré-PR, deduplicando achados e ordenando por severidade.

> **Fallback sequencial:** se o substrato de fan-out paralelo não estiver disponível, invoque os quatro agentes em sequência (1→4) e consolide ao final — mesmo resultado, mais lento. Padrão canônico de degradação: `.agents/shared/orchestration-fallback.md`.

### 📋 AUTO-UPDATE:
5. **Validar critérios de aceitação** - Verificar todos os checkboxes
6. **Adicionar comentário de preparação** no Task Manager configurado automaticamente (conforme `TASK_MANAGER_PROVIDER`)
7. **Aplicar tags** (ready-for-pr ou needs-fixes)
8. **Atualizar progresso** pela derivação `progress` (`bash .agents/utils/task-manager/progress.sh <subtasks.tsv>`), com a linha `declared:` no comentário — nunca um % fixo à mão (até 2026-10-11 este passo gravava 90%)

Se a sessão tiver consumo registrado, inclua no relatório o resumo de `bash .agents/utils/tokens/tokens.sh report --slug <feature-slug>`; sem dados, não imprima nada.

Você também precisará lidar com todo o feedback que esses agentes fornecerem e fazer mudanças e correções conforme necessário.

Uma vez terminado E todos os critérios de aceitação validados, me avise e peça minha permissão para abrir o Pull Request.

