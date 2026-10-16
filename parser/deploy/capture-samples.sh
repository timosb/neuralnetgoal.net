#!/usr/bin/env bash
# Downloads real betexplorer pages (politely: one request every ~3 seconds) so the parser's selectors
# (parser/neuralnetgoal.parser/Parsing/Selectors.cs) can be verified against real HTML.
# Run it on the VPS:   bash capture-samples.sh
# Output: ~/bxsamples2/*.html, cookies.txt and a summary on screen.
# v2: explicit URLs (v1 fetched the results page twice instead of a match page), legacy /soccer/ path test,
#     stage-tab leagues, an old season, and a TIME-ZONE experiment.

set -u
OUT="$HOME/bxsamples2"
mkdir -p "$OUT"
cd "$OUT" || exit 1

BASE="https://www.betexplorer.com"
UA="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"

get() {  # get <file> <path-or-url> [extra curl args...]
  local file="$1" url="$2"; shift 2
  [[ "$url" == http* ]] || url="$BASE$url"
  printf '%-26s ' "$file"
  curl -sS -L -m 30 -A "$UA" -H "Accept-Language: en-US,en;q=0.9" "$@" \
       -o "$file" -w "HTTP %{http_code}  %{size_download} bytes  -> %{url_effective}\n" "$url" \
    || echo "FAILED (curl error)"
  sleep 3
}

L=/football/england/premier-league

echo "== 1. match pages (the part v1 missed) =="
get match_finished.html   "$L/crystal-palace-nottingham/ld8jMT45/"
get match_finished2.html  "$L/arsenal-leeds/xtmHKGT0/"
get match_fixture.html    "$L/everton-chelsea/2wo3m634/"

echo "== 2. old season + legacy /soccer/ path =="
get results_archive.html  "$L-2025-2026/results/"
get legacy_soccer.html    "/soccer/england/premier-league/results/"

echo "== 3. leagues that have stage tabs (playoffs / groups) =="
get stages_cl.html        "/football/europe/champions-league/results/"
get stages_be.html        "/football/belgium/jupiler-pro-league/results/"

echo "== 4. TIME-ZONE experiment (same page, different time-zone cookie) =="
get tz_default_fixtures.html "$L/fixtures/" -c cookies.txt
get tz_p0_fixtures.html      "$L/fixtures/" -b "my_timezone=+0"
get tz_p3_fixtures.html      "$L/fixtures/" -b "my_timezone=+3"
get tz_p3_match_fixture.html "$L/everton-chelsea/2wo3m634/" -b "my_timezone=+3"

echo
echo "== cookies the site set =="
grep -v '^# ' cookies.txt 2>/dev/null | awk 'NF>=7 {print $6 " = " $7}' | head -20

echo
echo "== time of the first fixtures in each variant (Everton-Chelsea is the 17.10. 12:30 one) =="
for f in tz_default_fixtures tz_p0_fixtures tz_p3_fixtures; do
  printf '%-26s ' "$f.html"
  grep -o 'table-main__datetime">[^<]*' "$f.html" | head -4 | sed 's/.*">//' | tr '\n' '|'
  echo
done
echo
echo "== data-dt on the fixture match page (default vs +3 cookie) =="
for f in match_fixture tz_p3_match_fixture; do
  printf '%-26s ' "$f.html"
  grep -o 'data-dt="[^"]*"' "$f.html" | head -2 | tr '\n' ' '
  echo
done

echo
echo "== which of the match-page ids exist (match_finished.html) =="
for id in 'list-details__item__title teamsLink' 'id="match-date"' 'id="js-score"' 'id="js-partial"' 'id="js-eventstage"'; do
  printf '%-45s %s\n' "$id" "$(grep -c "$id" match_finished.html)"
done

echo
echo "Done. Files are in $OUT"
echo "On your PC:  scp \"root@<vps-ip>:~/bxsamples2/*\" <repo>/parser/samples/"
