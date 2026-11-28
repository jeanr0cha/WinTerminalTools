#!/usr/bin/env bash
# tokens.sh — estimativa e histórico de consumo de tokens (faixas, nunca promessas).
#
# Uso:
#   tokens.sh estimate  --points N [--repos R] [--tool T] [--complexity baixa|normal|alta]
#                       [--slug S] [--phase P] [--model M] [--save] [--plain]
#   tokens.sh actual    --slug S [--phase P] --in N --out N [--cache N] [--points N] [--tool T]
#                       [--model M] [--source manual|auto] [--note "..."]
#   tokens.sh report    [--slug S]
#   tokens.sh calibrate [--tool T]
#   tokens.sh history   [--slug S]
#
# Números aceitam sufixos k e m (120k, 1.2m) e vírgula ou ponto decimal.
# O histórico fica em .agents/token-history.tsv (dado do projeto; o update.sh não o apaga).
# Não converte tokens em % de limite do plano nem em dinheiro: de propósito.
#
# Só bash + awk (funciona no Git Bash do Windows).
set -euo pipefail
export LC_ALL=C

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
HIST="${LIZ_TOKEN_HISTORY:-$ROOT/.agents/token-history.tsv}"
HEADER=$'# date\tslug\tphase\tpoints\ttool\tmodel\test_in_min\test_in_max\test_out_min\test_out_max\treal_in\treal_out\treal_cache\tsource\tbasis\tnote'
MIN_CALIB=5      # registros da mesma ferramenta para trocar a hipótese pela razão real
MAX_CALIB=20     # só os registros mais recentes entram na calibração

die() { echo "tokens.sh: $1" >&2; exit "${2:-1}"; }
usage() { sed -n '2,15p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; }

# ---- funções de awk compartilhadas (formatação, arredondamento, quartis) -------------------
AWKLIB='
function sig2(x,   e, p) { if (x <= 0) return 0; e = int(log(x) / log(10) + 1e-9); p = 10 ^ (e - 1); return int(x / p + 0.5) * p }
function trim1(x,   s) { s = sprintf("%.1f", x); sub(/\.0$/, "", s); sub(/\./, ",", s); return s }
function flong(n,   v) {
  if (n >= 1000000) { v = n / 1000000; return trim1(v) (v >= 2 ? " milhões" : " milhão") }
  if (n >= 1000) return trim1(n / 1000) " mil"
  return sprintf("%d", n)
}
function fshort(n,   s) {
  if (n >= 1000000) { s = sprintf("%.1f", n / 1000000); sub(/\.0$/, "", s); return s "m" }
  if (n >= 10000) return sprintf("%.0f", n / 1000) "k"
  if (n >= 1000) { s = sprintf("%.1f", n / 1000); sub(/\.0$/, "", s); return s "k" }
  return sprintf("%d", n)
}
function sortn(a, n,   i, j, t) { for (i = 2; i <= n; i++) { t = a[i]; for (j = i - 1; j >= 1 && a[j] > t; j--) a[j + 1] = a[j]; a[j + 1] = t } }
function quant(a, n, q,   pos, lo, fr) { pos = (n - 1) * q; lo = int(pos); fr = pos - lo; if (lo + 1 > n - 1) return a[n]; return a[lo + 1] + fr * (a[lo + 2] - a[lo + 1]) }
'

# ---- validação de argumentos ------------------------------------------------------------------
num() { # num <texto> → inteiro (ex.: 120k → 120000; 1,5m → 1500000). Falha se inválido.
  local t; t="$(printf '%s' "$1" | tr 'A-Z' 'a-z' | tr -d ' ' | tr ',' '.')"
  printf '%s' "$t" | awk '{
    if ($0 !~ /^[0-9]+(\.[0-9]+)?[km]?$/) exit 1
    m = 1; s = $0
    if (s ~ /k$/) { m = 1000; sub(/k$/, "", s) } else if (s ~ /m$/) { m = 1000000; sub(/m$/, "", s) }
    printf "%d", s * m + 0.5 }'
}
need_num() { local v; v="$(num "$2")" || die "valor inválido em $1: \"$2\". Use um número, com k ou m se quiser (ex.: 120k, 1.2m)."; printf '%s' "$v"; }
need_int() { case "$2" in ''|*[!0-9]*) die "$1 precisa ser um número inteiro positivo (recebi \"$2\")." ;; esac; [ "$2" -gt 0 ] || die "$1 precisa ser maior que zero."; printf '%s' "$2"; }
check_tool() { case "$1" in claude|codex|antigravity|other) ;; *) die "ferramenta inválida: \"$1\". Use: claude, codex, antigravity ou other." ;; esac; }
check_phase() { case "$1" in all) ;; ''|*[!0-9]*) die "--phase deve ser um número ou \"all\" (recebi \"$1\")." ;; esac; }
opt_val() { [ $# -ge 2 ] || die "a opção $1 precisa de um valor."; }

# ---- hipóteses por story point (ver .agents/knowledge/token-estimation.md) --------------------
# Atenção: são HIPÓTESES, não medições. Valem até haver registros reais (calibração).
step_for() { # step_for <pontos> → degrau da escala (1 2 3 5 8 13) ou "epic"
  local p="$1" s
  [ "$p" -ge 20 ] && { echo epic; return; }
  for s in 1 2 3 5 8 13; do [ "$p" -le "$s" ] && { echo "$s"; return; }; done
  echo 13
}
prior_for() { # prior_for <degrau> → "entrada_min entrada_max saida_min saida_max"
  case "$1" in
    1)  echo "50000 150000 3000 8000" ;;
    2)  echo "100000 300000 6000 15000" ;;
    3)  echo "200000 500000 10000 25000" ;;
    5)  echo "400000 1000000 20000 50000" ;;
    8)  echo "800000 2000000 40000 90000" ;;
    13) echo "1500000 4000000 70000 150000" ;;
  esac
}

