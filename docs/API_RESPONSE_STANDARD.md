# API Response Standard Documentation

## Tujuan

Dokumen ini menjadi standar response untuk seluruh REST API agar:

- Konsisten di seluruh endpoint.
- Mudah digunakan oleh Frontend, Mobile, maupun pihak ketiga.
- Memudahkan debugging dan maintenance.
- Memiliki standar HTTP Status Code yang jelas.
- Mendukung pagination untuk endpoint list.

---

# General Response Format

## Success Response

```json
{
  "success": true,
  "message": "Success message",
  "data": {}
}
```

## Error Response

```json
{
  "success": false,
  "message": "Error message",
  "errors": {}
}
```

---

# Pagination Response

Digunakan hanya pada endpoint yang mengembalikan daftar data (List).

```json
{
  "success": true,
  "message": "Get data success",
  "data": [],
  "pagination": {
    "currentPage": 1,
    "pageSize": 10,
    "totalData": 100,
    "totalPage": 10,
    "hasPreviousPage": false,
    "hasNextPage": true
  }
}
```

## Pagination Request

```http
GET /api/users?page=1&pageSize=10
```

---

# HTTP Status Code Standard

| Status Code | Keterangan |
| --- | --- |
| 200 OK | Request berhasil |
| 201 Created | Data berhasil dibuat |
| 204 No Content | Delete berhasil tanpa response body |
| 400 Bad Request | Request tidak valid |
| 401 Unauthorized | Belum login / Token tidak valid |
| 403 Forbidden | Tidak memiliki hak akses |
| 404 Not Found | Data tidak ditemukan |
| 409 Conflict | Data sudah ada / terjadi konflik |
| 422 Unprocessable Entity | Validasi gagal |
| 429 Too Many Requests | Terlalu banyak request |
| 500 Internal Server Error | Kesalahan pada server |

---

# Standard Response

## GET List

HTTP Status: 200 OK

```json
{
  "success": true,
  "message": "Get users success",
  "data": [
    {
      "id": 1,
      "name": "Yuda"
    }
  ],
  "pagination": {
    "currentPage": 1,
    "pageSize": 10,
    "totalData": 100,
    "totalPage": 10,
    "hasPreviousPage": false,
    "hasNextPage": true
  }
}
```

---

## GET Detail

HTTP Status: 200 OK

```json
{
  "success": true,
  "message": "Get user success",
  "data": {
    "id": 1,
    "name": "Yuda"
  }
}
```

---

## POST

HTTP Status: 201 Created

```json
{
  "success": true,
  "message": "User created successfully",
  "data": {
    "id": 10
  }
}
```

---

## PUT / PATCH

HTTP Status: 200 OK

```json
{
  "success": true,
  "message": "User updated successfully",
  "data": {
    "id": 10
  }
}
```

---

## DELETE

HTTP Status: 200 OK

```json
{
  "success": true,
  "message": "User deleted successfully"
}
```

Atau:

HTTP Status: 204 No Content

Tanpa response body.

---

# Error Response

## 400 Bad Request

```json
{
  "success": false,
  "message": "Bad request"
}
```

---

## 401 Unauthorized

```json
{
  "success": false,
  "message": "Unauthorized"
}
```

---

## 403 Forbidden

```json
{
  "success": false,
  "message": "Access denied"
}
```

---

## 404 Not Found

```json
{
  "success": false,
  "message": "Data not found"
}
```

---

## 409 Conflict

```json
{
  "success": false,
  "message": "Data already exists"
}
```

---

## 422 Validation Error

```json
{
  "success": false,
  "message": "Validation failed",
  "errors": {
    "email": [
      "Email is required"
    ]
  }
}
```

---

## 429 Too Many Requests

```json
{
  "success": false,
  "message": "Too many requests"
}
```

---

## 500 Internal Server Error

```json
{
  "success": false,
  "message": "Internal server error"
}
```

---

# Prompt untuk AI

Gunakan standar berikut ketika membuat REST API.

## Rules

1. Selalu gunakan HTTP Status Code yang sesuai.
2. Semua response wajib memiliki field:
   - `success`
   - `message`
   - `data` untuk success
   - `errors` untuk validation/error jika diperlukan
3. Endpoint list wajib menggunakan object `pagination`.
4. Jangan membuat format response yang berbeda antar endpoint.
5. Gunakan `200 OK` untuk GET, PUT, PATCH, dan DELETE, kecuali menggunakan `204 No Content`.
6. Gunakan `201 Created` setelah POST berhasil.
7. Gunakan `422` untuk validasi.
8. Gunakan `404` jika data tidak ditemukan.
9. Gunakan `409` jika terjadi konflik data, misalnya duplicate key.
10. Gunakan `500` hanya untuk kesalahan server yang tidak terduga.
11. Response harus sederhana, konsisten, dan mudah diparsing oleh frontend.
12. Nama properti JSON menggunakan camelCase.
13. Semua endpoint list harus mendukung parameter `page` dan `pageSize`.
14. Struktur `pagination` harus selalu berisi:
    - `currentPage`
    - `pageSize`
    - `totalData`
    - `totalPage`
    - `hasPreviousPage`
    - `hasNextPage`
15. Jangan mengembalikan stack trace, query SQL, atau informasi sensitif pada response API.
16. Semua endpoint harus mengembalikan format response yang konsisten sesuai dokumen ini.
