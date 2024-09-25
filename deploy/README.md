### Deployment Description

The Bicep files in this folder can be used to customize the deployment of an rl_loop.

1. **Sample Parameters**: 
   - A `sample.bicepparam` file is provided as a starter template for parameter values.
   - This file supplies parameters to the `main.bicep` file.
   - For more information on the parameters in `sample.bicepparam`, see the descriptions provided in `mainconfigtypes.bicep`.

2. **Main Deployment File**:
   - The `main.bicep` file is the primary deployment script.
   - It references three module files: `storage.bicep`, `container.bicep`, and `eventhubs.bicep`.

3. **Configuration Types**:
   - The `mainconfigtypes.bicep` file helps configure the parameters for `main.bicep`.

4. **Common Functions**:
   - The `functions.bicep` file contains common transformation functions for creating uniform resource names.
   - To customize the resource names, modify the functions provided in this file.

5. **Customization**:
   - The `main.bicep` file supports most standard configurations.
   - Custom deployment scripts can be created by leveraging the provided modules.
   - If the provided modules do not meet specific needs, they can be used as a basis for creating custom modules.
   - Please note that resource names may be restricted in length and allowed characters. Ensure that the resource names comply with the naming restrictions imposed by the Azure resource.   