# tokens.sh — estimativa e histórico de consumo de tokens

Estima quanto de IA uma tarefa (ou fase) vai consumir, **em faixa**, e guarda o consumo real para as
próximas estimativas ficarem melhores. Só bash + awk (funciona no Git Bash).

```bash
bash .agents/utils/tokens/tokens.sh estimate --points 5 --repos 2 --tool claude
bash .agents/utils/tokens/tokens.sh estimate --points 3 --slug login --phase 1 --tool claude --save
bash .agents/utils/tokens/tokens.sh actual --slug login --phase 1 --in 450k --out 22k
bash .agents/utils/tokens/tokens.sh report --slug login
bash .agents/utils/tokens/tokens.sh calibrate --tool claude
bash .agents/utils/tokens/tokens.sh history
```

| Comando | O que faz |
|---|---|
| `estimate` | Mostra a faixa de **entrada** e de **saída** e o tamanho (P/M/G). Com `--save --slug`, grava a estimativa. `--plain` imprime uma linha só, para colar em plano ou spec. |
| `actual` | Registra o consumo real (`--in`, `--out`, opcional `--cache`). Se já houver estimativa da fase, atualiza a mesma linha. |
| `report` | Estimado × real por fase e no total, com "dentro/acima/abaixo da faixa". |
| `calibrate` | Mostra, por ferramenta, quantos registros há e as razões de tokens por story point. |
| `history` | Lista o histórico. |

Opções do `estimate`: `--points N` (obrigatório), `--repos R` (repos afetados, padrão 1),
`--tool claude|codex|antigravity|other`, `--complexity baixa|normal|alta`, `--slug`, `--phase`
(número ou `all`), `--model`, `--save`, `--plain`. Números aceitam `k` e `m` (`120k`, `1.2m`).

## Como a estimativa é feita

- **Até 4 registros** da mesma ferramenta: usa uma tabela de **hipóteses** por story point
  (`não calibrado`). Os ajustes valem só aqui: +25% por repo além do primeiro (máx. ×2); complexidade
  `alta` ×1,3; `baixa` ×0,75.
- **Com 5 ou mais registros** (os 20 mais recentes, ignorando os que têm `note` começando por
  `outlier`): usa o 1º e o 3º quartil de `consumo real ÷ story points` do próprio histórico.
  A faixa nunca é mais estreita que ±15% da mediana.
- Épico (20 pontos ou mais) é recusado: quebre em histórias menores.
- **Não** converte tokens em porcentagem do seu plano nem em dinheiro, de propósito: os limites
  variam e não são públicos em tokens.

## O histórico

`.agents/token-history.tsv` na raiz do workspace (criado na primeira gravação). É dado do projeto:
o `update.sh` não o apaga. Colunas, separadas por TAB:

`date`, `slug`, `phase`, `points`, `tool`, `model`, `est_in_min`, `est_in_max`, `est_out_min`,
`est_out_max`, `real_in`, `real_out`, `real_cache`, `source`, `basis`, `note`.

A chave é `(slug, phase, tool)`: registrar de novo atualiza a linha, não duplica. A gravação é atômica.
Mais detalhes do método em [`.agents/knowledge/token-estimation.md`](../../knowledge/token-estimation.md).