# ---- calibração --------------------------------------------------------------------------------
calib_stats() { # calib_stats <tool> → "N q1_in med_in q3_in q1_out med_out q3_out" (razões por ponto)
  if [ ! -f "$HIST" ]; then echo "0 0 0 0 0 0 0"; return; fi
  awk -F'\t' -v tool="$1" -v maxn="$MAX_CALIB" "$AWKLIB"'
    /^#/ || NF == 0 { next }
    $5 == tool && ($4 + 0) > 0 && $11 != "" && $12 != "" && $16 !~ /^outlier/ { n++; ri[n] = $11 / $4; ro[n] = $12 / $4 }
    END {
      if (n == 0) { print "0 0 0 0 0 0 0"; exit }
      s = (n > maxn) ? n - maxn + 1 : 1; m = 0
      for (i = s; i <= n; i++) { m++; a[m] = ri[i]; b[m] = ro[i] }
      sortn(a, m); sortn(b, m)
      printf "%d %.4f %.4f %.4f %.4f %.4f %.4f\n", m, quant(a, m, .25), quant(a, m, .5), quant(a, m, .75), quant(b, m, .25), quant(b, m, .5), quant(b, m, .75)
    }' "$HIST"
}

# ---- gravação atômica no histórico -------------------------------------------------------------
# Variáveis T_* (ambiente) = campos a gravar; campo vazio = não mexe. Chave: (slug, phase, tool).
upsert() {
  local dir tmp; dir="$(dirname "$HIST")"; mkdir -p "$dir"
  tmp="$(mktemp "$dir/.token-history.XXXXXX")"
  { [ -f "$HIST" ] || printf '%s\n' "$HEADER"; [ ! -f "$HIST" ] || cat "$HIST"; } > "$tmp.in"
  awk -F'\t' 'BEGIN { OFS = "\t" }
    function setf(i, v) { if (v != "") $i = v }
    function apply(   ) {
      NF = 16
      if (ENVIRON["T_UPDDATE"] == "1") setf(1, ENVIRON["T_DATE"])
      setf(4, ENVIRON["T_POINTS"]); setf(6, ENVIRON["T_MODEL"])
      setf(7, ENVIRON["T_EIMIN"]); setf(8, ENVIRON["T_EIMAX"]); setf(9, ENVIRON["T_EOMIN"]); setf(10, ENVIRON["T_EOMAX"])
      setf(11, ENVIRON["T_RIN"]); setf(12, ENVIRON["T_ROUT"]); setf(13, ENVIRON["T_RCACHE"])
      setf(14, ENVIRON["T_SOURCE"]); setf(15, ENVIRON["T_BASIS"]); setf(16, ENVIRON["T_NOTE"])
    }
    /^#/ || NF == 0 { print; next }
    $2 == ENVIRON["T_SLUG"] && $3 == ENVIRON["T_PHASE"] && $5 == ENVIRON["T_TOOL"] { found = 1; apply(); print; next }
    { print }
    END { if (!found) { $0 = ""; NF = 16; $1 = ENVIRON["T_DATE"]; $2 = ENVIRON["T_SLUG"]; $3 = ENVIRON["T_PHASE"]; $5 = ENVIRON["T_TOOL"]; apply(); print } }
  ' "$tmp.in" > "$tmp" || { rm -f "$tmp" "$tmp.in"; die "não consegui gravar o histórico em $HIST." 2; }
  rm -f "$tmp.in"
  mv "$tmp" "$HIST" || { rm -f "$tmp"; die "não consegui gravar o histórico em $HIST." 2; }
}

