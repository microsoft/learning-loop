// Generate the RL Sim json configuration based on the Learning Loop deployment
param loopName string
param eventHubEndpoint string
param storageBlobEndpoint string

var rlSimConfigConnStrVal_0 = replace('''
{
   "ApplicationID": "{loopName}",
   "IsExplorationEnabled": true,
   "InitialExplorationEpsilon": 1.0,
   "EventHubInteractionConnectionString": "<EVENTHUB_CONNECTION_STRING>;EntityPath=interaction",
   "EventHubObservationConnectionString": "<EVENTHUB_CONNECTION_STRING>;EntityPath=observation",
   "model.vw.initial_command_line": "--cb_explore_adf --epsilon 0.2 --power_t 0 -l 0.001 --cb_type ips -q ::",
   "protocol.version": 2,
   "model.source": "HTTP_MODEL_DATA",
   "model.blob.uri": "{storageBlobEndpoint}/exported-models/current?<TOKEN_PLACEHOLDER>"
}
''', '{loopName}', loopName)
var rlSimConfigConnStrValFinal = replace(rlSimConfigConnStrVal_0, '{storageBlobEndpoint}', storageBlobEndpoint)
var rlSimConfigConnStrVal = rlSimConfigConnStrValFinal

var rlSimConfigAzVal_0 = replace('''
{
   "ApplicationID": "{loopName}",
   "IsExplorationEnabled": true,
   "InitialExplorationEpsilon": 1.0,
   "http.api.header.key.name": "Authorization",
   "http.api.oauth.token.type": "Bearer",
   "interaction.sender.implementation": "INTERACTION_HTTP_API_SENDER_OAUTH_AZ",
   "interaction.eventhub.name": "interaction",
   "interaction.http.api.host": "{eventHubEndpoint}interaction/messages",
   "observation.sender.implementation": "OBSERVATION_HTTP_API_SENDER_OAUTH_AZ",
   "observation.eventhub.name": "observation",
   "observation.http.api.host": "{eventHubEndpoint}observation/messages",
   "model.vw.initial_command_line": "--cb_explore_adf --epsilon 0.2 --power_t 0 -l 0.001 --cb_type ips -q ::",
   "protocol.version": 2,
   "model.source": "HTTP_MODEL_DATA_OAUTH_AZ",
   "model.blob.uri": "{storageBlobEndpoint}/exported-models/current"
}
''', '{loopName}', loopName)
var rlSimConfigAzVal_1 = replace(rlSimConfigAzVal_0, '{eventHubEndpoint}', eventHubEndpoint)
var rlSimConfigAzValFinal = replace(rlSimConfigAzVal_1, '{storageBlobEndpoint}', storageBlobEndpoint)
var rlSimConfigAzVal = rlSimConfigAzValFinal

output rlSimConfigAz string = rlSimConfigAzVal
output rlSimConfigConnStr string = rlSimConfigConnStrVal
