#!/usr/bin/env bash
# ws.sh — lê o .agents/workspace.yaml e opera sobre os repos do workspace.
#
# Uso:
#   ws.sh list                    nome<TAB>path<TAB>papel de cada repo
#   ws.sh status                  branch e alterações pendentes de cada repo
#   ws.sh each "<comando>"        roda o comando dentro de cada repo
#   ws.sh get <repo> <campo>      valor de um campo (path, role, stack, dev, test, build, lint, default_branch)
#   ws.sh path <repo>             caminho absoluto do repo
#   ws.sh role <papel>            nome(s) do(s) repo(s) com esse papel (backend, frontend, mcp...)
#
# Sem dependências além de bash/awk/git (funciona no Git Bash do Windows).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
YAML="$ROOT/.agents/workspace.yaml"

die() { echo "ws.sh: $*" >&2; exit 2; }
[ -f "$YAML" ] || die "não achei $YAML (rode /warm-up para criar)"

# Emite: nome<TAB>campo<TAB>valor para cada campo de cada repo.
fields() {
  awk '
    function trim(s) { sub(/^[ \t]+/, "", s); sub(/[ \t]+$/, "", s); gsub(/^["'\'']|["'\'']$/, "", s); return s }
    /^[ \t]*#/ || /^[ \t]*$/ { next }
    /^repos:/ { inrepos = 1; next }
    /^[^ \t-]/ { inrepos = 0 }
    !inrepos { next }
    /^[ \t]*-[ \t]*name:/ { line = $0; sub(/^[ \t]*-[ \t]*name:/, "", line); name = trim(line); print name "\tname\t" name; next }
    name != "" && /^[ \t]+[a-z_]+:/ {
      line = $0; key = line; sub(/^[ \t]+/, "", key); sub(/:.*/, "", key)
      val = line; sub(/^[^:]*:/, "", val); sub(/[ \t]+#.*$/, "", val)
      print name "\t" key "\t" trim(val)
    }
  ' "$YAML"
}

get() { fields | awk -F'\t' -v r="$1" -v k="$2" '$1==r && $2==k { print $3; exit }'; }
names() { fields | awk -F'\t' '$2=="name" { print $1 }'; }
abspath() { local p; p="$(get "$1" path)"; p="${p:-$1}"; case "$p" in /*|?:/*|?:\*) echo "$p" ;; *) echo "$ROOT/$p" ;; esac; }

cmd="${1:-status}"; shift || true
case "$cmd" in
  list)
    for n in $(names); do printf '%s\t%s\t%s\n' "$n" "$(get "$n" path)" "$(get "$n" role)"; done ;;
  get)  [ $# -eq 2 ] || die "uso: ws.sh get <repo> <campo>"; get "$1" "$2" ;;
  path) [ $# -eq 1 ] || die "uso: ws.sh path <repo>"; abspath "$1" ;;
  role) [ $# -eq 1 ] || die "uso: ws.sh role <papel>"; fields | awk -F'\t' -v v="$1" '$2=="role" && $3==v { print $1 }' ;;
  status)
    for n in $(names); do
      d="$(abspath "$n")"; role="$(get "$n" role)"
      if [ ! -d "$d/.git" ]; then printf '%-28s %-10s ⚠️  repo não encontrado em %s\n' "$n" "${role:--}" "$d"; continue; fi
      br="$(git -C "$d" branch --show-current 2>/dev/null || echo '?')"
      ch="$(git -C "$d" status --porcelain 2>/dev/null | wc -l | tr -d ' ')"
      printf '%-28s %-10s @ %-24s %s alteração(ões)\n' "$n" "${role:--}" "$br" "$ch"
    done ;;
  each)
    [ $# -ge 1 ] || die "uso: ws.sh each \"<comando>\""
    for n in $(names); do
      d="$(abspath "$n")"; [ -d "$d" ] || continue
      echo "── $n"; (cd "$d" && bash -c "$*") || echo "   (falhou com código $?)"
    done ;;
  *) die "comando desconhecido: $cmd (use list|status|each|get|path|role)" ;;
esac
