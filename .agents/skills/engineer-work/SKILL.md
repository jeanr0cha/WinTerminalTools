---
name: engineer-work
description: |
  Continuar trabalho em feature ativa. Lê sessão e identifica próxima fase.
  Atualiza progresso via Task Manager abstraction.
---

# Engineer Work

> **Workspace com vários repos (Liz):** a sessão fica em `.agents/sessions/<slug>/` na raiz do
> workspace. Cada fase do `plan.md` diz o seu `Repo:`. Antes de codar a fase, entre no repo dela
> (`bash .agents/utils/workspace/ws.sh path <repo>`), leia o `AGENTS.md` daquele repo, confira que
> a branch `feature/<slug>` está ativa nele e rode a validação da fase (`ws.sh get <repo> test`) antes
> de marcar `[DONE]`. Commits são feitos **dentro do repo da fase**, nunca na raiz do workspace.

Estamos atualmente trabalhando em uma funcionalidade que está especificada na seguinte pasta:

<folder>
{argumentos do usuário}
</folder>

Para trabalhar nisso, você deve usar o **protocolo de leitura escalonado** (Tier 0→3) — **nunca** faça `cat` da pasta inteira (anti-pattern "Context Dump"; protocolo em [worklog-protocol.md §4](.agents/knowledge/worklog-protocol.md)):

1. **Tier 0 (sempre, ~1KB):** leia **só o `STATE.md`**. Seu `## NEXT` é o ponteiro **autoritativo** — diz fase atual e próximo passo. Não escaneie badges do `plan.md` para decidir.
2. **Tier 1 (sob demanda):** leia **apenas o bloco da fase `[ACTIVE]`** do `plan.md` (a fase nomeada em `STATE.md.NEXT.phase`).
3. **Tier 2 (raro):** abra `architecture.md`/`context.md` **só** se o `## Map` do `STATE.md` indicar que esta fase precisa — e só a seção apontada.
4. **Drift guard:** se `STATE.md.last_checkpoint` for mais antigo que a edição mais recente do `plan.md`, ou se houver mais de uma fase `[ACTIVE]`, **pare e peça reconciliação** ao usuário (não escolha silenciosamente).
5. Apresente ao usuário um plano para abordar a fase `[ACTIVE]`.

## 🔄 **Auto-Update Task Manager**

Mecanismo de sincronização: `.agents/shared/task-manager-auto-update.md` (provedor
ativo via adapter; comentário DUAL detalhado-na-subtask + resumido-na-task;
timestamp + status; offline → registrar em `plan.md`/`notes.md`, sem persistir).

**Gatilho deste comando:** a cada FASE concluída → comentário de progresso +
`updateStatus(subtaskId, 'done')` + atualização do `plan.md` (status, decisões, progresso %).
O **progresso %** é o da derivação `progress` (story points das subtasks feitas ÷ total; sem pontos,
contagem declarada): `bash .agents/utils/task-manager/progress.sh <subtasks.tsv>` — e a linha
`declared:` vai junto do número no comentário da task principal (ver o fragmento acima).

### **🔗 CRITICAL: Phase→Subtask Mapping**
**OBRIGATÓRIO**: Quando uma fase é completada, o sistema deve:
1. **Identificar subtask correspondente** via mapeamento estabelecido no context.md
2. **Atualizar status da subtask** para "done" automaticamente
3. **Documentar conclusão** com timestamp e métricas da fase

