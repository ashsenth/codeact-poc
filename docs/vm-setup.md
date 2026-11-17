# Ubuntu x64 VM setup

Prepare [Azure resources and permissions](azure-setup.md), then use this guide for real Hyperlight execution. Continue with [running experiments](running-experiments.md); connection help is in [troubleshooting](troubleshooting.md).

## Ubuntu x64 VM and real Hyperlight

Use Ubuntu 24.04 **x64**, with nested virtualization and readable/writable `/dev/kvm`. The recorded host used `Standard_D4s_v5`. SKU documentation alone is not proof of working KVM; ARM64, ordinary containers and hosted development environments are not substitutes.

### Provisioning and corporate-network SSH

Review current VM price/quota, networking and corporate policy first. The historical USD 0.192/hour estimate is not current approval.

For this corporate environment register the subscription feature before provisioning the approved public-IP setup:

```bash
az feature register --namespace Microsoft.Network --name AllowBringYourOwnPublicIpAddress
az feature show --namespace Microsoft.Network --name AllowBringYourOwnPublicIpAddress \
  --query properties.state -o tsv
# Wait for Registered; then refresh provider registration.
az provider register --namespace Microsoft.Network
```

Registration can require administrator approval. Connect the client to the corporate VPN. **Both the NIC NSG and subnet NSG must allow TCP 22 from `CorpNetPublic`.** A rule on only one NSG is insufficient.

The [provisioning script](../scripts/provision-vm.sh) now creates separate NIC/subnet NSGs, the VNet/subnet association, and CorpNetPublic rules on both. It no longer discovers your current public IP or accepts `--ssh-ip`. Its ECDSA public-key token bug is fixed. It prints a plan by default; creation requires a new dedicated resource group and explicit approval:

```bash
export VM_RG="<new-vm-resource-group>" VM_NAME="<vm-name>" VM_REGION="<approved-vm-region>"
export ADMIN_USER="<ssh-user>" SSH_PUBLIC_KEY="<absolute-path-to-approved-public-key.pub>"
bash scripts/provision-vm.sh --subscription "$SUBSCRIPTION" --resource-group "$VM_RG" \
  --location "$VM_REGION" --name "$VM_NAME" --admin-user "$ADMIN_USER" \
  --ssh-public-key "$SSH_PUBLIC_KEY" --estimated-hourly-usd "<reviewed-compute-price>"
# Review the complete plan, feature registration, price/quota and policy; only then
# repeat the same command with --approved-create.
```

Corporate policy can delete SSH allow rules after creation. Before first connection and after a timeout/restart inspect BOTH associations/effective rules:

```bash
NIC_ID="$(az vm show --resource-group "$VM_RG" --name "$VM_NAME" \
  --query 'networkProfile.networkInterfaces[0].id' -o tsv)"
SUBNET_ID="$(az network nic show --ids "$NIC_ID" --query 'ipConfigurations[0].subnet.id' -o tsv)"
az network nic show --ids "$NIC_ID" --query networkSecurityGroup.id -o tsv
az network vnet subnet show --ids "$SUBNET_ID" --query networkSecurityGroup.id -o tsv
az network nic list-effective-nsg --ids "$NIC_ID"
```

If policy removed the approved allow, an authorized operator can recreate it on each affected NSG (script-created names below):

```bash
for nsg in "$VM_NAME-nsg" "$VM_NAME-subnet-nsg"; do
  az network nsg rule create --resource-group "$VM_RG" --nsg-name "$nsg" \
    --name AllowSshFromCorpNet --priority 100 --direction Inbound --access Allow \
    --protocol Tcp --source-address-prefixes CorpNetPublic --source-port-ranges '*' \
    --destination-address-prefixes '*' --destination-port-ranges 22
done
```

Resolve recurring deletion with the policy administrator; do not bypass policy with a world-open/current-IP rule. If `CorpNetPublic` is unavailable, use only the administrator-approved equivalent. Verify the SSH host-key fingerprint through a trusted channel, then connect from the VPN:

