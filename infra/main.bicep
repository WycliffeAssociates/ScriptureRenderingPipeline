@description('Bus to handle all events and messages for the Scripture Rendering Pipeline')
param bus_name string = 'wabusdev'

var subscriptionProperties = {
    isClientAffine: false
    lockDuration: 'PT5M'
    requiresSession: false
    defaultMessageTimeToLive: 'P14D'
    deadLetteringOnMessageExpiration: false
    deadLetteringOnFilterEvaluationExceptions: false
    maxDeliveryCount: 10
    status: 'Active'
    enableBatchedOperations: true
    autoDeleteOnIdle: false
}

// Rules for filtering messages in subscriptions
var onlyPushCorrelationFilterRule = {
  action: {}
  filterType: 'CorrelationFilter'
  correlationFilter: {
    properties: {
      EventType: 'push'
    }
  }
}

var onlyDeletesFilter = {
  action: {}
  filterType: 'CorrelationFilter'
  correlationFilter: {
    properties: {
      Action: 'Delete'
      EventType: 'Repo'
    }
  }
}

var everythingRepoFilter = {
  action: {}
  filterType: 'CorrelationFilter'
  correlationFilter: {
    properties: {
      EventType: 'Repo'
    }
  }
}

var everythingFilter = {
  action: {}
  filterType: 'SqlFilter'
  sqlFilter: {
    sqlExpression: '1=1'
    compatibilityLevel: 20
  }
}

var onlySuccessFilter = {
  action: {}
  filterType: 'SqlFilter'
  sqlFilter: {
    sqlExpression: 'user.success=true'
    compatibilityLevel: 20
  }
}

var onlyNewContentFilter = {
  action: {}
  filterType: 'SqlFilter'
  sqlFilter: {
    sqlExpression: 'eventType=\'push\' or eventType=\'fork\' or eventType=\'create\''
    compatibilityLevel: 20
  }
}

var topicProperties = {
    maxMessageSizeInKilobytes: 256
    defaultMessageTimeToLive: 'P14D'
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: false
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    enableBatchedOperations: true
    status: 'Active'
    supportOrdering: false
    autoDeleteOnIdle: false
    enablePartitioning: false
    enableExpress: false
}


resource namespaces_wabus_name_resource 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: bus_name
  location: 'eastus'
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    premiumMessagingPartitions: 0
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
    zoneRedundant: true
  }
}

resource namespaces_wabus_name_audiobiel 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = {
  parent: namespaces_wabus_name_resource
  name: 'audiobiel'
  properties: topicProperties
}

resource namespaces_wabus_name_cataloggenerated 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = {
  parent: namespaces_wabus_name_resource
  name: 'cataloggenerated'
  properties: topicProperties
}

resource namespaces_wabus_name_lintingresult 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = {
  parent: namespaces_wabus_name_resource
  name: 'lintingresult'
  properties: topicProperties
}

// Repo merging topics

resource namespaces_wabus_name_mergedresult 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = {
  parent: namespaces_wabus_name_resource
  name: 'mergedresult'
  properties: topicProperties
}

resource namespaces_wabus_name_mergerequest 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = {
  parent: namespaces_wabus_name_resource
  name: 'mergerequest'
  properties: topicProperties
}

resource namespaces_wabus_name_reporendered 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = {
  parent: namespaces_wabus_name_resource
  name: 'reporendered'
  properties: topicProperties
}

resource namespaces_wabus_name_versecountingresult 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = {
  parent: namespaces_wabus_name_resource
  name: 'versecountingresult'
  properties: topicProperties
}

resource namespaces_wabus_name_wacsevent 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = {
  parent: namespaces_wabus_name_resource
  name: 'wacsevent'
  properties: topicProperties
}

// Subscriptions ----------------------------------------------


resource namespaces_wabus_name_versecountingresult_InternalProcessor 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  parent: namespaces_wabus_name_versecountingresult
  name: 'InternalProcessor'
  properties: subscriptionProperties
}

resource namespaces_wabus_name_wacsevent_InternalProcessor 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  parent: namespaces_wabus_name_wacsevent
  name: 'InternalProcessor'
  properties: subscriptionProperties
}

resource namespaces_wabus_name_audiobiel_languageapi 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  parent: namespaces_wabus_name_audiobiel
  name: 'languageapi'
  properties: subscriptionProperties
}

resource namespaces_wabus_name_wacsevent_languageapi 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  parent: namespaces_wabus_name_wacsevent
  name: 'languageapi'
  properties: subscriptionProperties
}

