# LogiTrack (WIP)

## Project Structure
```
LogiTrack/
├── Controllers/
│   ├── AuthController.cs
│   ├── InventoryController.cs
│   └── OrderController.cs
├── Data/
│   ├── ISeeder.cs
│   ├── LogiTrackContext.cs
│   └── RoleAndUserSeeder.cs
├── Dto/
│   ├── InventoryItemDto.cs
│   ├── LoginDto.cs
│   ├── LoginResponseDto.cs
│   ├── OrderDto.cs
│   └── RegisterDto.cs
├── Infrastructure/
│   └── DatabaseSeederExtensions.cs
├── Migrations/
├── Models/
│   ├── ApplicationUser.cs
│   ├── InventoryItem.cs
│   └── Order.cs
├── Properties/
│   └── launchSetting.json
├── Services/
│   └── Mappers
|       ├── IInventoryMapper.cs
|       ├── InventoryMapper.cs
|       ├── IOrderMapper.cs
|       └── OrderMapper.cs
├── appsettings.json
├── LogiTrack.csproj
├── LogiTrack.sln
├── Program.cs
└── README.md
```


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

## Next Steps
- [ ] add fields `description` and `isActive` to the `InventoyItem` model and dto
- [ ] add unit tests
- [ ] add integration tests
- [ ] Rewrite endpoints in kebab-case format