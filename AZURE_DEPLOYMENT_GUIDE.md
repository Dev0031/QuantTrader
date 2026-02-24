# 🚀 QuantTrader — Azure Deployment Guide
### From Scratch to Production on Azure Kubernetes Service (AKS)

> **Audience:** This guide walks a new team member or client through every step needed to provision, build, and deploy the entire QuantTrader platform on Microsoft Azure — starting with zero cloud resources.

---

## 📋 Table of Contents

1. [Project Architecture Overview](#1-project-architecture-overview)
2. [Prerequisites & Local Tools](#2-prerequisites--local-tools)
3. [Azure Account & Subscription Setup](#3-azure-account--subscription-setup)
4. [Resource Group & Core Infrastructure](#4-resource-group--core-infrastructure)
5. [Azure Container Registry (ACR)](#5-azure-container-registry-acr)
6. [Azure Kubernetes Service (AKS)](#6-azure-kubernetes-service-aks)
7. [Azure Service Bus](#7-azure-service-bus)
8. [Azure Key Vault](#8-azure-key-vault)
9. [Azure Database for PostgreSQL + TimescaleDB](#9-azure-database-for-postgresql--timescaledb)
10. [Azure Cache for Redis](#10-azure-cache-for-redis)
11. [Configure GitHub Actions CI/CD](#11-configure-github-actions-cicd)
12. [Build & Push Docker Images](#12-build--push-docker-images)
13. [Deploy to AKS with Kubernetes Manifests](#13-deploy-to-aks-with-kubernetes-manifests)
14. [DNS, TLS & Ingress Setup](#14-dns-tls--ingress-setup)
15. [Monitoring — Prometheus & Grafana](#15-monitoring--prometheus--grafana)
16. [Verify the Deployment](#16-verify-the-deployment)
17. [Troubleshooting Reference](#17-troubleshooting-reference)
18. [Azure Cost Estimate](#18-azure-cost-estimate)

---

## 1. Project Architecture Overview

QuantTrader is a **cloud-native, event-driven crypto trading bot** running as Kubernetes microservices on Azure.

```
┌─────────────────────────────────────────────────────────────┐
│                      INTERNET / CLIENT                       │
└──────────────────────────┬──────────────────────────────────┘
                           │ HTTPS
                  ┌────────▼────────┐
                  │  NGINX Ingress  │  (AKS Ingress Controller)
                  │   + TLS/HTTPS   │
                  └────────┬────────┘
          ┌────────────────┼────────────────┐
          │                │                │
   ┌──────▼──────┐  ┌──────▼──────┐        │
   │  Dashboard  │  │ API Gateway │        │
   │  (React SPA)│  │  (.NET 8)   │        │
   └─────────────┘  └──────┬──────┘        │
                           │               │
          ┌────────────────┼───────────────┘
          │                │               │              │
  ┌───────▼───────┐ ┌──────▼──────┐ ┌─────▼─────┐ ┌────▼────────────┐
  │ DataIngestion │ │  Strategy   │ │   Risk    │ │    Execution    │
  │   Service     │ │   Engine    │ │  Manager  │ │     Engine      │
  │  (.NET 8)     │ │  (.NET 8)   │ │  (.NET 8) │ │   (.NET 8)      │
  └───────┬───────┘ └──────┬──────┘ └─────┬─────┘ └────┬────────────┘
          │                │               │              │
          └────────────────┴──────┬────────┴──────────────┘
                                  │  Azure Service Bus
                    ┌─────────────┴─────────────┐
                    │                           │
           ┌────────▼────────┐        ┌─────────▼────────┐
           │  TimescaleDB    │        │   Azure Cache    │
           │  (PostgreSQL)   │        │   for Redis      │
           └─────────────────┘        └──────────────────┘

Azure Services Used:
  ACR → Container Registry       AKS → Kubernetes Cluster
  Key Vault → Secrets            Service Bus → Messaging
  PostgreSQL Flexible → DB       Redis Cache → Cache/PubSub
  GitHub Actions → CI/CD        Prometheus + Grafana → Monitoring
```

### Services Summary

| Service | Role | Port |
|---|---|---|
| **DataIngestion** | Binance WebSocket/REST feeds → DB + Redis | 8080 |
| **StrategyEngine** | MA crossover, momentum, mean-reversion signals | 8080 |
| **RiskManager** | Position sizing, stop-loss, kill-switch | 8080 |
| **ExecutionEngine** | Binance REST order placement (HMAC-signed) | 8080 |
| **ApiGateway** | JWT auth REST/WebSocket API for the dashboard | 8080 |
| **Dashboard** | React + TypeScript SPA | 80 |

---

## 2. Prerequisites & Local Tools

Install the following tools on your local machine **before starting**.

### Required CLI Tools

| Tool | Version | Install |
|---|---|---|
| **Azure CLI** | ≥ 2.55 | https://docs.microsoft.com/cli/azure/install-azure-cli |
| **kubectl** | ≥ 1.28 | `az aks install-cli` |
| **Helm** | ≥ 3.13 | https://helm.sh/docs/intro/install/ |
| **Docker Desktop** | ≥ 24 | https://www.docker.com/products/docker-desktop |
| **.NET 8 SDK** | 8.0 LTS | https://dotnet.microsoft.com/download/dotnet/8.0 |
| **Node.js** | ≥ 20 LTS | https://nodejs.org/ |
| **Git** | ≥ 2.40 | https://git-scm.com/ |

### Verify Your Tools

```bash
az --version
kubectl version --client
helm version
docker --version
dotnet --version        # Should show 8.x.x
node --version          # Should show v20.x.x
git --version
```

---

## 3. Azure Account & Subscription Setup

### 3.1 Sign In to Azure

```bash
# Login to Azure (opens browser)
az login

# List your subscriptions
az account list --output table

# Set the subscription you want to deploy to
az account set --subscription "<YOUR_SUBSCRIPTION_ID>"

# Confirm active subscription
az account show --output table
```

### 3.2 Register Required Azure Resource Providers

```bash
az provider register --namespace Microsoft.ContainerRegistry
az provider register --namespace Microsoft.ContainerService
az provider register --namespace Microsoft.KeyVault
az provider register --namespace Microsoft.ServiceBus
az provider register --namespace Microsoft.DBforPostgreSQL
az provider register --namespace Microsoft.Cache

# Check registration status (wait until all show "Registered")
az provider show --namespace Microsoft.ContainerService --query "registrationState"
```

### 3.3 Define Naming Variables

> 💡 Run these in your terminal session — all subsequent commands reference these variables.

```bash
# ---- Edit these values ----
LOCATION="eastus"
RESOURCE_GROUP="rg-quanttrader-prod"
ACR_NAME="quanttraderacr"          # Must be globally unique, lowercase, no hyphens
AKS_CLUSTER="aks-quanttrader-prod"
KEYVAULT_NAME="kv-quanttrader-prod"  # Must be globally unique
SERVICEBUS_NS="sb-quanttrader-prod"
POSTGRES_SERVER="pg-quanttrader-prod"
REDIS_NAME="redis-quanttrader-prod"
DB_NAME="quanttrader"
DB_USER="quanttrader"
DB_PASSWORD="<STRONG_PASSWORD_HERE>"    # e.g. Qtr@dP@ss2024!
# ----------------------------
```

---

## 4. Resource Group & Core Infrastructure

A **Resource Group** is a logical container for all Azure resources in this project.

```bash
# Create the resource group
az group create \
  --name $RESOURCE_GROUP \
  --location $LOCATION

# Verify creation
az group show --name $RESOURCE_GROUP --output table
```

---

## 5. Azure Container Registry (ACR)

ACR stores the Docker images built by CI/CD and pulled by AKS.

### 5.1 Create the Registry

```bash
az acr create \
  --resource-group $RESOURCE_GROUP \
  --name $ACR_NAME \
  --sku Standard \
  --admin-enabled true

# Get the login server URL (format: <name>.azurecr.io)
az acr show --name $ACR_NAME --query loginServer --output tsv
```

### 5.2 Login to ACR Locally

```bash
az acr login --name $ACR_NAME
```

> **Note:** The ACR name becomes your image registry prefix: `quanttraderacr.azurecr.io`

---

## 6. Azure Kubernetes Service (AKS)

AKS is the managed Kubernetes cluster that runs all QuantTrader microservices.

### 6.1 Create the AKS Cluster

```bash
az aks create \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER \
  --node-count 2 \
  --node-vm-size Standard_B2s \
  --enable-addons monitoring \
  --generate-ssh-keys \
  --attach-acr $ACR_NAME \
  --enable-managed-identity \
  --dns-name-prefix quanttrader-prod

# This takes 5–10 minutes
```

> **Node sizing guide:**
> - `Standard_B2s` — 2 vCPU, 4 GB RAM — suitable for dev/staging
> - `Standard_D4s_v3` — 4 vCPU, 16 GB RAM — recommended for production

### 6.2 Get AKS Credentials (kubectl access)

```bash
az aks get-credentials \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER

# Verify connection
kubectl get nodes
```

You should see your nodes in `Ready` state.

### 6.3 Install NGINX Ingress Controller

```bash
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm repo update

helm install ingress-nginx ingress-nginx/ingress-nginx \
  --namespace ingress-nginx \
  --create-namespace \
  --set controller.replicaCount=2 \
  --set controller.service.annotations."service\.beta\.kubernetes\.io/azure-load-balancer-health-probe-request-path"=/healthz

# Get the public IP assigned to the ingress (takes ~2 min)
kubectl get service ingress-nginx-controller --namespace ingress-nginx
```

> 📌 Note the **EXTERNAL-IP** — this is where your DNS will point later.

### 6.4 Install cert-manager (Automatic TLS)

```bash
helm repo add jetstack https://charts.jetstack.io
helm repo update

helm install cert-manager jetstack/cert-manager \
  --namespace cert-manager \
  --create-namespace \
  --set installCRDs=true
```

---

## 7. Azure Service Bus

Service Bus replaces RabbitMQ in production for reliable event-driven messaging between microservices.

```bash
# Create Service Bus namespace
az servicebus namespace create \
  --resource-group $RESOURCE_GROUP \
  --name $SERVICEBUS_NS \
  --sku Standard \
  --location $LOCATION

# Create topics for each event type
az servicebus topic create --resource-group $RESOURCE_GROUP --namespace-name $SERVICEBUS_NS --name market-ticks
az servicebus topic create --resource-group $RESOURCE_GROUP --namespace-name $SERVICEBUS_NS --name trade-signals
az servicebus topic create --resource-group $RESOURCE_GROUP --namespace-name $SERVICEBUS_NS --name risk-decisions
az servicebus topic create --resource-group $RESOURCE_GROUP --namespace-name $SERVICEBUS_NS --name order-events

# Get connection string (save this for Key Vault!)
az servicebus namespace authorization-rule keys list \
  --resource-group $RESOURCE_GROUP \
  --namespace-name $SERVICEBUS_NS \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString --output tsv
```

---

## 8. Azure Key Vault

Key Vault centralizes all secrets (API keys, DB passwords, connection strings).  
**Never put secrets in code or YAML files.**

### 8.1 Create Key Vault

```bash
az keyvault create \
  --name $KEYVAULT_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku standard
```

### 8.2 Store All Secrets

```bash
# Database password
az keyvault secret set --vault-name $KEYVAULT_NAME --name "DbPassword" --value "$DB_PASSWORD"

# Service Bus connection string
SB_CONN=$(az servicebus namespace authorization-rule keys list \
  --resource-group $RESOURCE_GROUP \
  --namespace-name $SERVICEBUS_NS \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString --output tsv)
az keyvault secret set --vault-name $KEYVAULT_NAME --name "ServiceBusConnectionString" --value "$SB_CONN"

# Binance API keys (get from Binance Testnet dashboard)
az keyvault secret set --vault-name $KEYVAULT_NAME --name "BinanceApiKey" --value "<YOUR_BINANCE_API_KEY>"
az keyvault secret set --vault-name $KEYVAULT_NAME --name "BinanceSecretKey" --value "<YOUR_BINANCE_SECRET_KEY>"

# JWT signing key (generate a strong random string)
az keyvault secret set --vault-name $KEYVAULT_NAME --name "JwtSigningKey" --value "<RANDOM_256_BIT_KEY>"

# Redis connection string (fill after Step 10)
# az keyvault secret set --vault-name $KEYVAULT_NAME --name "RedisConnectionString" --value "<REDIS_CONN_STRING>"
```

### 8.3 Grant AKS Access to Key Vault

```bash
# Get AKS managed identity object ID
AKS_IDENTITY=$(az aks show \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER \
  --query identityProfile.kubeletidentity.objectId --output tsv)

# Grant secret read permissions
az keyvault set-policy \
  --name $KEYVAULT_NAME \
  --object-id $AKS_IDENTITY \
  --secret-permissions get list
```

---

## 9. Azure Database for PostgreSQL + TimescaleDB

QuantTrader uses **TimescaleDB** (a PostgreSQL extension) for time-series market data storage.

### 9.1 Create PostgreSQL Flexible Server

```bash
az postgres flexible-server create \
  --resource-group $RESOURCE_GROUP \
  --name $POSTGRES_SERVER \
  --location $LOCATION \
  --admin-user $DB_USER \
  --admin-password $DB_PASSWORD \
  --sku-name Standard_B1ms \
  --tier Burstable \
  --version 16 \
  --storage-size 32 \
  --database-name $DB_NAME

# Allow access from AKS (adjust IP range to your AKS subnet)
az postgres flexible-server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --name $POSTGRES_SERVER \
  --rule-name AllowAKS \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0
```

> ⚠️ `0.0.0.0/0.0.0.0` allows all Azure services. For production, restrict to the AKS subnet CIDR.

### 9.2 Enable TimescaleDB Extension

```bash
# Connect to the database and enable TimescaleDB
az postgres flexible-server parameter set \
  --resource-group $RESOURCE_GROUP \
  --server-name $POSTGRES_SERVER \
  --name "azure.extensions" \
  --value "timescaledb"
```

### 9.3 Initialize the Database Schema

```bash
# Get the server FQDN
POSTGRES_HOST=$(az postgres flexible-server show \
  --resource-group $RESOURCE_GROUP \
  --name $POSTGRES_SERVER \
  --query fullyQualifiedDomainName --output tsv)

# Run the init SQL script
psql "host=$POSTGRES_HOST port=5432 dbname=$DB_NAME user=$DB_USER password=$DB_PASSWORD sslmode=require" \
  -f deploy/init-db.sql
```

### 9.4 Save the Connection String to Key Vault

```bash
DB_CONN="Host=$POSTGRES_HOST;Port=5432;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD;SSL Mode=Require"
az keyvault secret set --vault-name $KEYVAULT_NAME --name "DbConnectionString" --value "$DB_CONN"
```

---

## 10. Azure Cache for Redis

Redis is used for market data caching, pub/sub, and rate-limit state.

```bash
# Create Redis cache (takes ~15 minutes)
az redis create \
  --resource-group $RESOURCE_GROUP \
  --name $REDIS_NAME \
  --location $LOCATION \
  --sku Basic \
  --vm-size c0

# Get connection string
REDIS_HOST=$(az redis show --resource-group $RESOURCE_GROUP --name $REDIS_NAME --query hostName --output tsv)
REDIS_KEY=$(az redis list-keys --resource-group $RESOURCE_GROUP --name $REDIS_NAME --query primaryKey --output tsv)
REDIS_CONN="$REDIS_HOST:6380,password=$REDIS_KEY,ssl=True,abortConnect=False"

# Save to Key Vault
az keyvault secret set --vault-name $KEYVAULT_NAME --name "RedisConnectionString" --value "$REDIS_CONN"
```

---

## 11. Configure GitHub Actions CI/CD

The CI/CD pipeline automatically builds Docker images, pushes them to ACR, and deploys to AKS on every push to `main`.

### 11.1 Create a Service Principal for GitHub Actions

```bash
SUBSCRIPTION_ID=$(az account show --query id --output tsv)

az ad sp create-for-rbac \
  --name "sp-quanttrader-github" \
  --role contributor \
  --scopes /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP \
  --sdk-auth
```

> 📋 **Copy the entire JSON output** — you'll need it in the next step.

### 11.2 Add GitHub Repository Secrets

Go to your GitHub repository → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**

| Secret Name | Value |
|---|---|
| `AZURE_CREDENTIALS` | Full JSON from the service principal command above |
| `ACR_LOGIN_SERVER` | `quanttraderacr.azurecr.io` |
| `ACR_USERNAME` | From: `az acr credential show --name $ACR_NAME --query username` |
| `ACR_PASSWORD` | From: `az acr credential show --name $ACR_NAME --query passwords[0].value` |
| `AKS_RESOURCE_GROUP` | `rg-quanttrader-prod` |
| `AKS_CLUSTER_NAME` | `aks-quanttrader-prod` |
| `KEYVAULT_NAME` | `kv-quanttrader-prod` |

### 11.3 GitHub Actions Workflow (`.github/workflows/deploy.yml`)

The project already includes CI/CD workflows in `.github/workflows/`. The pipeline does:

```
1. Trigger: push to main branch
2. Build & test all .NET services (dotnet build + dotnet test)
3. Build frontend (npm install + npm run build)
4. Build Docker images for each service
5. Push images to ACR with :latest and :<git-sha> tags
6. Update AKS deployments with new image tags (kubectl set image)
```

Verify the workflow exists:
```bash
ls .github/workflows/
```

---

## 12. Build & Push Docker Images

### 12.1 Review Dockerfiles

Each service has its own Dockerfile in `deploy/docker/`:

```bash
ls deploy/docker/
```

### 12.2 Build Images Locally (optional validation)

```bash
# Login to ACR
az acr login --name $ACR_NAME
ACR_SERVER="${ACR_NAME}.azurecr.io"

# Build and push each service
docker build -f deploy/docker/Dockerfile.DataIngestion \
  -t $ACR_SERVER/data-ingestion:latest .
docker push $ACR_SERVER/data-ingestion:latest

docker build -f deploy/docker/Dockerfile.StrategyEngine \
  -t $ACR_SERVER/strategy-engine:latest .
docker push $ACR_SERVER/strategy-engine:latest

docker build -f deploy/docker/Dockerfile.RiskManager \
  -t $ACR_SERVER/risk-manager:latest .
docker push $ACR_SERVER/risk-manager:latest

docker build -f deploy/docker/Dockerfile.ExecutionEngine \
  -t $ACR_SERVER/execution-engine:latest .
docker push $ACR_SERVER/execution-engine:latest

docker build -f deploy/docker/Dockerfile.ApiGateway \
  -t $ACR_SERVER/api-gateway:latest .
docker push $ACR_SERVER/api-gateway:latest

# Build React dashboard
cd dashboard && npm install && npm run build && cd ..
docker build -f deploy/docker/Dockerfile.Dashboard \
  -t $ACR_SERVER/dashboard:latest .
docker push $ACR_SERVER/dashboard:latest
```

### 12.3 Verify Images in ACR

```bash
az acr repository list --name $ACR_NAME --output table
```

---

## 13. Deploy to AKS with Kubernetes Manifests

### 13.1 Create Namespace

```bash
kubectl apply -f deploy/k8s/namespace.yaml

# Verify
kubectl get namespace quanttrader
```

### 13.2 Create Secrets in Kubernetes

```bash
# Pull values from Key Vault and create Kubernetes secrets
DB_CONN=$(az keyvault secret show --vault-name $KEYVAULT_NAME --name "DbConnectionString" --query value --output tsv)
REDIS_CONN=$(az keyvault secret show --vault-name $KEYVAULT_NAME --name "RedisConnectionString" --query value --output tsv)
SB_CONN=$(az keyvault secret show --vault-name $KEYVAULT_NAME --name "ServiceBusConnectionString" --query value --output tsv)
BINANCE_KEY=$(az keyvault secret show --vault-name $KEYVAULT_NAME --name "BinanceApiKey" --query value --output tsv)
BINANCE_SECRET=$(az keyvault secret show --vault-name $KEYVAULT_NAME --name "BinanceSecretKey" --query value --output tsv)
JWT_KEY=$(az keyvault secret show --vault-name $KEYVAULT_NAME --name "JwtSigningKey" --query value --output tsv)

kubectl create secret generic quanttrader-secrets \
  --namespace quanttrader \
  --from-literal=db-connection-string="$DB_CONN" \
  --from-literal=redis-connection-string="$REDIS_CONN" \
  --from-literal=servicebus-connection-string="$SB_CONN" \
  --from-literal=binance-api-key="$BINANCE_KEY" \
  --from-literal=binance-secret-key="$BINANCE_SECRET" \
  --from-literal=jwt-signing-key="$JWT_KEY"
```

### 13.3 Apply ConfigMaps

```bash
kubectl apply -f deploy/k8s/configmap.yaml -n quanttrader
```

### 13.4 Deploy Infrastructure Services (DB Proxies if needed)

```bash
kubectl apply -f deploy/k8s/infrastructure.yaml -n quanttrader
```

### 13.5 Deploy All Microservices

```bash
# Update ACR registry in all manifests (replace placeholder)
sed -i "s|quanttrader.azurecr.io|${ACR_NAME}.azurecr.io|g" deploy/k8s/*.yaml

# Deploy services in dependency order
kubectl apply -f deploy/k8s/data-ingestion.yaml   -n quanttrader
kubectl apply -f deploy/k8s/strategy-engine.yaml  -n quanttrader
kubectl apply -f deploy/k8s/risk-manager.yaml     -n quanttrader
kubectl apply -f deploy/k8s/execution-engine.yaml -n quanttrader
kubectl apply -f deploy/k8s/api-gateway.yaml      -n quanttrader
kubectl apply -f deploy/k8s/dashboard.yaml        -n quanttrader

# Watch rollout status
kubectl rollout status deployment/data-ingestion  -n quanttrader
kubectl rollout status deployment/api-gateway     -n quanttrader
kubectl rollout status deployment/dashboard       -n quanttrader
```

### 13.6 Check All Pods Are Running

```bash
kubectl get pods -n quanttrader

# All pods should show STATUS: Running
# READY column should show 1/1 (or 2/2 for gateway & dashboard)
```

---

## 14. DNS, TLS & Ingress Setup

### 14.1 Get the Ingress Public IP

```bash
INGRESS_IP=$(kubectl get service ingress-nginx-controller \
  --namespace ingress-nginx \
  -o jsonpath='{.status.loadBalancer.ingress[0].ip}')
echo "Ingress IP: $INGRESS_IP"
```

### 14.2 Configure Your Domain DNS

In your domain registrar (e.g., GoDaddy, Cloudflare, Azure DNS):

| Type | Host | Value |
|---|---|---|
| `A` | `@` (root) | `<INGRESS_IP>` |
| `A` | `api` | `<INGRESS_IP>` |

> If using **Azure DNS**:
> ```bash
> az dns zone create --resource-group $RESOURCE_GROUP --name "yourdomain.com"
> az dns record-set a add-record \
>   --resource-group $RESOURCE_GROUP \
>   --zone-name "yourdomain.com" \
>   --record-set-name "@" \
>   --ipv4-address $INGRESS_IP
> ```

### 14.3 Create a Let's Encrypt ClusterIssuer

```yaml
# Save as: deploy/k8s/cluster-issuer.yaml
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    email: your-email@example.com          # ← Change this
    server: https://acme-v02.api.letsencrypt.org/directory
    privateKeySecretRef:
      name: letsencrypt-prod
    solvers:
      - http01:
          ingress:
            class: nginx
```

```bash
kubectl apply -f deploy/k8s/cluster-issuer.yaml
```

### 14.4 Update Ingress Host and Apply

Edit `deploy/k8s/ingress.yaml` and replace `quanttrader.local` with your actual domain, then:

```bash
kubectl apply -f deploy/k8s/ingress.yaml -n quanttrader

# Check TLS certificate issuance (takes 1–3 minutes)
kubectl get certificate -n quanttrader
kubectl describe certificate quanttrader-tls -n quanttrader
```

---

## 15. Monitoring — Prometheus & Grafana

### 15.1 Install Prometheus Stack via Helm

```bash
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo update

helm install prometheus-stack prometheus-community/kube-prometheus-stack \
  --namespace monitoring \
  --create-namespace \
  --set grafana.adminPassword="admin" \
  --set prometheus.prometheusSpec.additionalScrapeConfigs[0].job_name="quanttrader" \
  --set prometheus.prometheusSpec.additionalScrapeConfigs[0].kubernetes_sd_configs[0].role="pod" \
  --set prometheus.prometheusSpec.additionalScrapeConfigs[0].kubernetes_sd_configs[0].namespaces.names[0]="quanttrader"
```

### 15.2 Apply Custom Prometheus Config

```bash
kubectl apply -f deploy/prometheus/prometheus.yml -n monitoring
```

### 15.3 Access Grafana Dashboard

```bash
# Port forward Grafana to your local machine
kubectl port-forward -n monitoring svc/prometheus-stack-grafana 3000:80

# Open http://localhost:3000
# Username: admin
# Password: admin (change immediately!)
```

### 15.4 Import QuantTrader Dashboards

In Grafana: **Dashboard → Import** → upload files from `deploy/grafana/dashboards/`

---

## 16. Verify the Deployment

### 16.1 Health Checks

Each service exposes health endpoints. Verify them:

```bash
# Get API Gateway external URL
kubectl get ingress -n quanttrader

# Test health endpoints
curl https://yourdomain.com/health/live
curl https://yourdomain.com/health/ready

# Should return: {"status":"Healthy"}
```

### 16.2 Check All Pods and Services

```bash
# All pods running
kubectl get pods -n quanttrader -o wide

# Services have correct ClusterIPs
kubectl get svc -n quanttrader

# Deployments are fully rolled out
kubectl get deployments -n quanttrader
```

### 16.3 Check Logs

```bash
# View logs for any service (replace NAME with pod name from above)
kubectl logs -n quanttrader deployment/data-ingestion --tail=50
kubectl logs -n quanttrader deployment/api-gateway --tail=50

# Follow logs in real-time
kubectl logs -n quanttrader deployment/strategy-engine -f
```

### 16.4 End-to-End Test

1. Open `https://yourdomain.com` in a browser → Dashboard should load
2. Login with your JWT credentials
3. Verify real-time market data is flowing (check DataIngestion logs)
4. Confirm Binance Testnet connection is active

### 16.5 Verify CI/CD Pipeline

Make a small commit and push to `main`:

```bash
git commit --allow-empty -m "test: verify CI/CD pipeline"
git push origin main
```

Check **GitHub → Actions** tab — the workflow should trigger, build images, and deploy to AKS automatically.

---

## 17. Troubleshooting Reference

### Pod Won't Start (CrashLoopBackOff)

```bash
# Describe the failing pod for events
kubectl describe pod <POD_NAME> -n quanttrader

# Check container logs
kubectl logs <POD_NAME> -n quanttrader --previous
```

### ImagePullBackOff

```bash
# Verify ACR credentials are linked to AKS
az aks check-acr --name $AKS_CLUSTER --resource-group $RESOURCE_GROUP --acr $ACR_NAME

# Reattach ACR if needed
az aks update --name $AKS_CLUSTER --resource-group $RESOURCE_GROUP --attach-acr $ACR_NAME
```

### Database Connection Issues

```bash
# Check the secret exists and is base64-encoded correctly
kubectl get secret quanttrader-secrets -n quanttrader -o jsonpath='{.data.db-connection-string}' | base64 -d

# Test connectivity from a pod
kubectl run psql-test --image=postgres:16 -it --rm -n quanttrader \
  -- psql "host=$POSTGRES_HOST port=5432 dbname=$DB_NAME user=$DB_USER password=$DB_PASSWORD sslmode=require"
```

### TLS Certificate Not Issuing

```bash
kubectl describe clusterissuer letsencrypt-prod
kubectl describe certificaterequest -n quanttrader
kubectl logs -n cert-manager deployment/cert-manager --tail=50
```

### Common Issues Quick Reference

| Symptom | Cause | Fix |
|---|---|---|
| `ImagePullBackoff` | ACR not linked to AKS | `az aks update --attach-acr` |
| `CrashLoopBackOff` | Missing env var / secret | Check `kubectl logs --previous` |
| 502 Bad Gateway | Service not ready yet | `kubectl rollout status` |
| TLS fails | DNS not propagated | Wait 5–15 min, recheck DNS |
| DB connection refused | Firewall rule | Add AKS subnet to PostgreSQL firewall |

---

## 18. Azure Cost Estimate

> Costs are approximate **East US** monthly estimates (as of 2024).

| Resource | SKU | Est. Monthly Cost |
|---|---|---|
| AKS Node Pool (2× B2s) | Standard_B2s × 2 | ~$70 |
| Azure Container Registry | Standard | ~$20 |
| PostgreSQL Flexible Server | Standard_B1ms | ~$15 |
| Azure Cache for Redis | Basic C0 | ~$17 |
| Azure Service Bus | Standard | ~$10 |
| Azure Key Vault | Standard | ~$5 |
| Load Balancer (Ingress) | Standard | ~$18 |
| Networking & Storage | — | ~$10 |
| **Total** | | **~$165/month** |

> 💡 **Save costs:** Stop AKS node pool when not in use:
> ```bash
> az aks nodepool scale --resource-group $RESOURCE_GROUP \
>   --cluster-name $AKS_CLUSTER --name nodepool1 --node-count 0
> ```

---

## ✅ Deployment Checklist

Use this as a final sign-off checklist before going live:

- [ ] Resource group created
- [ ] ACR created and login tested
- [ ] AKS cluster running with nodes in `Ready` state
- [ ] NGINX Ingress Controller installed with public IP
- [ ] cert-manager installed
- [ ] Azure Service Bus created with all topics
- [ ] Key Vault created with all secrets stored
- [ ] PostgreSQL Flexible Server running with TimescaleDB enabled
- [ ] Database schema initialized (`init-db.sql` applied)
- [ ] Azure Redis Cache running
- [ ] All Docker images built and pushed to ACR
- [ ] Kubernetes namespace `quanttrader` created
- [ ] Kubernetes secrets created from Key Vault values
- [ ] All 6 deployments running (`kubectl get pods -n quanttrader`)
- [ ] DNS A-record pointing to Ingress IP
- [ ] TLS certificate issued (STATUS: True)
- [ ] `/health/live` and `/health/ready` return 200
- [ ] Dashboard loads at `https://yourdomain.com`
- [ ] GitHub Actions CI/CD pipeline runs successfully
- [ ] Grafana accessible with QuantTrader dashboards imported
- [ ] Binance Testnet connection verified in DataIngestion logs

---

*Generated for QuantTrader — Elite Crypto Trading Bot Platform*  
*Architecture: .NET 8 Microservices · Azure AKS · TimescaleDB · Redis · Service Bus*  
*Last updated: February 2026*
