 # Local setup:

  - Create the local.settings.json file prvangap-upi\Upi

   ``` json

 {
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage":"AzureWebJobsStorageConnectionString",
    "AzureSignalRConnectionString": "AzureSignalRConnectionString",
  }
}

```

# Run:

- Visit http://localhost:7071/api/index and then http://localhost:7071/api/sender