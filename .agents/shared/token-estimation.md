# 🧮 Estimativa de consumo de tokens — trecho compartilhado

> Citado por `/product-feature`, `/engineer-plan`, `/engineer-work`, `/engineer-pre-pr` e `/engineer-pr`.
> Método e motivos: `.agents/knowledge/token-estimation.md`. Ferramenta: `.agents/utils/tokens/tokens.sh`.

## Estimar

Com os **story points** definidos (da tarefa ou da fase):

```bash
bash .agents/utils/tokens/tokens.sh estimate --points <N> --repos <nº de repos afetados> --tool <ferramenta>
```

- `--tool` é a ferramenta **em que você está rodando**: `claude`, `codex` ou `antigravity` (na dúvida,
  `other`). A calibração é separada por ferramenta.
- `--complexity alta|baixa` só quando houver motivo claro (tecnologia nova ou requisito nebuloso / padrão
  já existente). O padrão é `normal`.
- Para colar numa spec ou plano, use `--plain` (uma linha). Para guardar, `--save --slug <tarefa>
  [--phase <N>]` (sem `--phase`, vale para a tarefa inteira).
- 20 pontos ou mais: o script recusa (épico). Quebre em histórias menores antes de estimar.

Mostre ao humano **o bloco inteiro** (ele traz entrada, saída, tamanho e o aviso de que é faixa). Nunca
resuma como um número só.

## Registrar o consumo real (opcional)

Ao fim de uma fase, **pergunte uma vez**: "Quer registrar o consumo desta fase? Cole o uso da sessão
(entrada e saída) ou diga *pular*." Se vier:

```bash
bash .agents/utils/tokens/tokens.sh actual --slug <tarefa> --phase <N> --in <entrada> --out <saída>
```

Depois mostre uma linha "estimado × real" (`tokens.sh report --slug <tarefa>`). Se o humano pular,
siga normalmente: **nunca** bloqueie fase, PR ou tarefa por falta desse registro.

## Regras

- É **faixa, não promessa**. Diga sempre se é `não calibrado`.
- **Não** converta em % do limite do plano nem em dinheiro, e não invente números fora do script.
- **Não** leia nem copie o conteúdo de conversas: só os números que o humano informar.
