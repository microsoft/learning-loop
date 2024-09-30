// integration environment parameters
using 'main.bicep'

param kvImageRegistryUsername = getSecret('mysubscriptionid', 'myresourcegroup', 'keyvaultname', 'imageRegistryUsername')
param kvImageRegistryPassword = getSecret('mysubscriptionid', 'myresourcegroup', 'keyvaultname', 'imageRegistryPassword')

param mainConfig = {
  appName: 'sampleloop'
  // see full list of supported environment variables below
  environmentVars: [
    {
      name: 'ExperimentalUnitDuration'
      value: '0:0:10'
    }
    {
      name: 'TrainerEnabled'
      value: true
    }
    {
      name: 'JoinerEnabled'
      value: true
    }
    {
      name: 'MachineLearningArguments'
      value: '--cb_explore_adf --epsilon 0.2 --power_t 0 -l 0.001 --cb_type ips -q ::'
    }
    {
      name: 'LastConfigurationEditDate'
      value: '2024-01-01'
    }
  ]
  resourceTags: {
    deploymentGroupName: 'sample_loop'
  }
  storage: {
    sku: 'Standard_LRS'
    kind: 'StorageV2'
  }
  eventhub: {
    capacity: 1
    partitionCount: 4
    sku: 'Standard'
    messageRetentionDays: 1
  }
  container: {
    cpuCores: 4
    memoryGig: 16
    image: {
      registry: {
        host: 'docker.io'
        credentials: {
          type: 'keyVault'
        }
      }
      name: 'learningloop/rl_loop'
      tag: 'latest'
    }
  }
}

///////////////////////////////////////////////
// application environment variables