# ---- subcomandos -------------------------------------------------------------------------------
cmd_estimate() {
  local points="" repos=1 tool=other cx=normal slug="" phase=all model="" save=0 plain=0
  while [ $# -gt 0 ]; do case "$1" in
    --points) opt_val "$@"; points="$(need_int --points "$2")"; shift 2 ;;
    --repos) opt_val "$@"; repos="$(need_int --repos "$2")"; shift 2 ;;
    --tool) opt_val "$@"; tool="$2"; shift 2 ;;
    --complexity) opt_val "$@"; cx="$2"; shift 2 ;;
    --slug) opt_val "$@"; slug="$2"; shift 2 ;;
    --phase) opt_val "$@"; phase="$2"; shift 2 ;;
    --model) opt_val "$@"; model="$2"; shift 2 ;;
    --save) save=1; shift ;;
    --plain) plain=1; shift ;;
    *) die "opção desconhecida em estimate: $1 (veja: tokens.sh --help)" ;;
  esac; done
  [ -n "$points" ] || die "informe os story points: tokens.sh estimate --points N"
  check_tool "$tool"; check_phase "$phase"
  case "$cx" in baixa|normal|alta) ;; *) die "--complexity deve ser baixa, normal ou alta (recebi \"$cx\")." ;; esac
  [ "$save" -eq 0 ] || [ -n "$slug" ] || die "--save precisa de --slug <tarefa>."

  local step; step="$(step_for "$points")"
  if [ "$step" = epic ]; then
    die "$points pontos é um épico: não dá para estimar com confiança. Quebre em histórias menores (até 13 pontos) e estime cada uma."
  fi
  local warn=""
  [ "$step" = "$points" ] || warn="$points pontos está fora da escala (1, 2, 3, 5, 8, 13): usei o degrau $step."

  local n q1i mi q3i q1o mo q3o
  read -r n q1i mi q3i q1o mo q3o <<< "$(calib_stats "$tool")"
  local basis typ_in="" typ_out="" imin imax omin omax
  if [ "$n" -ge "$MIN_CALIB" ]; then
    basis="calibrated:$n"
    read -r imin imax omin omax typ_in typ_out <<< "$(awk -v p="$points" -v a="$q1i" -v b="$q3i" -v c="$q1o" -v d="$q3o" -v mi="$mi" -v mo="$mo" "$AWKLIB"'BEGIN {
      i1 = a * p; i2 = b * p; o1 = c * p; o2 = d * p
      if (i2 < i1 * 1.2) { i1 = mi * p * 0.85; i2 = mi * p * 1.15 }     # faixa mínima de ±15%
      if (o2 < o1 * 1.2) { o1 = mo * p * 0.85; o2 = mo * p * 1.15 }
      printf "%d %d %d %d %d %d", sig2(i1), sig2(i2), sig2(o1), sig2(o2), sig2(mi * p), sig2(mo * p) }')"
  else
    basis="prior"
    local base; base="$(prior_for "$step")"
    read -r imin imax omin omax <<< "$(awk -v r="$repos" -v cx="$cx" -v base="$base" "$AWKLIB"'BEGIN {
      split(base, v, " "); f = 1 + 0.25 * (r - 1); if (f > 2) f = 2
      if (cx == "alta") f *= 1.3; else if (cx == "baixa") f *= 0.75
      printf "%d %d %d %d", sig2(v[1] * f), sig2(v[2] * f), sig2(v[3] * f), sig2(v[4] * f) }')"
  fi

  [ "$basis" = prior ] || warn=""   # na calibração os pontos entram direto, sem degrau da escala
  local size conf
  size="$(awk -v o="$omax" 'BEGIN { print (o < 15000) ? "P" : (o <= 60000 ? "M" : "G") }')"
  if [ "$basis" = prior ]; then conf="não calibrado ($n de $MIN_CALIB registros)"; else conf="calibrado com $n registros ($tool)"; fi

  if [ "$plain" -eq 1 ]; then
    awk -v s="$size" -v a="$imin" -v b="$imax" -v c="$omin" -v d="$omax" -v conf="$( [ "$basis" = prior ] && echo "não calibrado" || echo "calibrado ($n)" )" "$AWKLIB"'BEGIN {
      printf "%s · entrada %s–%s · saída %s–%s · %s\n", s, fshort(a), fshort(b), fshort(c), fshort(d), conf }'
  else
    awk -v s="$size" -v conf="$conf" -v a="$imin" -v b="$imax" -v c="$omin" -v d="$omax" -v ti="$typ_in" -v to="$typ_out" -v w="$warn" -v pts="$points" "$AWKLIB"'BEGIN {
      printf "Consumo estimado (IA): tamanho %s · %s\n", s, conf
      printf "  Entrada: %s – %s tokens  (a maior parte é releitura de contexto em cache, que é barata)\n", flong(a), flong(b)
      printf "  Saída:   %s – %s tokens\n", flong(c), flong(d)
      if (ti != "") printf "  Típico:  entrada ~%s · saída ~%s\n", flong(ti), flong(to)
      if (s == "G" && pts >= 13) print "  Sugestão: tarefa grande. Considere quebrar em fases ou histórias menores."
      if (w != "") print "  Aviso: " w
      print "Faixa estimada, não promessa. Não equivale a % do seu plano." }'
  fi

  if [ "$save" -eq 1 ]; then
    T_UPDDATE=1 T_DATE="$(date +%F)" T_SLUG="$slug" T_PHASE="$phase" T_TOOL="$tool" T_POINTS="$points" T_MODEL="$model" \
      T_EIMIN="$imin" T_EIMAX="$imax" T_EOMIN="$omin" T_EOMAX="$omax" T_BASIS="$basis" \
      T_RIN="" T_ROUT="" T_RCACHE="" T_SOURCE="" T_NOTE="" upsert
    [ "$plain" -eq 1 ] || echo "(estimativa gravada em ${HIST#"$ROOT"/})"
  fi
}

