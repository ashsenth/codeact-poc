#!/usr/bin/env bash
set -euo pipefail

subscription=''
resource_group='codeact-validation'
location='westus2'
vm_name='codeact-validation'
vm_size='Standard_D4s_v5'
admin_user='codeact'
ssh_key=''
ssh_ip=''
hourly_cost=''
approved=false
while (($#)); do
    case "$1" in
        --subscription) subscription="$2"; shift 2 ;;
        --resource-group) resource_group="$2"; shift 2 ;;
        --location) location="$2"; shift 2 ;;
        --name) vm_name="$2"; shift 2 ;;
        --size) vm_size="$2"; shift 2 ;;
        --admin-user) admin_user="$2"; shift 2 ;;
        --ssh-public-key) ssh_key="$2"; shift 2 ;;
        --ssh-ip) ssh_ip="$2"; shift 2 ;;
        --estimated-hourly-usd) hourly_cost="$2"; shift 2 ;;
        --approved-create) approved=true; shift ;;
        *) echo "Unknown argument: $1" >&2; exit 2 ;;
    esac
done
[[ -n "$subscription" && -f "$ssh_key" ]] || { echo 'Required: --subscription ID --ssh-public-key existing.pub [--ssh-ip current-public-IPv4]' >&2; exit 2; }
grep -Eq '^(ssh-ed25519|ssh-rsa|ecdsa-sha2-) ' "$ssh_key" || { echo 'Expected an SSH PUBLIC key, not a private key.' >&2; exit 2; }
if [[ -z "$ssh_ip" ]]; then ssh_ip=$(curl --fail --silent --show-error https://api.ipify.org); fi
[[ "$ssh_ip" =~ ^([0-9]{1,3}\.){3}[0-9]{1,3}$ ]] || { echo 'Supply current public IPv4 with --ssh-ip.' >&2; exit 2; }
IFS=. read -r -a octets <<< "$ssh_ip"
for octet in "${octets[@]}"; do ((10#$octet <= 255)) || { echo 'Invalid IPv4.' >&2; exit 2; }; done
[[ "$ssh_ip" != '0.0.0.0' ]] || { echo 'Refusing unspecified SSH source.' >&2; exit 2; }
if [[ -z "$hourly_cost" && "$location" == westus2 && "$vm_size" == Standard_D4s_v5 ]]; then hourly_cost='0.192'; fi
[[ "$hourly_cost" =~ ^[0-9]+([.][0-9]+)?$ ]] || { echo 'Supply a reviewed --estimated-hourly-usd for this region/size.' >&2; exit 2; }
echo "ESTIMATE: USD $hourly_cost/hour compute only; disk, Standard public IPv4, egress and Azure OpenAI extra. Retail reference checked 2026-11-03. Reconfirm current pricing before approval."
echo 'Dedicated NEW resource group required; no existing resources will be reused. No auto-cleanup on failure.'
group=(az group create --subscription "$subscription" --name "$resource_group" --location "$location")
nsg=(az network nsg create --subscription "$subscription" --resource-group "$resource_group" --name "$vm_name-nsg" --location "$location")
ssh_rule=(az network nsg rule create --subscription "$subscription" --resource-group "$resource_group" --nsg-name "$vm_name-nsg" --name AllowSshFromCurrentIp --priority 100 --direction Inbound --access Allow --protocol Tcp --source-address-prefixes "$ssh_ip/32" --source-port-ranges '*' --destination-address-prefixes '*' --destination-port-ranges 22)
deny_rule=(az network nsg rule create --subscription "$subscription" --resource-group "$resource_group" --nsg-name "$vm_name-nsg" --name DenyAllOtherInbound --priority 200 --direction Inbound --access Deny --protocol '*' --source-address-prefixes '*' --source-port-ranges '*' --destination-address-prefixes '*' --destination-port-ranges '*')
vm=(az vm create --subscription "$subscription" --resource-group "$resource_group" --name "$vm_name" --location "$location" --image Canonical:ubuntu-24_04-lts:server:latest --size "$vm_size" --security-type Standard --admin-username "$admin_user" --authentication-type ssh --ssh-key-values "$ssh_key" --nsg "$vm_name-nsg" --nsg-rule NONE --public-ip-sku Standard --storage-sku StandardSSD_LRS --os-disk-size-gb 64)
print_command() { printf '%q ' "$@"; printf '\n'; }
print_command "${group[@]}"
print_command "${nsg[@]}"
print_command "${ssh_rule[@]}"
print_command "${deny_rule[@]}"
print_command "${vm[@]}"
echo 'TEARDOWN (separate explicit approval required; deletes the ENTIRE dedicated resource group):'
print_command az group delete --subscription "$subscription" --name "$resource_group" --yes --no-wait
if [[ "$approved" != true ]]; then echo 'PLAN ONLY: no Azure commands executed. Review commands/cost, then explicitly approve before --approved-create.'; exit 0; fi
command -v az >/dev/null
command -v jq >/dev/null || { echo 'Install jq in the planning environment before approved creation.' >&2; exit 1; }
[[ $(az group exists --subscription "$subscription" --name "$resource_group") == false ]] || { echo 'Resource group exists; refusing to modify it.' >&2; exit 1; }
sku=$(az vm list-skus --subscription "$subscription" --location "$location" --size "$vm_size" --all --query "[?name=='$vm_size'] | [0]" -o json)
architecture=$(jq -er '.capabilities[] | select(.name == "CpuArchitectureType") | .value' <<< "$sku")
generations=$(jq -er '.capabilities[] | select(.name == "HyperVGenerations") | .value' <<< "$sku")
capability=$(jq -r '.capabilities[]? | select(.name == "NestedVirtualization") | .value' <<< "$sku")
[[ "$architecture" == x64 && ",$generations," == *,V2,* ]] || { echo 'Requires an x64, Gen2-capable SKU.' >&2; exit 1; }
case "$capability" in
    True|true) echo 'SKU explicitly advertises nested virtualization.' ;;
    '')
        [[ "$vm_size" == Standard_D4s_v5 ]] || { echo 'No nested virtualization capability; this size requires separate documentation review.' >&2; exit 1; }
        echo 'Standard_D4s_v5: nested virtualization supported per Microsoft Learn Dsv5 feature table; API field absent. KVM must still be verified on VM.'
        ;;
    *) echo "Nested virtualization capability is not supported: $capability" >&2; exit 1 ;;
esac
"${group[@]}"
"${nsg[@]}"
"${ssh_rule[@]}"
"${deny_rule[@]}"
"${vm[@]}"
echo 'Creation finished. Verify /dev/kvm on the guest; SKU capability is not runtime proof.'