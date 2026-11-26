---
name: engineer-start
description: |
  Iniciar desenvolvimento de feature. Cria sessão e analisa tasks.
  Suporta múltiplos gerenciadores via TASK_MANAGER_PROVIDER.
---

# Engineer Start

Este é o comando para iniciar o desenvolvimento de uma funcionalidade.

## 🚨 PASSO 0 (OBRIGATÓRIO): Detectar Provedor

Detectar e validar o provedor ativo **antes de qualquer ação**, seguindo o
fragmento canônico `.agents/shared/task-manager-provider-detection.md`: obter o provider pelo helper (`bash .agents/utils/task-manager/env-check.sh --provider`), nunca abrindo o `.env`,
validar a variável obrigatória do provedor e aplicar o fallback gracioso em modo
offline. Quando houver `task-id` salvo/recebido, validar a compatibilidade dele
com o provedor ativo (item 3 do fragmento) — em caso de divergência, avisar o
usuário antes de prosseguir (ver exemplo em **Validação de ID Incompatível**).

## Configuração (workspace com vários repos)

O Liz roda na **raiz do workspace**; o código vive nos repos listados em `.agents/workspace.yaml`
(`bash .agents/utils/workspace/ws.sh list`). Uma tarefa pode tocar um ou mais repos.

1. **Descubra os repos afetados.** Leia a tarefa (spec em `docs/specs/` ou task do gerenciador) e
   decida quais repos ela toca (ex.: `backend` + `frontend`). Na dúvida, pergunte. Registre a lista
   no `context.md` na seção `## Repos afetados` (nome, papel, branch).
2. **Branch em cada repo afetado.** Em cada um: se não estiver numa feature branch, peça permissão
   para criar `feature/<feature-slug>` a partir da branch de integração do repo
   (`ws.sh get <repo> default_branch`, padrão `develop` se existir, senão `main`). Use **o mesmo
   nome de branch** em todos os repos da tarefa. Nunca trabalhe direto na `main`.
   Comando: `git -C "$(bash .agents/utils/workspace/ws.sh path <repo>)" switch -c feature/<feature-slug>`
3. **Sessão (worklog) na raiz do workspace:** `.agents/sessions/<feature-slug>/`. É uma sessão só,
   mesmo quando a tarefa toca vários repos.
4. Peça ao usuário o input para esta sessão (você receberá uma ou mais tasks para trabalhar).
5. Leia o `AGENTS.md` de cada repo afetado antes de propor qualquer mudança: ele diz a stack, os
   comandos e as regras daquele repo.

