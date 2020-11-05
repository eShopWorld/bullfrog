# Bullfrog configuration

Bullfrog supports multiple scale groups. Each scale group contains its own configuration of resources to be scaled and its own list of scale events. Sharing resources between scale groups is not supported.

Main elements of the scale group configurations are lists of regions and resources. The configuration defines one or more regions. The regions are only a way to group resources - they don’t have to map to Azure regions or contain only resources from the same Azure region. Additionally the configuration defines some scale group features.

Bullfrog can control two types of resources: virtual machine scale sets (VMSS) and Cosmos Db instances. VM scale sets are always part of a region. Cosmos DB usually are declared outside of regions, because they scale globally. It is possible to declare Cosmos DB in a region as long as this DB is not used in any other place.

> Warning: Change of the configuration can take a long time because some elements of configuration are validated (e.g. access permissions are tested). If this time exceeds the load balancer timeout then the call will return an error (503 Service Unavailable) but Bullfrog will still try to apply new configuration. If it fails then the details about configuration issues are written to the Bullfrog Application Insights log (please remember about a delay - usually about 2 minutes - between the event is logged and is available for querying in UI).

Not all elements of the new configuration are validated. It’s recommended to perform test scaling to validate that all elements are accessible and correctly configured.


### Virtual machine Scale Sets
Scaling of VMSS is done indirectly by modifying their autoscale settings. Bullfrog uses its own scaling profile to define the required scale of VMSS during a scale event. When the Bullfrog’s profile is added and removed as needed. When it is created its rules are copied from the specified default profile. Additionally a fixed date schedule is used in the Bullfrog’s. In all cases the maximum number of instances is left equal to the number in the default profile. The scaling only modifies the minimal (and default - see later) by setting it to the number of instances necessary to handle the expected traffic. The minimal number in the Bullfrog’s profile is always kept between the minimum and maximum number of instances defined by the default profile.

The autoscale settings can be modified directly using Azure Management API or indirectly using an Automation Account runbook. The later method is useful when the VMSS is part of a secure environment (e.g. PCI) and permissions to change autoscale settings cannot be granted to the BF account. In this case BF executes the specified runbook providing the scale set name, the number of instances and the expiration time of the BF profile.

```
{
  "name": "esw-we",
  "autoscaleSettingsResourceId": "/subscriptions/6f2887e2-6a85-4986-9f17-08ce7f57af39/resourceGroups/rgName/providers/microsoft.insights/autoscalesettings/vmss-autoscale",
  "profileName": "DefaultProfile",
  "loadBalancerResourceId": "/subscriptions/6f2887e2-6a85-4986-9f17-08ce7f57af39/resourceGroups/rgName/providers/Microsoft.Network/loadBalancers/vmss-loadbalancer",
  "healthPortPort": 24100,
  "requestsPerInstance": 700,
  "minInstanceCount": 5,
  "reservedInstances": 0.0,
  "runbook":  {
     "automationAccountName": "aa-name",
     "runbookName": "VmssScale",
     "scaleSetName": "ss-esw-we"
  }
}
```

- `name`: any identifier of the scale set, used in telemetry
- `autoscaleSettingsResourceId` - resource id of the autoscale settings which is read and modified by BF
- `profileName` - the name of the default profile in the specified autoscale settings
- `loadBalancerResourceId` - the resource id of the load balancer which handles the traffic of VMSS
- `healthPortPort` - the health probe port defined in the load balancer of any application
- `requestsPerInstance` - the number of requests a single instance of VMSS can handle
- `minInstanceCount` - the minimal number of instances. BF will not configure VMSS to use less instances than this number unless this number is lower than the maximum number defined by the default profile
- `reservedInstances` - the number of instances (or parts of them) which are excluded from calculations of required number of machines to handle a predicted treffic
-`runbook` - optional part which when given configures BF to use anAutomation Account runbook to modify the autoscale setting instead of doing it directly
- `runbook/automationAccountName` - the name of the automation account used to run a runbook. The automation accounts and their names are defined in the automationAccunts section of the main configuration
- `runbook/runbookName` - the name of the runbook
- `runbook/scaleSetName` - the optional name of scale set recognized by the runbook. If it not provided than the name value is used instead.


### Cosmos DB

Sample: 
```
{
    "name": "paymentsV2",
    "dataPlaneConnection": {
        "accountName": "esw-payments-prep",
        "databaseName": "payments-prep-v2",
        "containerName": null
    },
    "controlPlaneConnection": null,
    "requestUnitsPerRequest": 1.0,
    "minimumRU": 400,
    "maximumRU": 60000
},
{
    "name": "profilePCI",
    "dataPlaneConnection": null,
    "controlPlaneConnection": {
        "accountResurceId": "/subscriptions/9a291edb-63eb-4dbd-868d-6fc5fd670f62/resourceGroups/pci-preprod-we/providers/Microsoft.DocumentDB/databaseAccounts/we-pci-preprod-profile-store-72279",
        "databaseName": "payments-pci",
        "containerName": "shopperProfile"
    },
    "requestUnitsPerRequest": 1.0,
    "minimumRU": 400,
    "maximumRU": 50000
}
```

- `name` - name of the instance, used in telemetry
- `dataPlaneConnection` - an optional element configuring data plane connection to scale database instance
- `dataPlaneConnection/accountName` - the name of the account. This name is used as a part of the key in Bullfrog’s Key Vault to search for connection string to the database. The full name of the key is a concatenation of Bullfrog--Cosmos-- and the account name.
- `dataPlaneConnection/databaseName` - the database name
- `dataPlaneConnection/containerName` - an optional name of the container. If it is provided then the scaling is done at the container level. Otherwise it is done at the database level.
- `controlPlaneConnection` - an optional element which when used configures scaling of Cosmos DB instance using Azure Management API.
- `controlPlaneConnection/accountResourceId` - the resource id of the Cosmos DB account
- `controlPlaneConnection/databaseName` - the database name
- `controlPlaneConnection/containerName` - an optional name of the container. If it is provided then the scaling is done at the container level. Otherwise it is done at the database level.
- `requestUnitsPerRequest` - defines how many RUs are used by average by each requests. This value is multiplied by the requested scale to calculate the throughput requested from Cosmos DB.
- `minimumRU` - the minimum value of RUs. The database can enforce higher value.
- `maximumRU` - the maximim value of RUs which BF can request.

Either `dataPlaneConnection` or `controlPlaneConnection` (but not both) must be defined.