cmd_actual() {
  local slug="" phase=all rin="" rout="" rcache="" points="" tool="" model="" source=manual note=""
  while [ $# -gt 0 ]; do case "$1" in
    --slug) opt_val "$@"; slug="$2"; shift 2 ;;
    --phase) opt_val "$@"; phase="$2"; shift 2 ;;
    --in) opt_val "$@"; rin="$(need_num --in "$2")"; shift 2 ;;
    --out) opt_val "$@"; rout="$(need_num --out "$2")"; shift 2 ;;
    --cache) opt_val "$@"; rcache="$(need_num --cache "$2")"; shift 2 ;;
    --points) opt_val "$@"; points="$(need_int --points "$2")"; shift 2 ;;
    --tool) opt_val "$@"; tool="$2"; shift 2 ;;
    --model) opt_val "$@"; model="$2"; shift 2 ;;
    --source) opt_val "$@"; source="$2"; shift 2 ;;
    --note) opt_val "$@"; note="$(printf '%s' "$2" | tr '\t\r\n' '   ')"; shift 2 ;;
    *) die "opção desconhecida em actual: $1 (veja: tokens.sh --help)" ;;
  esac; done
  [ -n "$slug" ] || die "informe a tarefa: tokens.sh actual --slug <tarefa> --in N --out N"
  [ -n "$rin" ] && [ -n "$rout" ] || die "informe o consumo real: --in <entrada> --out <saída>."
  check_phase "$phase"
  case "$source" in manual|auto) ;; *) die "--source deve ser manual ou auto." ;; esac
  if [ -z "$tool" ]; then   # sem --tool: usa a ferramenta da estimativa gravada, se for uma só
    local found=""
    if [ -f "$HIST" ]; then
      found="$(awk -F'\t' -v s="$slug" -v p="$phase" '/^#/ || NF == 0 { next } $2 == s && $3 == p { print $5 }' "$HIST" | sort -u)"
    fi
    case "$(printf '%s' "$found" | grep -c .)" in
      0) tool=other ;;
      1) tool="$found" ;;
      *) die "há estimativas de mais de uma ferramenta para essa fase. Informe --tool (${found//$'\n'/, })." ;;
    esac
  fi
  check_tool "$tool"
  T_UPDDATE=0 T_DATE="$(date +%F)" T_SLUG="$slug" T_PHASE="$phase" T_TOOL="$tool" T_POINTS="$points" T_MODEL="$model" \
    T_EIMIN="" T_EIMAX="" T_EOMIN="" T_EOMAX="" T_BASIS="" \
    T_RIN="$rin" T_ROUT="$rout" T_RCACHE="$rcache" T_SOURCE="$source" T_NOTE="$note" upsert
  echo "Consumo real gravado: $slug · fase $phase · $tool · entrada $rin · saída $rout"
}

