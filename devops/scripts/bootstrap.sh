#!/bin/bash
set -euo pipefail

# [colours for output]
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

log()  { echo -e "${GREEN}[bootstrap]${NC} $1"; }
warn() { echo -e "${YELLOW}[warning]${NC} $1"; }
fail() { echo -e "${RED}[error]${NC} $1"; exit 1; }

[[ $EUID -ne 0 ]] && fail "Run as root: sudo bash bootstrap.sh"

# ────────────────────────────────────────────────────────
# CONFIGURATION — edit before running
# ────────────────────────────────────────────────────────
SSH_PUBLIC_KEY=""         # paste your public key here — required for SSH access before password auth is disabled
INSTALL_COOLIFY=true      # set to false to do base hardening only and install Coolify manually later
# ────────────────────────────────────────────────────────

[[ -z "$SSH_PUBLIC_KEY" ]] && fail "SSH_PUBLIC_KEY is required — paste your public key in the script config section"

# ────────────────────────────────────────────────────────
# STEP 1 — system update
# ────────────────────────────────────────────────────────
log "Updating system packages..."
apt update && apt upgrade -y
apt install -y \
  curl \
  wget \
  git \
  ufw \
  fail2ban \
  unattended-upgrades \
  apt-transport-https \
  ca-certificates \
  gnupg \
  lsb-release \
  software-properties-common \
  build-essential

# ────────────────────────────────────────────────────────
# STEP 2 — firewall
# ────────────────────────────────────────────────────────
log "Configuring UFW firewall..."
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp comment 'SSH'
ufw allow 80/tcp comment 'HTTP'
ufw allow 443/tcp comment 'HTTPS'
ufw allow 8000/tcp comment 'Coolify dashboard'
ufw --force enable
log "Firewall enabled — ports 22, 80, 443, 8000 open (restrict 8000 to your IP after Coolify setup)"

# ────────────────────────────────────────────────────────
# STEP 3 — fail2ban
# ────────────────────────────────────────────────────────
log "Configuring fail2ban..."
cat > /etc/fail2ban/jail.local <<EOF
[DEFAULT]
bantime  = 1h
findtime = 10m
maxretry = 5

[sshd]
enabled = true
port    = ssh
logpath = %(sshd_log)s
backend = %(sshd_backend)s
EOF
systemctl enable fail2ban
systemctl restart fail2ban
log "fail2ban configured — SSH brute force protection active"

# ────────────────────────────────────────────────────────
# STEP 4 — unattended security upgrades
# ────────────────────────────────────────────────────────
log "Enabling unattended security upgrades..."
cat > /etc/apt/apt.conf.d/20auto-upgrades <<EOF
APT::Periodic::Update-Package-Lists "1";
APT::Periodic::Unattended-Upgrade "1";
APT::Periodic::AutocleanInterval "7";
EOF
systemctl enable unattended-upgrades
log "Automatic security patches enabled"

# ────────────────────────────────────────────────────────
# STEP 5 — SSH hardening
# ────────────────────────────────────────────────────────
log "Installing SSH key and hardening SSH configuration..."
mkdir -p /root/.ssh
grep -qxF "$SSH_PUBLIC_KEY" /root/.ssh/authorized_keys 2>/dev/null || echo "$SSH_PUBLIC_KEY" >> /root/.ssh/authorized_keys
chmod 700 /root/.ssh
chmod 600 /root/.ssh/authorized_keys
sed -i 's/#PermitRootLogin yes/PermitRootLogin prohibit-password/' /etc/ssh/sshd_config
sed -i 's/PermitRootLogin yes/PermitRootLogin prohibit-password/' /etc/ssh/sshd_config
sed -i 's/#PasswordAuthentication yes/PasswordAuthentication no/' /etc/ssh/sshd_config
sed -i 's/PasswordAuthentication yes/PasswordAuthentication no/' /etc/ssh/sshd_config
systemctl restart sshd
log "SSH hardened — key installed, password auth disabled, root login key-only"

# ────────────────────────────────────────────────────────
# STEP 6 — Coolify
# ────────────────────────────────────────────────────────
# Coolify's installer pulls in Docker (from the official source if absent)
# and stands up the Coolify stack. Coolify then manages all app and
# resource containers, the Traefik proxy, SSL, and data volumes itself —
# there is no separate deploy user, Node/nvm, or /data directories to set up.
if [[ "$INSTALL_COOLIFY" == "true" ]]; then
  log "Installing Coolify (this also installs Docker if missing)..."
  curl -fsSL https://cdn.coollabs.io/coolify/install.sh | bash
  log "Coolify installed — dashboard available on port 8000"
else
  warn "INSTALL_COOLIFY=false — skipping Coolify install; install it manually later"
fi

# ────────────────────────────────────────────────────────
# DONE
# ────────────────────────────────────────────────────────
echo ""
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${GREEN} Bootstrap complete${NC}"
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo "  Docker      : $(docker --version 2>/dev/null || echo 'installed by Coolify — verify in new shell')"
echo "  Firewall    : $(ufw status | head -1)"
echo "  Coolify     : http://$(hostname -I | awk '{print $1}'):8000"
echo ""
echo -e "${YELLOW}  Next steps:${NC}"
echo "  1. Open a new SSH session to verify key-based access still works"
echo "  2. Open the Coolify dashboard on port 8000 and create the admin account immediately"
echo "  3. Restrict port 8000 to your IP (ufw) or set a dashboard domain with HTTPS"
echo "  4. Follow the deployment-coolify subagent — First Deploy Checklist"
echo ""
