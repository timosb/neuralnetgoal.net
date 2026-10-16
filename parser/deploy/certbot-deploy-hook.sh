#!/usr/bin/env bash
# Install as:  /etc/letsencrypt/renewal-hooks/deploy/neuralnetgoal-parser.sh   (chmod +x)
# certbot runs it after every successful issue/renewal. It copies the new certificate to a folder the service user can
# read (the originals under /etc/letsencrypt are root-only) and restarts the API so Kestrel loads the new files.
set -euo pipefail

SRC="${RENEWED_LINEAGE:?this script is meant to be run by certbot (RENEWED_LINEAGE is set by it)}"
DST=/etc/neuralnetgoal-parser/certs

install -d -m 750 -o root -g nngparser /etc/neuralnetgoal-parser "$DST"
install -m 640 -o root -g nngparser "$SRC/fullchain.pem" "$DST/fullchain.pem"
install -m 640 -o root -g nngparser "$SRC/privkey.pem"   "$DST/privkey.pem"

# restart only if the service exists (the very first issue can happen before it is configured)
if systemctl list-unit-files neuralnetgoal-parser.service >/dev/null 2>&1; then
  systemctl restart neuralnetgoal-parser || true
fi