cmd_report() {
  local slug=""
  while [ $# -gt 0 ]; do case "$1" in
    --slug) opt_val "$@"; slug="$2"; shift 2 ;;
    *) die "opção desconhecida em report: $1" ;;
  esac; done
  if [ ! -f "$HIST" ]; then echo "Ainda não há histórico. Estime com: tokens.sh estimate --points N --slug <tarefa> --save"; return 0; fi
  awk -F'\t' -v slug="$slug" "$AWKLIB"'
    function band(real, lo, hi,   mid) {
      if (real == "") return "sem consumo real registrado"
      if (lo == "" || hi == "") return "real " fshort(real) " (sem estimativa)"
      mid = (lo + hi) / 2
      return sprintf("real %s · %s · %+.0f%% do ponto médio", fshort(real), (real < lo ? "abaixo da faixa" : (real > hi ? "acima da faixa" : "dentro da faixa")), (real - mid) / mid * 100)
    }
    /^#/ || NF == 0 { next }
    slug != "" && $2 != slug { next }
    {
      s = $2; if (!(s in seen)) { seen[s] = 1; order[++ns] = s }
      k = ++cnt[s]; ph[s, k] = $3; pt[s, k] = $4; tl[s, k] = $5
      imin[s, k] = $7; imax[s, k] = $8; omin[s, k] = $9; omax[s, k] = $10; rin[s, k] = $11; rout[s, k] = $12
      if ($3 != "all") hasphase[s] = 1
    }
    END {
      if (ns == 0) { print "Nenhum registro" (slug != "" ? " para \"" slug "\"" : "") "."; exit }
      for (x = 1; x <= ns; x++) {
        s = order[x]; ti1 = ti2 = to1 = to2 = ri = ro = 0; comreal = 0; print s
        for (k = 1; k <= cnt[s]; k++) {
          label = (ph[s, k] == "all") ? "tarefa inteira" : "fase " ph[s, k]
          printf "  %s (%s pts, %s)\n", label, (pt[s, k] == "" ? "?" : pt[s, k]), tl[s, k]
          printf "    entrada: estimado %s · %s\n", (imin[s, k] == "" ? "—" : fshort(imin[s, k]) "–" fshort(imax[s, k])), band(rin[s, k], imin[s, k], imax[s, k])
          printf "    saída:   estimado %s · %s\n", (omin[s, k] == "" ? "—" : fshort(omin[s, k]) "–" fshort(omax[s, k])), band(rout[s, k], omin[s, k], omax[s, k])
          if (ph[s, k] == "all" && hasphase[s]) continue
          ti1 += imin[s, k]; ti2 += imax[s, k]; to1 += omin[s, k]; to2 += omax[s, k]
          if (rin[s, k] != "") { ri += rin[s, k]; ro += rout[s, k]; comreal++ }
        }
        printf "  Total: entrada estimada %s–%s · saída estimada %s–%s", fshort(ti1), fshort(ti2), fshort(to1), fshort(to2)
        if (comreal > 0) printf " · real entrada %s, saída %s (%d registro(s) com consumo real)", fshort(ri), fshort(ro), comreal
        printf "\n"
      }
    }' "$HIST"
}

