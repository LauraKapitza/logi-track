# LogiTrack (WIP)

## Testing endpoints

The project uses **Swagger** for testing the API's endpoints.

Swagger UI is accessible via the following link:
```
http://localhost:5023/swagger/index.html
```

Use the following payload example when testing the `POST /api/inventory` endpoint:
```json
{
  "name": "Hand Truck",
  "quantity": 8,
  "location": "Warehouse C"
}

```

When testing the `POST /api/orders`, feel free tp use the following sample:
```json
{
  "customerName": "Samir",
  "datePlaced": "2025-11-08T00:00:00",
  "items": [
    {
      "name": "Pallet Jack",
      "quantity": 12,
      "location": "Warehouse A"
    },
    {
      "name": "Forklift",
      "quantity": 5,
      "location": "Warehouse B"
    }
  ]
}
```