# Face Recognition API

A RESTful API built with **ASP.NET Core 8** that uses **Amazon Rekognition** to authenticate users by facial recognition. Users are registered with a photo, which is indexed in a Rekognition collection. Authentication is then performed by comparing a new photo against the stored faces — no passwords required.

---

## Features

- Register users with a photo — face is automatically indexed via Amazon Rekognition
- Authenticate users by submitting a photo — matched against the face collection
- Stores user data and photo in SQL Server via Entity Framework Core
- Swagger UI available for easy endpoint exploration

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 8 (ASP.NET Core) |
| AI / Face Recognition | Amazon Rekognition (AWS SDK) |
| Database | SQL Server + Entity Framework Core 8 |
| API Docs | Swagger / OpenAPI |
| Unit Tests | xUnit + Moq + EF Core InMemory |

---

## Architecture Overview

```
Client (photo upload)
       │
       ▼
 ASP.NET Core API
  ├── POST /api/users/register ──► Amazon Rekognition (IndexFaces)
  │                                       │
  │                               Face ID returned
  │                                       │
  │                               SQL Server (save user + face ID)
  │
  └── POST /api/auth/login ────► Amazon Rekognition (SearchFacesByImage)
                                         │
                                 Match found → query SQL Server
                                         │
                                 Return user data
```

---

## API Endpoints

### Register User
**`POST /api/users/register`** — `multipart/form-data`

| Field | Type | Description |
|---|---|---|
| `name` | string | User's full name |
| `photo` | file | Face photo (JPG/PNG) |

**Response `200 OK`:**
```json
{ "id": 1, "name": "John Doe" }
```

---

### Authenticate via Face
**`POST /api/auth/login`** — `multipart/form-data`

| Field | Type | Description |
|---|---|---|
| `photo` | file | Face photo to authenticate |

**Response `200 OK`:**
```json
{
  "id": 1,
  "name": "John Doe",
  "photoBase64": "..."
}
```

**Response `401 Unauthorized`:** No matching face found in the collection.

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (Developer Edition or higher)
- [AWS Account](https://aws.amazon.com/free) with Rekognition access
- [AWS CLI](https://aws.amazon.com/cli/) configured

### 1. Configure AWS credentials

```bash
aws configure
```

Enter your `Access Key ID`, `Secret Access Key`, and default region (e.g. `us-east-1`).

### 2. Configure the connection string

In `appsettings.json` (or `appsettings.Development.json`):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=FaceRekognitionDb;Trusted_Connection=True;"
  }
}
```

### 3. Run migrations

```bash
dotnet tool install --global dotnet-ef
dotnet ef database update
```

### 4. Run the API

```bash
dotnet run
```

Swagger UI will be available at `https://localhost:{port}/swagger`.

---

## Running Tests

```bash
dotnet test
```

The test suite covers the main scenarios for both controllers using mocked AWS and an in-memory database:

- Login with missing or empty photo
- Login when no face match is found
- Login when face is matched but user is not in the database
- Login with a valid match — returns user data
- Login when Rekognition throws an exception
- Register with missing or empty photo
- Register when no face is detected in the photo
- Register with a valid photo — persists user and face ID
- Register when the Rekognition collection doesn't exist yet — creates it automatically

---

## Frontend Angular 

https://github.com/markimpdl/FrontFaceRekoginition

---

## AWS Free Tier

Amazon Rekognition offers **5,000 image analyses per month** free for the first 12 months — sufficient for development and demos.

---

## Author

**Marcos Ortolani**  
[LinkedIn](https://www.linkedin.com/in/marcosortolani) · [GitHub](https://github.com/markimpdl)
