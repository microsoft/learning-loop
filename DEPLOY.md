# Deploy a Learning Loop

The deploy folder contains Bicep scripts for deploying a sample Loop. These scripts can be used to deploy a self contained loop environment or can be included in a more customized configuration.

**Note:** This document contains:

- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Manual Deployment](#manual-deployment-steps)
- [Sample Deployment Script Details](#sample-deployment-script-details)
- [Customize a deployment](#customize-a-deployment)

## Prerequisites

Before you begin, ensure you have the following:

1. **A Learning Loop Docker image.** See the [Docker Image Artifact](#docker-image-artifact).

2. **Required Tools:**
   - **Azure CLI**
   - **Docker Engine**
   - **Git** (Quick Start only)
   - **jq** (Linux only)

    All of these prerequisites should be available via your package manager. See the below links for the details of each.

    ### Linux

    - [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli)
    - [Docker Engine](https://docs.docker.com/engine/install/)
    - [Git](https://git-scm.com/downloads)
    - [jq](https://jqlang.github.io/jq/download/)

    ### Windows

    - [PowerShell](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.4)
    - [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli)
    - [Docker Engine](https://docs.docker.com/engine/install/)
    - [Git](https://git-scm.com/downloads)

## Quick Start

Use the deploy-sample script to set up a new Resource Group and deploy a sample loop. A successful deployment requires the ability to create and manage the following resources in your Azure Subscription

- Create Resource Groups
- Ability to assign roles within the Resource Group
- Create an Azure Container Registry (ACR)
- Create Managed Identities
- Create Storage Accounts
- Create EventHubs
- Create KeyVaults
- Create Azure Container Groups/Instances
- Create Application Insights

**Note: At this time `Quick Start` requires the ability to download the Learning-Loop Docker artifact (see step 1)**

1) <a id="quick-start-step1"></a>Download the Learning Loop Docker image artifact. See [Docker Image Artifact](#docker-image-artifact) section. Note the file path of the downloaded artifact zip file for use in step 6.

2) Clone the [learning-loop](https://github.com/microsoft/learning-loop) GitHub repository

    ```bash
    git clone https://github.com/microsoft/learning-loop.git
    cd learning-loop/deploy
    ```

3) Log in to Azure

    ```bash
    az login --use-device-code
    ```

4) Start the Docker Engine

    #### Linux (not Windows/WSL2)

    ```bash
    sudo systemctl start docker.service
    ```

    #### Windows (and Linux/WSL2)

    Launch the Docker Desktop application

6) Run the deploy-sample script substituting DOCKER-IMAGE-FILE-PATH with the Docker image artifact obtained from step 1.

    #### Linux Script

    ```bash
    chmod +x ./scripts/deploy-sample.sh
    ./scripts/deploy-sample.sh --dockerImageFile DOCKER-IMAGE-FILE-PATH
    ```

    #### Windows Script

    ```bash
    ./scripts/deploy-sample.ps1 -dockerImageFile DOCKER-IMAGE-FILE-PATH
    ```

## Next Steps

By default the sample deployment script is configured to deploy a container instance that runs rl_sim_cpp.  You may need to restart the container after the deployment.

See [send events to the Learning Loop (run rl_sim_cpp)](RL_SIM.md) to run the simulator application directly.

## Manual Deployment Steps

Create a Learning Loop manually. These steps will require you to login to the Azure Portal using your web browser and in a terminal session. And, as noted in the [Quick Start](#quick-start-step1) section, you will need to [download the Learning Loop artifact](#docker-image-artifact) or build the Docker image.

1) Use the `Deploy to Azure` button below to create a Resource Group, Managed Identity, Azure Insights, and an Azure Container Registry.

    [![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://aka.ms/AAsti3v)

    <img src="images/learning-loop-deploy-rg.png" alt="Create a Learning-Loop Resource Group" />

    You can accept the defaults and proceed to `Review + Create` and then `Create`.

    When deployment is complete, click on the left menu bar's `Outputs`. The values listed will be needed in Step 4 below.

    <img src="images/learning-loop-deploy-rg-outputs.png" alt="Resource Group Outputs" />

    `Tip: keep this browser page open for reference and use a new browser tab for Step 4.`

2) Load the Docker image to the Azure Container Registry.

    - Open a command line terminal

    - Login to Azure if your not already logged in.

        ```bash
        az login --use-device-code
        ```

    - Login to the ACR created in Step 1 (the name of the ACR is from Step 1 outputs).

        ```bash
        az acr login --name <YOUR-ACR-NAME>
        ```

    - Load the Docker image from the image tar file.

        ```bash
        docker load -i <PATH-TO-THE-DOCKER-TAR-FILE>
        ```

    - Tag the Docker image

        ```bash
        docker tag learning-loop:latest <YOUR-ACR-NAME>.azurecr.io/learning-loop:latest
        ```

    - Push the Docker image to the ACR

        ```bash
        docker push <YOUR-ACR-NAME>.azurecr.io/learning-loop:latest
        ```

3) Get your Client Object Id to assign roles for accessing the Learning Loop's Azure Storage and Azure EventHub.

    Copy the output of the following command for use in Step 4.

    ```bash
    az ad signed-in-user show --query 'id' --output tsv
    ```

4) Use the `Deploy to Azure` button below to create Learning Loop resources (Azure Storage, Azure EventHub, and the Learning Loop Container).

    [![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://aka.ms/AAstpwq)

    <img src="images/learning-loop-deploy-ll.png" alt="Deploy the Learning Loop" />

    - Select the Resource Group created in Step 1 from the drop-down.
    - Select True for `App Insights Enabled`
    - Copy and paste the Application Insights Connection string from the output of Step 1.
    - Copy and paste your Client Object Id from step 3 into `User Role Assignment Principal Id`
    - You can accept the defaults and proceed to `Review + Create` and then `Create`.

    When deployment is complete, click on the left menu bar's `Outputs`. There are two output values `rlSimConfigAz` and `rlSimConfigConnStr`.  Copy and paste the value from `rlSimConfigAz` into a new file named rl_sim_config.json (or give it any name you like).

    <img src="images/learning-loop-deploy-ll-outputs.png" alt="Learning Loop Deployment Outputs" />

5) Prepare the Learning Loop's storage for the Learning Loop simulator.

    Since the Azure Storage Blob is newly created, it needs to be prepared for use with rl_sim_cpp by copying an empty model file as follows.

    If you deployed with different Storage Account name or Learning Loop name, change the command below to match. For example:

    *az storage blob upload --account-name MY-STORAGE-ACCOUNT-NAME --container-name "MY-LEARNING-LOOP-NAME/exported-models" --file ./empty_model --name "current"*

    ```bash
    echo '' > ./empty_model
    az storage blob upload --account-name sampleloopstg --container-name "sample-loop/exported-models" --file ./empty_model --name "current"
    rm ./empty_model
    ```

### Next Steps

By default the sample deployment is configured to deploy a container instance that runs rl_sim_cpp.  You may need to restart the container after the deployment.

See [send events to the Learning Loop (run rl_sim_cpp)](RL_SIM.md) to run the simulator application directly.

## Docker Image Artifact

The Docker image is currently built with GitHub Actions and does not currently reside in a public repository.  We are working to make the image public and will update this document when complete.

In the meantime, download the Learning Loop Docker image artifact from the [latest successful build](https://github.com/microsoft/learning-loop/actions?query=is%3Asuccess). Click on the link to the latest successful build, then click on the Artifacts link located in the header of the page (or scroll to the bottom of the page). Select the link labeled `docker-image-ubuntu-latest`.

<img src="images/learning-loop-artifacts.png" alt="Learning-Loop Artifacts" width="50%"/>

*If you are unable to access the artifacts, you will need to [build the project](BUILD.md) and [the Docker image](DOCKER.md).*

### Unpack the tar'd Docker image

If you downloaded the Docker image artifact from GitHub, you will have a zip file containing the Docker image tar file.  For the [Quick Start](#quick-start) steps this is all you need. For the [Manual Deployment Steps](#manual-deployment-steps), you will need to manually unzip the file.

- unzip the artifacts file.

    ```bash
    unzip docker-image-ubuntu-latest.zip
    ```

`learning-loop-ubuntu-latest.tar` is Learning Loop Docker image tar file needed for [Manual Deployment Steps](#manual-deployment-steps)

## Sample Deployment Script Details

The deploy-sample script sets up a resource group to deploy the Learning Loop image (see `get-help ./deploy/scripts/deploy-sample.ps1` or `./deploy/scripts/deploy-sample.sh --help`). The script executes in three phases.

- **Phase 1**: creates a Resource Group where the Loop resources will be deployed. If using an Azure Container Registry, the repository will be created here. Since Managed Identity is used by the Loop to access storage and EventHub resources, a Managed Identity will also be created.

- **Phase 2**: pushes the specified tar'd Docker image to either an Azure Container Registry or a Docker Hub repository.

- **Phase 3**: deploys the Azure Container, the Storage Account, and the EventHub.

A full deployment using the deploy-sample script will deploy all required resources and generate two files.

- `<loopName>.bicep` - contains the parameters for deploying the application environment, including the Learning Loop container, EventHub, and the Storage Account. This parameters file is used with `main.bicep` and can be re-run independently of the script.
- `<loopName>.config.json` - contains the JSON config parameters for use with `rl_sim_cpp`

## Customize a deployment

See the Bicep scripts and [README](deploy/README.md) in the project deploy folder for deployment details.