### **🗺️ SUBTASK MAPPING STRUCTURE (context.md):**
Formato canônico na [SSOT](.agents/knowledge/gitflow-patterns.md#contrato-de-sessão-de-desenvolvimento) — lido (não redefinido) daqui:
```markdown
## 📋 Phase-Subtask Mapping
- **Phase 1**: "Nome da Fase" → Subtask ID: [subtask-id-1]
- **Phase 2**: "Nome da Fase" → Subtask ID: [subtask-id-2]
```

### **⚡ AUTOMATIC EXECUTION (Via Abstração):**
Quando uma fase é marcada como `[DONE]` no plan.md, o sistema deve **EXECUTAR NESTA ORDEM**:

```typescript
// 1. Obter task manager
const taskManager = getTaskManager();

if (taskManager.isConfigured) {
  // 2. Comentário DETALHADO na SUBTASK
  await taskManager.addComment(subtaskId, `
🔧 FASE COMPLETADA: ${phaseName}

━━━━━━━━━━━━━━

📁 ARQUIVOS MODIFICADOS:
${filesModified.map(f => `   ∟ ${f}`).join('\n')}

🔧 IMPLEMENTAÇÕES:
${implementations.map(impl => `   ▶ ${impl}`).join('\n')}

💡 DECISÕES TÉCNICAS:
${decisions.map(d => `   ∟ ${d}`).join('\n')}

🚀 PRÓXIMOS PASSOS:
   ∟ ${nextPhase}

━━━━━━━━━━━━━━

⏰ Completado: ${timestamp} | 🎯 Status: Done
  `);

  // 3. Atualizar STATUS da SUBTASK
  await taskManager.updateStatus(subtaskId, 'done');

  // 3b. Progresso da task-mãe: DEPOIS do update, com as subtasks relidas (antes, sai defasado em uma).
  //     getSubtasks(mainTaskId) → linhas id<TAB>status<TAB>storyPoints → progress.sh → { percent, method, declared }
  const progress = derive(await taskManager.getSubtasks(mainTaskId)); // bash .agents/utils/task-manager/progress.sh
  const progressLine = progress.method === 'none' ? progress.declared : `${progress.percent}% · ${progress.declared}`;

  // 4. Comentário RESUMIDO na TASK PRINCIPAL
  await taskManager.addComment(mainTaskId, `
📝 PROGRESSO: Fase ${phaseNum}/${totalPhases} Completada
📈 ${progressLine}

✅ ${phaseName} - Concluída
   ∟ Subtask: #${subtaskId}
   ∟ Detalhes: Ver comentário na subtask

🎯 Próximo: Fase ${phaseNum + 1}/${totalPhases} - ${nextPhaseName}

⏰ ${timestamp}
  `);
}
```

## Importante:

Quando você desenvolver o código para a fase atual, use os sub-agentes de desenvolvimento, code-review e teste quando apropriado para preservar o máximo possível do seu contexto.

Toda vez que completar uma fase do plano (**checkpoint** — ver [worklog-protocol.md §7](.agents/knowledge/worklog-protocol.md)):
- **AUTO-UPDATE**: Adicione comentário de progresso via abstração
- **RASTREAMENTO**: Marque checkboxes na description correspondentes aos critérios completados
- Pause e peça ao usuário para validar seu código.
- Faça as mudanças necessárias até ser aprovado
- **Flip de estado**: marque a fase concluída como `[DONE]` no `plan.md` e a próxima como `[ACTIVE]`; adicione comentários úteis (decisões, questões) para quem abordará as próximas fases.
- **Atualize o `STATE.md.NEXT`** (fonte autoritativa): `phase`, `phase_title`, `status`, `next_action` (imperativo literal), `files_in_flight`, `validate_with`, `last_checkpoint`. Mantenha o invariante de **uma só** fase `[ACTIVE]`.
- **Antes de `/compact` ou `/clear`**: faça flush do checkpoint primeiro — após a compactação, reler só o `STATE.md` reorienta instantaneamente.
- Apenas inicie a próxima fase após o usuário concordar que você deve começar.

## 🔗 Referências

- Contrato de worklog (SSOT): [gitflow-patterns.md §Contrato de Sessão](.agents/knowledge/gitflow-patterns.md#contrato-de-sessão-de-desenvolvimento)
- Protocolo de leitura/resume/checkpoint: [worklog-protocol.md](.agents/knowledge/worklog-protocol.md)
- Higiene de contexto: [context-window-optimization.md](.agents/knowledge/context-window-optimization.md)
- Abstração: `.agents/utils/task-manager/`
- Detector: `.agents/utils/task-manager/detector.md`
- Factory: `.agents/utils/task-manager/factory.md`
- Padrões de comentários: `.agents/shared/clickup-patterns.md`

Agora, veja a fase atual de desenvolvimento e forneça um plano ao usuário sobre como abordá-la.
