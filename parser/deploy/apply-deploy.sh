#!/usr/bin/env bash
# Install as /usr/local/bin/neuralnetgoal-apply-deploy (chmod 755). Started by neuralnetgoal-deploy.path whenever the
# marker file /opt/neuralnetgoal-incoming/DEPLOY is written (i.e. you uploaded it with SFTP, as the LAST step of an upload).
#
#   new files in /opt/neuralnetgoal-incoming  ->  stop API, back up the current version, replace it, start the API,
#                                                 and roll back automatically if the new version does not stay running
#   marker only (no new files)               ->  just restart the API (use it after editing /etc/neuralnetgoal-parser.env)
set -uo pipefail

SRC=/opt/neuralnetgoal-incoming
DST=/opt/neuralnetgoal-parser
PREV=/opt/neuralnetgoal-parser.previous
MARK="$SRC/DEPLOY"
STATE=/var/lib/neuralnetgoal-deploy.state
LOG=/var/log/neuralnetgoal-deploy.log
SERVICE=neuralnetgoal-parser

log() { echo "$(date '+%F %T') $*" | tee -a "$LOG"; }

exec 9>/run/neuralnetgoal-deploy.lock
flock -n 9 || exit 0                      # a deployment is already running
[ -f "$MARK" ] || exit 0                  # nothing to do

sleep 5                                   # let the uploader finish closing / timestamping files
M=$(stat -c %Z "$MARK")                   # change time of the marker = when it was uploaded
if [ "$(cat "$STATE" 2>/dev/null || true)" = "$M" ]; then exit 0; fi   # this upload was already applied

if [ -f "$SRC/neuralnetgoal.parser.api.dll" ]; then
  log "deploy: new files found, applying"
  systemctl stop "$SERVICE"
  rm -rf "$PREV"
  cp -a "$DST" "$PREV"
  find "$DST" -mindepth 1 -delete
  (cd "$SRC" && find . -mindepth 1 -maxdepth 1 ! -name DEPLOY -exec cp -a {} "$DST"/ \;)
  chown -R root:root "$DST"
  systemctl start "$SERVICE"
  sleep 8
  if systemctl is-active --quiet "$SERVICE"; then
    log "deploy OK"
    find "$SRC" -mindepth 1 ! -name DEPLOY -delete
  else
    log "deploy FAILED: the new version did not start. Rolling back to the previous version."
    systemctl stop "$SERVICE"
    find "$DST" -mindepth 1 -delete
    cp -a "$PREV"/. "$DST"/
    systemctl start "$SERVICE"
    log "rolled back; the new files stay in $SRC for inspection. See: journalctl -u $SERVICE -n 50"
  fi
else
  log "restart only (no new files in $SRC)"
  systemctl restart "$SERVICE"
fi

echo "$M" > "$STATE"