// environmentVars = [
  /////////////////////////////////////////////
  // trainer config
  // {
  //   // optional = default = false
  //   // enable/disable the trainer
  //   name: 'TrainerEnabled'
  //   value: true
  // }
  // {
  //   // required for trainer
  //   // Machine learning arguments for VW
  //   name: 'MachineLearningArguments'
  //   value: '--cb_explore_adf --epsilon 0.2 --power_t 0 -l 0.001 --cb_type ips -q ::'
  // }
  // { // optional
  //   name: 'ExplorationPercentage'
  //   value: '0.2'
  // }
  // {
  //   // optional - default = null
  //   // Warmstart model storage URL
  //   name: 'WarmstartModelUrl'
  //   value: 'https://mystorage.blob.core.windows.net/mymodel/model'
  // }
  // {
  //   // optional - default = 0.0
  //   // Default reward value to use when no reward is provided in the event.
  //   name: 'DefaultReward'
  //   value: '1.0'
  // }
  // {
  //   // optional - default = 1 minute
  //   // Frequency of model checkpoint
  //   name: 'ModelCheckpointFrequency'
  //   value: '00:01:00'
  // }
  // {
  //   // optional - default = 1 minute
  //   // Frequency of model export
  //   name: 'ModelExportFrequency'
  //   value: '00:01:00'
  // }
  // {
  //   // optional - default = true
  //   // When auto publish is enabled (= default), the trainer exports the model and override the "current" model.
  //   // When auto publish is disabled, the trainer exports models but does not override the "current" model
  //   // (this is done by the user through API calls).
  //   name: 'ModelAutoPublish'
  //   value: 'true'
  // }
  // { 
  //   // optional - default = 10
  //   // Length of staged models history in days.
  //   // Old staged models are automatically deleted from history.
  //   name: 'StagedModelHistoryLength'
  //   value: '10'
  // }
  // {
  //   // optional - default = 1
  //   // Size of block buffer used to process a batch of events, resulting in a block in storage.
  //   // Be careful of this size, each may mean 100MB, so this number should be small, like 1 or 2
  //   name: 'BlockBufferCapacityForEventBatch'
  //   value: '1'
  // }
  // { 
  //   // optional - default = null
  //   // The VW binary to use for training. If null, this tries to select a bundled VW binary.
  //   name: 'VwBinaryPath'
  //   value: '/usr/local/bin/vw'
  // }

  /////////////////////////////////////////////
  // joiner config
  // {
  //   // optional - default = false
  //   // enable/disable the joiner
  //   name: 'JoinerEnabled'
  //   value: true
  // }
  // {
  // // required
  // // the duration of the experimental unit used for joining events
  // // joins are performed on events that are within the same experimental unit
  //   name: 'ExperimentalUnitDuration'
  //   value: '0:0:10'
  // }
  // {
  //   // optional - default = '00:02:00'
  //   // join lookback window duration
  //   name: 'BackwardEventJoinWindowTimeSpan'
  //   value: '00:02:00'
  // }
  // {
  //   // optional - default = 'false'
  //   // the the client supplied timestamp instead of the event timestamps
  //   name: 'UseClientTimestamp'
  //   value: 'true'
  // }
  // {
  //   // optional - default = 'interaction'
  //   // the name of the interaction event hub
  //   name: 'InteractionHubName'
  //   value: 'interaction'
  // }
  // {
  //   // the name of the observation event hub
  //   // optional - default = 'observation'
  //   name: 'ObservationHubName'
  //   value: 'observation'
  // }
  // {
  //   // optional - default = '00:00:30'
  //   name: 'PunctuationTimeout'
  //   value: '00:00:30'
  // }
  // {
  //   // optional - default = '00:00:05'
  //   // To avoid clock issues punctuations are send (UtcNow - slack) in joiner.
  //   // enable with AddPunctuationSlack = true
  //   name: 'PunctuationSlack'
  //   value: '00:00:05'
  // }
  // {
  //   // optional - default = 'false'
  //   // Flag to adjust from using the fast (UtcNow - slack) in joiner to (last + slack).
  //   name: 'AddPunctuationSlack'
  //   value: 'true'
  // }
  // {
  //   // optional - default = '100'
  //   // The number of times we will retry when trying to receive an event times out
  //   // in the LOJ block.
  //   name: 'EventReceiveTimeoutMaxRetryCount'
  //   value: '100'
  // }
  // {
  //   // optional - default = '00:01:00'
  //   // Time to wait for events to be ready on a partition before skipping it.
  //   name: 'ActivePartitionReadTimeout'
  //   value: '00:01:00'
  // }
  // {
  //   // optional - default = '00:00:10'
  //   // Partitions Source maximum wait time when its event source is not able to read data.
  //   name: 'EventHubReceiveTimeout'
  //   value: '00:00:10'
  // }
  // {
  //   // optional - default = null
  //   // The input folder to be used when in local JoinerFiles mode.
  //   // This is only for JoinerFiles and setting it causes JoinerFiles to be used.
  //   name: 'JoinerFilesInputDirectory'
  //   value: '/mnt/joiner_files'
  // }
  // {
  //   // optional - default = '16'
  //   // Size of block buffer used in EventMergeSort
  //   name: 'EventMergeSortBlockBufferSize'
  //   value: '16'
  // }
  // {
  //   // optional - default = 'false'
  //   // Toggles use of more verbose metrics.
  //   name: 'LOJVerboseMetricsEnabled'
  //   value: 'true'
  // }
  // {
  //   // optional - default = 'false'
  //   // Setting to enable log mirror to another storage account.
  //   name: 'LogMirrorEnabled'
  //   value: 'true'
  // }
  // {
  //   // optional - default = null
  //   // Storage account Sas Uri for log mirroring.
  //   name: 'LogMirrorSasUri'
  //   value: 'https://mystorage.blob.core.windows.net/mymodel/model'
  // }
  // {
  //   // optional - default = '1'
  //   name: 'BlockBufferCapacityForEventBatch'
  //   value: '1'
  // }
  // {
  //   // optional - default = '1'
  //   name: 'BlockBufferCapacityForEventBatch'
  //   value: '1'
  // }
  // {
  //   // optional - default = 'false'
  //   name: 'IsBillingEnabled'
  //   value: 'true'
  // }

  /////////////////////////////////////////////
  // shared config
  // { // required
  //   name: 'LastConfigurationEditDate'
  //   value: '2017-08-14'
  // }
  // {
  //   // optional - default = null
  //   // Local path to use for cooked log storage.
  //   name: 'LocalCookedLogsPath'
  //   value: '/mnt/cooked_logs'
  // }
  // {
  //   // optional - default = null
  //   // Start date time for warmstart model
  //   name: 'WarmstartStartDateTime'
  //   value: '2021-08-01T00:00:00Z'
  // }
//]
