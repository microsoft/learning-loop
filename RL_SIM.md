# Reinforcement Learning Simulator

The [reinforcement learning simulator](https://github.com/VowpalWabbit/reinforcement_learning/tree/master/examples/rl_sim_cpp) (`rl_sim_cpp`) can be used to exercise the Learning Loop.

As part of the [deployment](DEPLOY.md), an rl_sim_cpp client configuration is `generated`. By default, the configuration is generated for use with Azure Credentials.

rl_sim_cpp must be built with `RL_LINK_AZURE_LIBS` to use Azure Credentials. Alternatively, a connection string can be used.

## rl_sim_cpp with Azure Credentials

To use Azure Credentials with rl_sim_cpp, the logged-in user should have the following roles:

- `Azure Event Hubs Data Sender`
- `Azure Event Hubs Data Receiver`

These roles are applied by default with the [sample-deploy](DEPLOY.md) script.

The following is an example config file:

```json
{
   "ApplicationID": "sample-loop",
   "IsExplorationEnabled": true,
   "InitialExplorationEpsilon": 1.0,
   "http.api.header.key.name": "Authorization",
   "http.api.oauth.token.type": "Bearer",
   "interaction.sender.implementation": "INTERACTION_HTTP_API_SENDER_OAUTH_AZ",
   "interaction.eventhub.name": "interaction",
   "interaction.http.api.host": "https://sample-loop-eh.servicebus.windows.net:443/interaction/messages",
   "observation.sender.implementation": "OBSERVATION_HTTP_API_SENDER_OAUTH_AZ",
   "observation.eventhub.name": "observation",
   "observation.http.api.host": "https://sample-loop-eh.servicebus.windows.net:443/observation/messages",
   "model.vw.initial_command_line": "--cb_explore_adf --epsilon 0.2 --power_t 0 -l 0.001 --cb_type ips -q ::",
   "protocol.version": 2,
   "model.source": "FILE_MODEL_DATA"
}
```

## rl_sim_cpp with EventHub Connection String

To use an EventHub Connection String, copy the connection string–primary key from the Event Hub's Shared Access Policies.

The following is an example config file.

```json
{
   "ApplicationID": "sample-loop",
   "IsExplorationEnabled": true,
   "InitialExplorationEpsilon": 1.0,
   "EventHubInteractionConnectionString": "Endpoint=sb://sample-loop.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=************;EntityPath=interaction",
   "EventHubObservationConnectionString": "Endpoint=sb://sample-loop.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=************;EntityPath=observation",
   "model.vw.initial_command_line": "--cb_explore_adf --epsilon 0.2 --power_t 0 -l 0.001 --cb_type ips -q ::",
   "protocol.version": 2,
   "model.source": "FILE_MODEL_DATA"
}
```

**Security Note:** Ensure that your credentials, especially connection strings with shared access keys, are kept secure and not exposed in public repoistories, files, or logs.

## Running rl_sim_cpp

```sh
rl_sim_cpp -j sample-loop.config.json
```
