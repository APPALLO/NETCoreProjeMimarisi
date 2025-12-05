# Postman Kullanım Rehberi

## Kurulum

### 1. Collection ve Environment Import Et

1. Postman'i aç
2. Sol üstte **Import** butonuna tık
3. Bu dosyaları sürükle:
   - `postman/Microservices-API.postman_collection.json`
   - `postman/Local-Development.postman_environment.json`

### 2. Environment Seç

Sağ üstten **Local Development** environment'ını seç.

---

## Kullanım Senaryoları

### Senaryo 1: Yeni Kullanıcı Kaydı ve Login

1. **Register User** isteğini çalıştır
   - Otomatik random email/isim oluşturur
   - Token otomatik environment'a kaydedilir

2. **Login User** isteğini çalıştır
   - Sabit email kullanır: `test@example.com`
   - Token güncellenir

3. Token artık tüm protected endpoint'lerde kullanılabilir

### Senaryo 2: Ürün Ekleme ve Listeleme

1. **Create Product** isteğini çalıştır
   - Random ürün adı oluşturur
   - Category: Electronics

2. **Get Products by Category** isteğini çalıştır
   - Eklediğin ürünü göreceksin
   - İlk istekte cache miss (yavaş)
   - İkinci istekte cache hit (hızlı)

3. **Search Products** isteğini çalıştır
   - Arama terimi: "laptop"

### Senaryo 3: Sipariş Oluşturma (Saga)

1. Önce **Login** yap (token gerekli)

2. **Create Order** isteğini çalıştır
   - Response'dan `order_id`'yi kopyala
   - Environment'a manuel ekle veya Tests script'i güncelle

3. **Get Saga Status** isteğini çalıştır
   - Saga adımlarını göreceksin
   - Status: Pending/Completed/Failed

---

## Request Detayları

### Register User

```http
POST http://localhost:5000/api/auth/register
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "Password123!",
  "firstName": "John",
  "lastName": "Doe"
}
```

**Response:**
```json
{
  "token": "eyJhbGc...",
  "expiresAt": "2024-12-06T10:30:00Z",
  "email": "user@example.com"
}
```

### Login User

```http
POST http://localhost:5000/api/auth/login
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "Password123!"
}
```

### Create Product

```http
POST http://localhost:5003/api/products
Content-Type: application/json

{
  "name": "Laptop",
  "description": "High-performance laptop",
  "price": 1299.99,
  "category": "Electronics",
  "stockQuantity": 50
}
```

### Create Order (Protected)

```http
POST http://localhost:5003/api/orders
Content-Type: application/json
Authorization: Bearer {{token}}

{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "items": [
    {
      "productId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "productName": "Laptop",
      "quantity": 1,
      "price": 1299.99
    }
  ]
}
```

---

## Otomatik Test Scripts

### Token'ı Otomatik Kaydet

Register ve Login request'lerinin **Tests** tab'ına ekle:

```javascript
if (pm.response.code === 200 || pm.response.code === 201) {
    var jsonData = pm.response.json();
    pm.environment.set("token", jsonData.token);
    pm.test("Token received", function () {
        pm.expect(jsonData.token).to.be.a('string');
    });
}
```

### Order ID'yi Otomatik Kaydet

Create Order request'inin **Tests** tab'ına ekle:

```javascript
if (pm.response.code === 201 || pm.response.code === 202) {
    var jsonData = pm.response.json();
    pm.environment.set("order_id", jsonData.id);
    pm.test("Order created", function () {
        pm.expect(jsonData.id).to.be.a('string');
    });
}
```

### Response Time Kontrolü

Herhangi bir request'in **Tests** tab'ına ekle:

```javascript
pm.test("Response time is less than 500ms", function () {
    pm.expect(pm.response.responseTime).to.be.below(500);
});

pm.test("Status code is 200", function () {
    pm.response.to.have.status(200);
});

pm.test("Has correlation ID", function () {
    pm.response.to.have.header("X-Correlation-ID");
});
```

---

## Collection Runner

Tüm request'leri sırayla çalıştırmak için:

1. Collection'a sağ tık → **Run collection**
2. Sırayı ayarla:
   - Register User
   - Login User
   - Create Product
   - Get Products by Category
   - Create Order
   - Get Saga Status
3. **Run** butonuna tık

---

## Environment Variables

| Variable | Açıklama | Örnek |
|----------|----------|-------|
| `base_url` | Identity Service URL | http://localhost:5000 |
| `gateway_url` | API Gateway URL | http://localhost:5003 |
| `token` | JWT token (otomatik) | eyJhbGc... |
| `order_id` | Son oluşturulan order ID | guid |

---

## Hata Durumları

### 400 Bad Request
```json
{
  "error": "User already exists"
}
```
**Çözüm:** Register'da random email kullan veya farklı email dene

### 401 Unauthorized
```json
{
  "error": "Invalid credentials"
}
```
**Çözüm:** Login bilgilerini kontrol et

### 429 Too Many Requests
```json
{
  "error": "Too many requests",
  "retryAfter": 45
}
```
**Çözüm:** Gateway rate limit'e takıldın, 45 saniye bekle

### 503 Service Unavailable
```json
{
  "error": "Service unavailable"
}
```
**Çözüm:** Downstream servis çalışmıyor, servisleri başlat

---

## Tips & Tricks

### 1. Random Data Kullan

Postman'in built-in değişkenlerini kullan:
- `{{$randomEmail}}` → random email
- `{{$randomFirstName}}` → random isim
- `{{$randomLastName}}` → random soyisim
- `{{$randomProductName}}` → random ürün adı
- `{{$guid}}` → random GUID

### 2. Pre-request Script

Login'den önce otomatik register yapmak için:

```javascript
pm.sendRequest({
    url: pm.environment.get("base_url") + "/api/auth/register",
    method: 'POST',
    header: {
        'Content-Type': 'application/json'
    },
    body: {
        mode: 'raw',
        raw: JSON.stringify({
            email: pm.variables.replaceIn("{{$randomEmail}}"),
            password: "Password123!",
            firstName: pm.variables.replaceIn("{{$randomFirstName}}"),
            lastName: pm.variables.replaceIn("{{$randomLastName}}")
        })
    }
}, function (err, res) {
    if (!err && res.code === 201) {
        console.log("User registered successfully");
    }
});
```

### 3. Correlation ID Takibi

Her request'te correlation ID'yi logla:

```javascript
var correlationId = pm.response.headers.get("X-Correlation-ID");
console.log("Correlation ID:", correlationId);
pm.environment.set("last_correlation_id", correlationId);
```

---

## Monitoring

### Newman ile CLI'dan Çalıştır

```bash
npm install -g newman

newman run postman/Microservices-API.postman_collection.json \
  -e postman/Local-Development.postman_environment.json \
  --reporters cli,json
```

### CI/CD Pipeline'da Kullan

```yaml
# GitHub Actions
- name: Run Postman Tests
  run: |
    newman run postman/Microservices-API.postman_collection.json \
      -e postman/Local-Development.postman_environment.json \
      --bail
```

---

## Sonuç

Postman collection'ı import et, environment'ı seç ve test etmeye başla! Tüm endpoint'ler hazır, otomatik token yönetimi var.
