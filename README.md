# Bullfrog

[![Build Status](https://eshopworld.visualstudio.com/Github%20build/_apis/build/status/bullfrog?branchName=master)](https://eshopworld.visualstudio.com/Github%20build/_build/latest?definitionId=607)


The system which allows to schedule events during which specified Azure resources are prescaled to requested levels.

## How does it work

Bullfrog allows to define scale groups. Each scale group controls Azure resources by scaling them in or out based on requested requirements. Currently the scale group can control one virtual machine scale set and a list of Cosmos databases. For each of them scale group defines how to map number of requests to be processed to number of instances in the scale set or RUs in case of CosmosDB.

Scale events can be added to scale groups. Each scale event defines how many requests system should be able to process during specified time frame. The scale events may overlap. The system starts the prescaling of requested resources before the scale event begins to allow the resources to reach their requested levels on time.

## Configuration

1 Define scale groups
  * for each controled VM scale make the user-assigned identity used by Bullfrog a contributor of the resource group which contains VM scale set
  * for each controlled CosmosDB save the full (not read-only) connection string in the Bullfrog's key vault as a secret named Bullfrog--Cosmos--<CosmosDbName> where <CosmosDbName> is the name of Cosmos DB account.

3 Create scale events
  Scale events are identified by Guid. Saving a scale event will either update an existing scale event (if the scale event with the same Guid has already been defined) or create a new scale event.


