# Bullfrog API


### Configurations - GET
List names of all configured scale groups
Scopes: `bullfrog.api.all`


### Configurations - GET /{scaleGroup}
Get the definition of the specified scale group
Scopes: `bullfrog.api.events.all`, `bullfrog.api.all`


### Configurations - PUT /{scaleGroup}
Configure the specified scale group
Scopes: `bullfrog.api.all`


### Configurations - DELETE /{scaleGroup}
Remove the specified scale group
Scopes: `bullfrog.api.all`

### ScaleGroups - GET
Query the current state of the environment / scaled resources
Scopes: `bullfrog.api.events.all`, `bullfrog.api.all`

Sample request

GET

```
GET bullfrog.production.eshopworld.com/api/v1/ScaleGroups/SNKRS/
                                               
200
	{
		"Regions":[{
			"Name":"us",                                   // or whatever the azure region names are
			"WasScaledUpAt":"yyyy-mm-ddT00:00:00.0000Z",   // can be null if no scale
        	"WillScaleDownAt":"yyyy-mm-ddT00:00:00.0000Z", // can be null if no scale
			"Scale":"50000"                                // minimum requests/s to handle
		}]
    }
```


### ScaleEvents - GET
Get a response with all registered scale events.
Scopes: `bullfrog.api.events.read`, `bullfrog.api.events.all, bullfrog.api.all`

Sample request

GET
```
GET bullfrog.production.eshopworld.com/api/v1/ScaleEvents/SNKRS/
                                                           ⮤ Scale group identifier (eg. SNKRS, SNKRS-Payments)
200
	[{
		"Id":"bbae46c7-db73-56bb-bd92-ea0c2a8f1e7d"
		"Name":"some high heat event",
		"RequiredScaleAt":"yyyy-mm-ddT00:00:00.0000Z",    // Scale is 'out' by this time
		"EstimatedScaleUpAt":"yyyy-mm-ddT00:00:00.0000Z", // Scale starts at this time (calculated by bullfrog)
        "StartScaleDownAt":"yyyy-mm-ddT00:00:00.0000Z",
		"RegionConfig":[{
			"Name":"us",
			"Scale":"5000" 
		},{
			"Name":"eu",
			"Scale":"50000"
		}]
    },
    { ... }]
```

### ScaleEvents - GET /{id}
Response for a specific scale event

Scopes: `bullfrog.api.events.read`, `bullfrog.api.events.all`, `bullfrog.api.all`

Sample request

```
GET
GET bullfrog.production.eshopworld.com/api/v1/ScaleEvents/SNKRS/bbae46c7-db73-56bb-bd92-ea0c2a8f1e7d/
                                                                   ⮤ Scale event identifier  
200
	{
		"Id":"bbae46c7-db73-56bb-bd92-ea0c2a8f1e7d"
		"Name":"some high heat event",
		"RequiredScaleAt":"yyyy-mm-ddT00:00:00.0000Z",
		"EstimatedScaleUpAt":"yyyy-mm-ddT00:00:00.0000Z",
        "StartScaleDownAt":"yyyy-mm-ddT00:00:00.0000Z",
		"RegionConfig":[{
			"Name":"us",
			"Scale":"5000" 
		},{
			"Name":"eu",
			"Scale":"50000"
		}]
    }
```

### ScaleEvents - PUT /{id}
Create or update a new 'scaling entry', which will schedule a scale at a particular time. In the case of an update, it will replace the existing entry completely. If the scale out portion of the event has already taken place, then the confirmation will be via the EDA.

Scopes: `bullfrog.api.events.all`, `bullfrog.api.all`

The contract allows specification of region characteristics to allow the API to perform scaling optimisations at the region level. This is not a blocking requirement, only an optimisation.

If a scale is requested, which the system/region can't scale to (i.e. target scale 1M requests/s), the API should return a 400 error, rejecting the scale event

Sample request

PUT
```
PUT bullfrog.production.eshopworld.com/api/v1/ScaleEvents/SNKRS/bbae46c7-db73-56bb-bd92-ea0c2a8f1e7d/
                                                            ⮤ Scale group identifier    ⮤ Scale event identifier 
	{
		"Name":"A name for context/logging etc",
		"RequiredScaleAt":"yyyy-mm-ddT00:00:00.0000Z",
        "StartScaleDownAt":"yyyy-mm-ddT00:00:00.0000Z",
		"RegionConfig":[{
			"Name":"us",
			"Scale":"5000" 
		},{
			"Name":"eu",
			"Scale":"50000"
		}]
    }

201 // no existing event, and new event registered
  Location: /api/v1/ScaleEvents/SNKRS/bbae46c7-db73-56bb-bd92-ea0c2a8f1e7d/

202 // reconfigure when when scale event has started. get completion notification via EDA

204 // reconfigure when scale event has not started

400
  {
	"Errors":[{"Code":"-1","Message":"Cant register scale event in the past"},{"Code":"-2","Message":"Cant scale to '1000000' requests a second, max requests supported is '500000 r/s'"}]
  }
```

### ScaleEvents - DELETE /{id}
Remove a previously registered entry. If the event has already scaled out, a delete should reverse this (i.e. scale in), and then remove the entry.

Scopes: `bullfrog.api.events.all`, `bullfrog.api.all`

Sample request

DELETE
```
DELETE bullfrog.production.eshopworld.com/api/v1/ScaleEvents/SNKRS/bbae46c7-db73-56bb-bd92-ea0c2a8f1e7d/
                                                               ⮤ Scale group identifier         ⮤ Scale event identifier 
204
  // when scale event has not started

202
  // when scale event has started, and will perform scale in. wait for EDA event to confirm
 ```