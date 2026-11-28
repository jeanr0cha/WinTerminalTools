---
name: engineer-plan
description: Planejamento de feature. Analisa e cria plano estruturado.
---
<!-- GERADO pelo Liz Framework a partir de .agents/ — não edite aqui; edite a fonte e rode update.sh -->

# Engineer Reason

Este é o comando para iniciar o planejamento de uma funcionalidade.

<arguments>
{argumentos do usuário}
</arguments>

## Análise

Leia os arquivos context.md e architecture.md na pasta .agents/sessions/<feature-slug> se ainda não tiver feito (carregue só as seções necessárias — ver protocolo de leitura em [worklog-protocol.md §4](.agents/knowledge/worklog-protocol.md)).

Sua tarefa agora é criar um plano de implementação detalhado (plan.md) para esta funcionalidade. O objetivo desta documentação é criar uma abordagem de implementação faseada que nos permita construir a funcionalidade incrementalmente, testando cada fase conforme avançamos. E também deve tornar possível retomar o trabalho caso nossa sessão seja interrompida.

O plan.md deve dividir a implementação em fases, cada fase com um pedaço do trabalho que pode ser realizado por um humano em ~2 horas. Cada fase é um **chunk auto-contido** (100–300 linhas): ler a fase N não deve exigir as fases anteriores em contexto.

**Vocabulário de estado (obrigatório):** use os tokens ASCII `[DONE]` / `[ACTIVE]` / `[TODO]` no header de cada fase e tarefa (definidos na [SSOT](.agents/knowledge/gitflow-patterns.md#contrato-de-sessão-de-desenvolvimento) e [worklog-protocol.md §6](.agents/knowledge/worklog-protocol.md)). Emoji é decorativo; o token entre colchetes é o que máquinas leem. Invariante: **exatamente uma** fase `[ACTIVE]`, e ela deve ser igual a `STATE.md.NEXT.phase` — o `STATE.md.NEXT` é o ponteiro **autoritativo** de resume; os badges abaixo são detalhe humano subordinado.

O template para o plan.md é:

<plan>
# [NOME DA FUNCIONALIDADE]

**Consumo estimado total (IA):** <soma das fases; ver "Consumo (IA)" abaixo do template>

Se você está trabalhando nesta funcionalidade, atualize este plan.md E o `STATE.md.NEXT` conforme progride (ver checkpoint em [worklog-protocol.md §7](.agents/knowledge/worklog-protocol.md)).

> **Estado inicial:** num plano recém-criado, a Fase 1 nasce `[ACTIVE]` (igual a `STATE.md.NEXT.phase = 1`) e as demais `[TODO]`. Ao concluir uma fase, marque-a `[DONE]`, promova a próxima a `[ACTIVE]` e atualize o `STATE.md.NEXT` (transição em [worklog-protocol.md §6-7](.agents/knowledge/worklog-protocol.md)). Invariante: **exatamente uma** fase `[ACTIVE]`.

## FASE 1 [ACTIVE]

**Repo:** <nome do repo em .agents/workspace.yaml> · **Validar com:** <comando de teste/lint do repo>
**Consumo estimado:** <saída de `tokens.sh estimate --plain` desta fase>

Detalhes desta parte da funcionalidade

### Uma tarefa que precisa ser feita [ACTIVE]

Detalhes sobre a tarefa

### Uma tarefa que precisa ser feita [TODO]

Detalhes sobre a tarefa

### Comentários:
- (preencher conforme progride) decisões, mudanças de direção, aprendizados

## FASE 2 [TODO]

### Uma tarefa que precisa ser feita [TODO]

Detalhes sobre a tarefa

## FASE 3 [TODO]

### Uma tarefa que precisa ser feita [TODO]

Detalhes sobre a tarefa

</plan>


Dicas:
   - Use repoprompt:search (se disponível) para encontrar arquivos específicos baseados nas respostas de descoberta
   - Use repoprompt:set_selection e repoprompt:read_selected_files (se disponível) para ler código relevante em batch
   - Analise detalhes específicos de implementação
   - Use WebSearch e ou context7 para melhores práticas ou documentação de bibliotecas (se necessário)

No caso desta pesquisa levantar uma nova decisão arquitetural ou contradição com as decisões anteriores, você iniciará uma discussão sobre isso com o humano, concordará com as mudanças e atualizará o documento architecture.md para aquela funcionalidade se necessário.

Se a sessão tiver fichas de spike (`spike-*.md`), use o resultado delas como fato e cite-as nas fases que dependem delas. Se surgir uma dúvida técnica que só um teste responde, pare e proponha um `/engineer-spike` em vez de planejar em cima de um chute.

Este documento também deve anotar quais tarefas precisam ser feitas sequencialmente ou em paralelo.

**Workspace com vários repos:** cada fase pertence a **um** repo (campo `Repo:` no header da fase).
Se uma mudança atravessa repos (ex.: novo endpoint no backend usado pelo frontend), ordene as fases
pelo contrato: primeiro o lado que **fornece** (backend/MCP), depois quem **consome** (frontend), e
escreva o contrato (rota, payload, tipos) na fase do fornecedor para o consumidor ler sem reabrir o
código. O comando de validação de cada fase vem do `workspace.yaml` (`ws.sh get <repo> test`).

**Consumo (IA):** em cada fase, estime com `bash .agents/utils/tokens/tokens.sh estimate --points <pontos da fase> --repos 1 --tool <ferramenta em que você roda> --plain --save --slug <feature-slug> --phase <N>` (se a fase não tiver pontos, divida os da tarefa pelo nº de fases e use o degrau mais próximo da escala 1, 2, 3, 5, 8, 13) e escreva a linha `**Consumo estimado:**` da fase. No topo, some as fases em `**Consumo estimado total (IA):**`. Fase de tamanho **G**: sugira dividi-la. É faixa, não promessa (ver `.agents/shared/token-estimation.md`).

Uma vez que o plan.md esteja finalizado, informe ao humano que você está pronto para prosseguir para o próximo passo.