> **Worklog vs. transcript**: o worklog (`.agents/sessions/<slug>/`) é o estado durável em arquivo; o transcript é a conversa nativa do Claude Code (`claude --resume`). A estrutura do worklog é definida pela **SSOT** — [gitflow-patterns.md §Contrato de Sessão](.agents/knowledge/gitflow-patterns.md#contrato-de-sessão-de-desenvolvimento); a mecânica de resume/leitura, por [worklog-protocol.md](.agents/knowledge/worklog-protocol.md). Não redefina a estrutura aqui.

## Análise

Analise as tasks, pais e filhos se necessário, e construa um entendimento inicial do que precisa ser desenvolvido. 

### **📋 Análise via Task Manager:**
**IMPORTANTE**: Use a abstração para ler tasks independente do provedor:

```typescript
// Via abstração - funciona para qualquer provedor (ClickUp, Asana, Linear, Zoho Projects)
const task = await taskManager.getTask(taskId);
const subtasks = await taskManager.getSubtasks(taskId);

// Documentar no context.md
console.log(`Provider: ${task.provider}`);
console.log(`Task: ${task.name}`);
console.log(`URL: ${task.url}`);
```

### **🎲 Validação de Story Points (Opcional mas Recomendado):**

**Antes de iniciar o desenvolvimento**, aplicar o gate de estimativa seguindo o
fragmento canônico `.agents/shared/story-points-gate.md`: sem estimativa → oferecer
estimar (`/product/estimate` ou `@story-points-framework-specialist`); épico
(> 13 pontos) → alertar e oferecer quebrar (`/product/refine`), parando se o
usuário não confirmar; estimativa válida (1–13) → confirmar e seguir.

### **🔍 Validação de ID Incompatível:**
Se o task-id salvo for de um provedor diferente do configurado:

```
⚠️ INCOMPATIBILIDADE DETECTADA
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Task ID: "86adfe9eb"
Provedor detectado: clickup
Provedor configurado: asana

💡 Ações sugeridas:
   1. Altere TASK_MANAGER_PROVIDER para 'clickup' no .env
   2. Ou limpe a sessão atual e crie uma nova task
   3. Execute /meta/setup-integration para reconfigurar
```

### **🔍 Questões de Análise:**
Pense cuidadosamente sobre o que é solicitado, certifique-se de entender exatamente:
    - Por que isso está sendo construído (contexto)
    - Qual é o resultado esperado para esta task? (objetivo)
    - Como deve ser construído, apenas direcionalmente, não em detalhes (abordagem)
    - Se requer o uso de novas APIs/ferramentas, você as entende?
    - Como deve ser testado?
    - Quais são as dependências?
    - Quais são as restrições?

Após refletir sobre essas questões, formule as 3-5 clarificações mais importantes necessárias para completar a tarefa. Pergunte essas questões ao humano, enquanto também fornece seu entendimento e sugestões.

Depois de obter as respostas do humano, considere se precisa fazer mais perguntas. Se sim, faça mais perguntas ao humano.

Uma vez que tenha um bom entendimento do que está sendo construído, salve-o no arquivo .agents/sessions/<feature-slug>/context.md e peça ao humano para revisar.

Se o humano concordar com seu entendimento, você pode prosseguir para o próximo passo. Caso contrário, continue iterando juntos até obter aprovação explícita para seguir em frente.

Se algo que você discutiu aqui afeta o que foi escrito nos requisitos, peça permissão ao humano para editar esses requisitos e fazer ajustes.

## Arquitetura

Dado seu entendimento do que será construído, você agora procederá ao desenvolvimento da arquitetura da funcionalidade.

É aqui que você colocará seu chapéu de pensamento ultra e considerará o melhor caminho para construir a funcionalidade, considerando também os padrões e melhores práticas deste projeto.

Seu documento de arquitetura deve incluir:
    - Uma visão geral de alto nível do sistema (antes e depois da mudança), com diagrama C4 em Mermaid do nível que muda (containers e, se preciso, componentes)
    - Componentes afetados e suas relações, dependências
    - Padrões e melhores práticas que serão mantidos ou introduzidos
    - Dependências externas
    - Restrições e suposições
    - Trade-offs e alternativas
    - Decisões caras de desfazer ou que afetam mais de um repo, marcadas como candidatas a ADR
    - Dúvidas abertas que só um teste responde
    - Lista dos principais arquivos a serem editados/criados

O formato dos diagramas e dos ADRs está em `.agents/knowledge/c4-adr-patterns.md`. Se o desenho for grande (vários repos, contrato novo, mudança de dados), delegue o rascunho ao `@software-architect` e revise o que ele trouxer.

Se uma dúvida aberta trava o desenho ("a lib aguenta?", "a API de terceiros permite?"), não chute: proponha ao humano um `/engineer-spike` antes do `/engineer-plan`.

Uma vez que tenha um bom entendimento do que está sendo construído, salve-o no arquivo .agents/sessions/<feature-slug>/architecture.md e peça ao humano para revisar.

## STATE.md — índice de resume (OBRIGATÓRIO)

Após `context.md` e `architecture.md` aprovados, crie o `.agents/sessions/<feature-slug>/STATE.md` — o índice Tier-0 (~1KB) que torna o resume barato e determinístico. Esquema completo em [worklog-protocol.md §3](.agents/knowledge/worklog-protocol.md). Mínimo:

```markdown
# STATE — <feature-slug>

## Objective
<uma frase: o quê + porquê — espelha context.md, não duplica>

## Constraints
- <invariantes; "não toque em X">

## Map
- architecture.md → <seção relevante por fase>
- context.md      → background; pular no resume
- plan.md         → ler SÓ o bloco da fase [ACTIVE]
- task-manager    → main: <id ou "—"> · provider: <provider>

## NEXT
phase: 1
phase_title: <título da Fase 1>
status: todo
next_action: "<imperativo literal do primeiro passo>"
blocked_by: none
files_in_flight: []
validate_with: "<comando de validação>"
last_checkpoint: <YYYY-MM-DDThh:mmZ>

## Native transcript
resume_command: claude --resume <id>   # conveniência opcional (colada pelo usuário/hook)
```

> Ordene `STATE.md` com **prefixo estável → cauda volátil** (Objective/Constraints/Map antes de NEXT) para amortizar o prompt cache. `STATE.md.NEXT` é o ponteiro autoritativo de resume; os badges do `plan.md` são detalhe subordinado.

## 🔄 **Auto-Update Task Manager**

Mecanismo de sincronização: `.agents/shared/task-manager-auto-update.md`.
**Gatilho deste comando:** ao iniciar a sessão de desenvolvimento.

### **✅ Comentário de início (template):**
```typescript
// Via abstração - funciona para qualquer provedor
if (taskManager.isConfigured && taskId) {
  // Atualizar status
  await taskManager.updateStatus(taskId, 'in_progress');
  
  // Adicionar comentário de início
  await taskManager.addComment(taskId, `
🚀 DESENVOLVIMENTO INICIADO

━━━━━━━━━━━━━━━━━━━━━━━━

🏗️ SESSÃO ATIVADA:
   ▶ Branch: feature/[slug]
   ▶ Sessão: .agents/sessions/[slug]/
   ▶ Provider: ${taskManager.provider}

📋 PLANO DE IMPLEMENTAÇÃO:
   ∟ Fase 1: [Descrição]
   ∟ Fase N: [Descrição]

━━━━━━━━━━━━━━━━━━━━━━━━

⏰ Iniciado: ${new Date().toISOString()}
  `);
}
```

### **📋 Identificação da Task:**
Identificação e validação conforme `.agents/shared/task-manager-auto-update.md`
(context.md / task-id / branch). Específico do `start`: usar `taskManager.getTask(taskId)`
para obter a estrutura completa antes de criar o mapeamento fase→subtask abaixo.

### **🗺️ OBRIGATÓRIO: Criar Phase-Subtask Mapping**
Quando subtasks existem, o sistema deve **automaticamente**:
1. **Detectar subtasks** via `taskManager.getSubtasks(taskId)`
2. **Correlacionar com fases** do plan.md (por ordem ou nome)
3. **Salvar mapeamento** na seção `## 📋 Phase-Subtask Mapping` do `context.md` (header e formato canônicos na [SSOT](.agents/knowledge/gitflow-patterns.md#contrato-de-sessão-de-desenvolvimento)) para uso pelo `/engineer/work`
4. **Validar correlação** e alertar se houver mismatch

## Pesquisa

Se você não tem certeza de como uma biblioteca específica funciona, você pode usar Context7 e Perplexity para buscar informações sobre ela. Então, não tente adivinhar.

## 🔗 Referências

- Abstração: `.agents/utils/task-manager/`
- Detector: `.agents/utils/task-manager/detector.md`
- Factory: `.agents/utils/task-manager/factory.md`

<feature-slug>
{argumentos do usuário}
</feature-slug>