```bash
ssh -i "<client-private-key-path>" "<ssh-user>@<vm-public-ip>"
```

### Repository access, bootstrap and guest path

On the VM use a repository-specific deploy key approved by the repository administrator. Generate it in the SSH user's home, never the checkout; register only the `.pub` value in repository **Settings > Deploy keys**. Read access suffices for cloning; automatic publication requires **Allow write access**. Protect the private file and never publish key material or authentication logs.

```bash
ssh-keygen -t ed25519 -f "$HOME/.ssh/codeact-repo" -C "benchmark-repo-deploy-key"
chmod 600 "$HOME/.ssh/codeact-repo"
```

Verify GitHub host fingerprints through the organization's trusted process; never disable host-key checking. Load the key into an approved agent, or set a process-local transport:

```bash
export GIT_SSH_COMMAND="ssh -i $HOME/.ssh/codeact-repo -o IdentitiesOnly=yes"
git clone "<approved-SSH-repository-URL>" codeact-poc
cd codeact-poc
git switch "<approved-results-branch>"
git config --local user.name "<approved-commit-author>"
git config --local user.email "<approved-commit-email>"
bash scripts/bootstrap-vm.sh
# After reviewing its plan and obtaining installation approval:
bash scripts/bootstrap-vm.sh --approved-install
```

Bootstrap installs prerequisites, pinned .NET SDK, Azure CLI, PowerShell and Hyperlight Python 0.4.0, loads KVM and adds the SSH user to `kvm`. Its current ordinary-sudo calls require an approved interactive sudo authentication setup; do not run it as an unattended job that may wait for a password.

Disconnect **all** SSH/Remote-SSH sessions and reconnect so the new group applies:

```bash
cd "<clone-directory>"
id -nG
test -r /dev/kvm && test -w /dev/kvm
export HYPERLIGHT_PYTHON_GUEST_PATH="$PWD/artifacts/guest/0.4.0/runtimes/linux-x64/native/python-sandbox.aot"
test -f "$HYPERLIGHT_PYTHON_GUEST_PATH"
export DATASET_ROOT="$PWD/data"
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet run -c Release --no-build --project src/SterlingVale.DataGenerator -- "$DATASET_ROOT"
dotnet test tests/SterlingVale.CodeAct.IntegrationTests -c Release --no-build \
  --filter 'Category=Hyperlight&Category!=HyperlightKnownIssue' \
  --blame-hang --blame-hang-timeout 3m
```

Require actual execution with zero skips; the supported selection has six tests. **Never include the retained native-cancellation known-issue test in an unattended unfiltered run.** Always use an absolute guest path: testhost changes its working directory. Guest presence is not proof of callbacks or correct execution.

Optional no-Azure real-guest feasibility check, using a fresh directory outside model cohort roots:

```bash
dotnet scripts/vm/Diagnosis/bin/Release/net9.0/Diagnosis.dll control \
  scripts/vm/reference-analysis-control.py "<new-control-output-directory>"
```

### Auto-shutdown, deallocation and restart

Configure reviewed auto-shutdown as a cost backstop, outside the cohort run window:

```bash
az vm auto-shutdown --resource-group "$VM_RG" --name "$VM_NAME" --time "<HHmm-UTC>"
# After all runs/publication finish, explicitly stop billable compute:
az vm deallocate --resource-group "$VM_RG" --name "$VM_NAME"
az vm get-instance-view --resource-group "$VM_RG" --name "$VM_NAME" \
  --query "instanceView.statuses[?starts_with(code, 'PowerState/')].code" -o tsv
# Resume later:
az vm start --resource-group "$VM_RG" --name "$VM_NAME"
az vm show --resource-group "$VM_RG" --name "$VM_NAME" --show-details --query publicIps -o tsv
```

Verify `PowerState/deallocated`: an OS shutdown or stopped-but-allocated VM may still incur compute charges. Disks and some networking remain billable. Recheck public IP, VPN and BOTH NSGs after restart; restore shell environment, verify KVM/guest, identity access and repository state. Never deallocate while the controller is running or publishing.