resource namespaces_wabus_name_wacsevent_LarrysScripts 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  parent: namespaces_wabus_name_wacsevent
  name: 'LarrysScripts'
  properties: subscriptionProperties
}

resource namespaces_wabus_name_mergerequest_MergeRequest 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  parent: namespaces_wabus_name_mergerequest
  name: 'MergeRequest'
  properties: subscriptionProperties
}

resource namespaces_wabus_name_mergedresult_NotificationHandler 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  parent: namespaces_wabus_name_mergedresult
  name: 'NotificationHandler'
  properties: subscriptionProperties
}

resource namespaces_wabus_name_wacsevent_RenderingWebhook 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  parent: namespaces_wabus_name_wacsevent
  name: 'RenderingWebhook'
  properties: subscriptionProperties
}

resource namespaces_wabus_name_reporendered_reporendered_languageapi 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  parent: namespaces_wabus_name_reporendered
  name: 'reporendered-languageapi'
  properties: subscriptionProperties
}

resource namespaces_wabus_name_cataloggenerated_ResourceArchiveGenerator 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  parent: namespaces_wabus_name_cataloggenerated
  name: 'ResourceArchiveGenerator'
  properties: subscriptionProperties
}

resource namespaces_wabus_name_lintingresult_ResultsUI 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  parent: namespaces_wabus_name_lintingresult
  name: 'ResultsUI'
  properties: subscriptionProperties
}

resource namespaces_wabus_name_wacsevent_VerseCounting 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  parent: namespaces_wabus_name_wacsevent
  name: 'VerseCounting'
  properties: subscriptionProperties
}

resource namespaces_wabus_name_wacsevent_Watchdog 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  parent: namespaces_wabus_name_wacsevent
  name: 'Watchdog'
  properties: subscriptionProperties
}

resource namespaces_wabus_name_audiobiel_languageapi_Default 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2024-01-01' = {
  parent: namespaces_wabus_name_audiobiel_languageapi
  name: '$Default'
  properties: everythingFilter
}

resource namespaces_wabus_name_cataloggenerated_ResourceArchiveGenerator_Default 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2024-01-01' = {
  parent: namespaces_wabus_name_cataloggenerated_ResourceArchiveGenerator
  name: '$Default'
  properties: everythingFilter
}

resource namespaces_wabus_name_mergedresult_NotificationHandler_Default 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2024-01-01' = {
  parent: namespaces_wabus_name_mergedresult_NotificationHandler
  name: '$Default'
  properties: everythingFilter
}

resource namespaces_wabus_name_mergerequest_MergeRequest_Default 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2024-01-01' = {
  parent: namespaces_wabus_name_mergerequest_MergeRequest
  name: '$Default'
  properties: everythingFilter
}

resource namespaces_wabus_name_reporendered_reporendered_languageapi_Default 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2024-01-01' = {
  parent: namespaces_wabus_name_reporendered_reporendered_languageapi
  name: '$Default'
  properties: everythingFilter
}

resource namespaces_wabus_name_versecountingresult_InternalProcessor_Default 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2024-01-01' = {
  parent: namespaces_wabus_name_versecountingresult_InternalProcessor
  name: '$Default'
  properties: onlySuccessFilter
}

resource namespaces_wabus_name_wacsevent_languageapi_Default 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2024-01-01' = {
  parent: namespaces_wabus_name_wacsevent_languageapi
  name: '$Default'
  properties: everythingFilter
}

resource namespaces_wabus_name_wacsevent_LarrysScripts_Default 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2024-01-01' = {
  parent: namespaces_wabus_name_wacsevent_LarrysScripts
  name: '$Default'
  properties: onlyNewContentFilter
}


resource namespaces_wabus_name_wacsevent_Watchdog_Default 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2024-01-01' = {
  parent: namespaces_wabus_name_wacsevent_Watchdog
  name: '$Default'
  properties: everythingFilter
}


resource namespaces_wabus_name_wacsevent_InternalProcessor_OnlyDeletes 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2024-01-01' = {
  parent: namespaces_wabus_name_wacsevent_InternalProcessor
  name: 'OnlyDeletes'
  properties: onlyDeletesFilter
}

resource namespaces_wabus_name_wacsevent_RenderingWebhook_OnlyPush 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2024-01-01' = {
  parent: namespaces_wabus_name_wacsevent_RenderingWebhook
  name: 'OnlyPush'
  properties: onlyNewContentFilter
}

resource namespaces_wabus_name_wacsevent_VerseCounting_OnlyPush 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2024-01-01' = {
  parent: namespaces_wabus_name_wacsevent_VerseCounting
  name: 'OnlyPush'
  properties: onlyNewContentFilter
}