cmd_calibrate() {
  local only=""
  while [ $# -gt 0 ]; do case "$1" in
    --tool) opt_val "$@"; only="$2"; check_tool "$only"; shift 2 ;;
    *) die "opção desconhecida em calibrate: $1" ;;
  esac; done
  local t n q1i mi q3i q1o mo q3o shown=0
  for t in claude codex antigravity other; do
    [ -z "$only" ] || [ "$only" = "$t" ] || continue
    read -r n q1i mi q3i q1o mo q3o <<< "$(calib_stats "$t")"
    [ "$n" -gt 0 ] || [ -n "$only" ] || continue
    shown=1
    echo "$t: $n registro(s) usável(is)"
    if [ "$n" -ge "$MIN_CALIB" ]; then
      awk -v a="$q1i" -v b="$mi" -v c="$q3i" -v d="$q1o" -v e="$mo" -v f="$q3o" "$AWKLIB"'BEGIN {
        printf "  calibrado. Por story point: entrada %s / %s / %s · saída %s / %s / %s  (1º quartil / mediana / 3º quartil)\n", fshort(a), fshort(b), fshort(c), fshort(d), fshort(e), fshort(f) }'
    else
      echo "  ainda não calibrado: faltam $((MIN_CALIB - n)) registro(s) com story points e consumo real (usando as hipóteses da tabela)."
    fi
  done
  [ "$shown" -eq 1 ] || echo "Nenhum registro com consumo real ainda. Registre com: tokens.sh actual --slug <tarefa> --points N --in N --out N"
}

cmd_history() {
  local slug=""
  while [ $# -gt 0 ]; do case "$1" in
    --slug) opt_val "$@"; slug="$2"; shift 2 ;;
    *) die "opção desconhecida em history: $1" ;;
  esac; done
  if [ ! -f "$HIST" ]; then echo "Ainda não há histórico."; return 0; fi
  awk -F'\t' -v slug="$slug" "$AWKLIB"'BEGIN { printf "%-10s %-24s %-5s %-4s %-11s %-9s %-9s %s\n", "data", "tarefa", "fase", "pts", "ferramenta", "entrada", "saida", "base" }
    /^#/ || NF == 0 { next }
    slug != "" && $2 != slug { next }
    { printf "%-10s %-24s %-5s %-4s %-11s %-9s %-9s %s\n", $1, substr($2, 1, 24), $3, $4, $5, ($11 == "" ? "-" : fshort($11)), ($12 == "" ? "-" : fshort($12)), $15 }' "$HIST"
}

cmd="${1:-}"; [ $# -eq 0 ] || shift
case "$cmd" in
  estimate)  cmd_estimate "$@" ;;
  actual)    cmd_actual "$@" ;;
  report)    cmd_report "$@" ;;
  calibrate) cmd_calibrate "$@" ;;
  history)   cmd_history "$@" ;;
  ''|-h|--help|help) usage ;;
  *) die "comando desconhecido: $cmd (use estimate, actual, report, calibrate ou history)" ;;
esac
