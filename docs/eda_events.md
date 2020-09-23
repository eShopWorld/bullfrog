# Bullfrog EDA events

Bullfrog will publish messages through EDA (Service Bus) to notify about a status change.

### ScaleChange
Published to the bullfrog.domainevents.scalechange topic when a scale event occurs.

Payload:

```
{
  "id": "bbae46c7-db73-56bb-bd92-ea0c2a8f1e7d",
  "type": "ScaleOutStarted|ScaleOutComplete|ScaleInStarted|ScaleInComplete|ScaleIssue",
}
```