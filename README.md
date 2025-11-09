# LogiTrack

## Introduction

LogiTrack is an educational, work-in-progress API that demonstrates building a small inventory and order service with ASP.NET Core, Entity Framework Core, and SQLite. 
It’s designed as a learning project to show common backend patterns: controllers, DTOs, EF migrations, authentication (JWT + Identity), simple seeding, and how to verify persistence across restarts. 

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

## Data Persistence Check

The script `verify_persistence.sh` allows a quick interactive check that the API actually persists data across a restart.
It can be found in the following location: 
```
Scripts/verify_persistence.sh
```
### How to run

1. Make executable once: chmod +x Scripts/verify_persistence.sh
2. Run from repo root: ./Scripts/verify_persistence.sh
3. Enter or accept defaults for port and manager email, paste the manager password (hidden).
4. When prompted, stop and restart your app, then press Enter to continue verification.
5. At the end choose whether to cleanup the test resources.

### What the script verifies

- Login works and a JWT is returned.
- Inventory item and order can be created.
- After a manual app restart, the created resources are found by their unique names (to avoid dependency on DB-generated numeric IDs).

### Troubleshooting

- If login hangs or fails: ensure the app is running and reachable at the chosen URL.
- If JSON parsing fails: install jq (choco/scoop/brew/apt) or run the script where python3 is available.
- Run against a test/dev DB only — do not run against production.

## Next Steps
- [ ] validate all inputs server-side
- [ ] add unit tests
- [ ] add integration tests
- [ ] create endpoints for health checks and readiness/liveness probes
- [ ] add pagination, filtering, and sorting to GET endpoints
- [ ] apply rate limiting & throttling
- [ ] Replace SQLite with postgresql or MySql
- [ ] integrate distributed tracing
- [ ] Use test database for `verify_persistence` script
- [ ] add fields `description` and `isActive` to the `InventoyItem` model and dto
- [ ] Rewrite endpoints in kebab-case format