#!/usr/bin/env bash
set -euo pipefail

subscription=''
resource_group='codeact-validation'
location='westus2'
vm_name='codeact-validation'
vm_size='Standard_D4s_v5'
admin_user='codeact'
ssh_key=''
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
        --estimated-hourly-usd) hourly_cost="$2"; shift 2 ;;
        --approved-create) approved=true; shift ;;
        *) echo "Unknown argument: $1" >&2; exit 2 ;;
    esac
done
[[ -n "$subscription" && -f "$ssh_key" ]] || { echo 'Required: --subscription ID --ssh-public-key existing.pub' >&2; exit 2; }
grep -Eq '^(ssh-ed25519|ssh-rsa|ecdsa-sha2-nistp(256|384|521)) ' "$ssh_key" || { echo 'Expected a supported SSH PUBLIC key, not a private key.' >&2; exit 2; }
if [[ -z "$hourly_cost" && "$location" == westus2 && "$vm_size" == Standard_D4s_v5 ]]; then hourly_cost='0.192'; fi
[[ "$hourly_cost" =~ ^[0-9]+([.][0-9]+)?$ ]] || { echo 'Supply a reviewed --estimated-hourly-usd for this region/size.' >&2; exit 2; }
echo "ESTIMATE: USD $hourly_cost/hour compute only; disk, Standard public IPv4, egress and Azure OpenAI extra. Retail reference checked 2026-11-03. Reconfirm current pricing before approval."
echo 'Dedicated NEW resource group required; no existing resources will be reused. No auto-cleanup on failure.'
group=(az group create --subscription "$subscription" --name "$resource_group" --location "$location")
nsg=(az network nsg create --subscription "$subscription" --resource-group "$resource_group" --name "$vm_name-nsg" --location "$location")
subnet_nsg=(az network nsg create --subscription "$subscription" --resource-group "$resource_group" --name "$vm_name-subnet-nsg" --location "$location")
vnet=(az network vnet create --subscription "$subscription" --resource-group "$resource_group" --name "$vm_name-vnet" --location "$location" --subnet-name default)
subnet=(az network vnet subnet update --subscription "$subscription" --resource-group "$resource_group" --vnet-name "$vm_name-vnet" --name default --network-security-group "$vm_name-subnet-nsg")
rules() {
    local action="$1" name
    for name in "$vm_name-nsg" "$vm_name-subnet-nsg"; do
        "$action" az network nsg rule create --subscription "$subscription" --resource-group "$resource_group" --nsg-name "$name" --name AllowSshFromCorpNet --priority 100 --direction Inbound --access Allow --protocol Tcp --source-address-prefixes CorpNetPublic --source-port-ranges '*' --destination-address-prefixes '*' --destination-port-ranges 22
        "$action" az network nsg rule create --subscription "$subscription" --resource-group "$resource_group" --nsg-name "$name" --name DenyAllOtherInbound --priority 200 --direction Inbound --access Deny --protocol '*' --source-address-prefixes '*' --source-port-ranges '*' --destination-address-prefixes '*' --destination-port-ranges '*'
    done
}
feature=(az feature register --subscription "$subscription" --namespace Microsoft.Network --name AllowBringYourOwnPublicIpAddress)
vm=(az vm create --subscription "$subscription" --resource-group "$resource_group" --name "$vm_name" --location "$location" --image Canonical:ubuntu-24_04-lts:server:latest --size "$vm_size" --security-type Standard --admin-username "$admin_user" --authentication-type ssh --ssh-key-values "$ssh_key" --nsg "$vm_name-nsg" --nsg-rule NONE --vnet-name "$vm_name-vnet" --subnet default --public-ip-sku Standard --storage-sku StandardSSD_LRS --os-disk-size-gb 64 --assign-identity)
print_command() { printf '%q ' "$@"; printf '\n'; }
print_command "${feature[@]}"
echo 'Wait for feature state Registered, then register Microsoft.Network before creation.'
print_command az provider register --subscription "$subscription" --namespace Microsoft.Network --wait
print_command "${group[@]}"
print_command "${nsg[@]}"
print_command "${subnet_nsg[@]}"
print_command "${vnet[@]}"
print_command "${subnet[@]}"
rules print_command
print_command "${vm[@]}"
echo 'SSH requires the corporate VPN and CorpNetPublic TCP 22 allows on BOTH NIC and subnet NSGs. Policy may delete rules: inspect both and recreate only the approved rules shown above.'
echo 'TEARDOWN (separate explicit approval required; deletes the ENTIRE dedicated resource group):'
print_command az group delete --subscription "$subscription" --name "$resource_group" --yes --no-wait
if [[ "$approved" != true ]]; then echo 'PLAN ONLY: no Azure commands executed. Review commands/cost, then explicitly approve before --approved-create.'; exit 0; fi
command -v az >/dev/null
command -v jq >/dev/null || { echo 'Install jq in the planning environment before approved creation.' >&2; exit 1; }
[[ $(az group exists --subscription "$subscription" --name "$resource_group") == false ]] || { echo 'Resource group exists; refusing to modify it.' >&2; exit 1; }
[[ $(az feature show --subscription "$subscription" --namespace Microsoft.Network --name AllowBringYourOwnPublicIpAddress --query properties.state -o tsv) == Registered ]] || {
    "${feature[@]}"
    echo 'Feature registration requested. Wait for Registered, run az provider register --namespace Microsoft.Network, then rerun the reviewed plan.' >&2
    exit 1
}
az provider register --subscription "$subscription" --namespace Microsoft.Network --wait
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
"${subnet_nsg[@]}"
"${vnet[@]}"
"${subnet[@]}"
rules command
"${vm[@]}"
echo 'Creation finished. Verify /dev/kvm on the guest; SKU capability is not runtime proof.'