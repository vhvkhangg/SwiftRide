#!/bin/bash
# Idempotent Keycloak setup for in-app Rider sign-up (POST /api/auth/register on the Gateway).
# --import-realm skips a realm that already exists, so a Keycloak database created before the
# registration client was added to swiftride-realm.json never gets it. This one-shot container
# (docker compose service keycloak-setup) applies the same settings to an existing realm:
#   - confidential client swiftride-registration with the configured secret, service account on
#   - its service account has realm-management/manage-users only
#   - default role default-roles-swiftride includes Rider (new users are Riders)
#   - Keycloak's own registration page is off (sign-up happens in the app)
# Safe to run on every start: each step checks or overwrites with the same value.
set -euo pipefail

KC=/opt/keycloak/bin/kcadm.sh
REALM=swiftride
CLIENT=swiftride-registration
SERVER=${KEYCLOAK_URL:-http://keycloak:8080}

$KC config credentials --server "$SERVER" --realm master \
  --user "$KEYCLOAK_ADMIN_USER" --password "$KEYCLOAK_ADMIN_PASSWORD" >/dev/null

client_id() {
  $KC get clients -r "$REALM" -q "clientId=$CLIENT" --fields id --format csv --noquotes 2>/dev/null | head -n 1
}

ID=$(client_id)
if [ -z "$ID" ]; then
  echo "Creating client $CLIENT"
  $KC create clients -r "$REALM" \
    -s "clientId=$CLIENT" -s "name=SwiftRide Gateway - in-app Rider sign-up" \
    -s publicClient=false -s clientAuthenticatorType=client-secret \
    -s standardFlowEnabled=false -s directAccessGrantsEnabled=false -s implicitFlowEnabled=false \
    -s serviceAccountsEnabled=true >/dev/null
  ID=$(client_id)
fi
# Keep the secret in sync with the Gateway's Registration__ClientSecret.
$KC update "clients/$ID" -r "$REALM" -s "secret=$REGISTRATION_CLIENT_SECRET" -s serviceAccountsEnabled=true
$KC add-roles -r "$REALM" --uusername "service-account-$CLIENT" --cclientid realm-management --rolename manage-users
$KC add-roles -r "$REALM" --rname default-roles-swiftride --rolename Rider
$KC update "realms/$REALM" -s registrationAllowed=false
echo "Keycloak registration setup is ready."
