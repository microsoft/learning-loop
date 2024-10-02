# Deploy a Learning Loop

The deploy folder contains bicep scripts for deploying a sample Loop. These scripts can be used to deploy a self-contained loop environment or can be included in a more customized configuration.

## Prerequisites

- [PowerShell installed](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.4)
- [Azure CLI installed and authenticated](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli)
- [Docker Engine installed and running](https://docs.docker.com/engine/install/)

## Sample Deployment Script

The deploy-sample.ps1 script sets up a resource group to deploy the Learning Loop image (see `get-help ./deploy/scripts/deploy-sample.ps1`). The script executes in three phases.

- Phase 1 (optional) -
  deploys a resource group environment where the Loop resources will be deployed. If using an Azure container repository, the repository will be created here. Since Managed Identity is used by the Loop to access storage and Event Hub resources, a Managed Identity will also be created. If these resources exist, this phase can be skipped using -skipSetupEnvironment.

- Phase 2 (optional) -
  pushes the specified tarred Docker image to either an Azure container registry or a Docker Hub repository. If the image is already in a repository, this phase can be skipped using -loadAndPushDockerImage $false.

- Phase 3 (optional) -
  deploys the Loop container, the storage account, and the Event Hub. This phase may be skipped if -noDeploy is specified. In this case, a `parameters.bicepparam` file will be generated with the specified parameters; this file can be used to deploy the Loop using az directly.

A full deployment using the deploy-sample.ps1 will deploy all required resource and generate two files.

- \<loopName\>.bicep - contains the parameters for deploying the application environment; this includes the Learning Loop container, Event Hub, and the Storage Account. This parameters file is used with `main.bicep` and can be re-run independently of the script.
- \<loopName\>.config.json - contains the json config parameters for use with `rl_sim_cpp`

## Sample Deployment using an Azure ACR

The sample deployment will set up a resource group for your environment, create an Azure ACR, push the Docker image to the ACR, and deploy the Learning Loop with storage and Event Hub.

Deploy the Docker image from your build or get the Docker image from the GitHub build (currently located [here](https://github.com/microsoft/learning-loop/actions/runs/11109130866/artifacts/1996359925))

The general steps are:
1. Start the docker engine
2. Start PowerShell
3. Login to Azure (--use-device-code is recommended)
4. Navigate to the `deploy` directory
5. Run the deploy-sample.sh script

### Sample Deploy (Linux - not WSL)

```powershell
sudo systemctl start docker.service
sudo pwsh
az login --use-device-code
cd deploy
./scripts/deploy-sample.sh -dockerImageTar ../learning-loop-ubuntu-latest.tar
```

### Sample Deploy (Windows / Linux - WSL)

Run the below commands in PowerShell and ensure the Docker Engine is running.

```powershell
docker info
az login --use-device-code
cd deploy
./scripts/deploy-sample.sh -dockerImageTar ../learning-loop-ubuntu-latest.tar
```

## Next Steps

- [Send events to the Learning Loop (run rl_sim_cpp)](RL_SIM.md)
