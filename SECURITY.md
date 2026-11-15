# Güvenlik

Bu bir portföy projesi, gerçek müşteri verisi tutmuyor. Yine de bir banka uygulamasından beklenen önlemlerle yazıldı. Bir açık bulursan herkese açık bir issue açmak yerine depo sahibine GitHub üzerinden özel olarak yaz ("Report a vulnerability").

## Tehdit modeli

STRIDE başlıklarına göre, neye karşı ne yapıldığı:

| Tehdit | Örnek | Önlem |
|---|---|---|
| Kimlik taklidi | Çalınan oturum, şifre deneme | Erişim token'ı 10 dakika ve yalnızca bellekte. Yenileme token'ı HttpOnly, Secure, SameSite=Strict cookie'de, her kullanımda yenilenir, tekrar kullanılırsa bütün oturumlar kapanır. Giriş uçlarında IP başına istek sınırı. JWT'de algoritma HS256'ya sabit, anahtar en az 32 bayt. |
| Kurcalama | Başkasının hesabından para çekmek, tutarı değiştirmek | Her hesap, hedef ve kart sorgusu kullanıcı kimliğiyle süzülür, başkasının kaydı 404 döner. Bakiye yalnızca defter kaydıyla değişir, veritabanı negatif bakiyeyi ve tek taraflı kaydı reddeder. |
| İnkâr | "Bu transferi ben yapmadım" | Her para hareketi çift taraflı deftere ve outbox olayına yazılır, finans tabloları temporal tablolarla geçmişini saklar, her istek bir korelasyon kimliğiyle günlüğe düşer. |
| Bilgi sızması | Kişisel verinin günlüğe, hata mesajına ya da başka siteye sızması | Günlüklerde T.C. kimlik numarası maskeli. Beklenmeyen hatalar ayrıntı vermeden 500 döner. Finansal yanıtlar önbelleğe alınmaz. Olaylarda kişisel veri yok. Bildirimlerde hesap numarasının yalnızca son dört hanesi geçer. KVKK için veri indirme ve silme var, bildirimler de buna dahil. |
| Hizmet engelleme | Aynı anda yüzlerce transfer, dev istek gövdesi | Para hareketlerinde kullanıcı başına dakikada 30 istek sınırı. İstek gövdesi 1 MB ile sınırlı, dosya yükleme kendi sınırıyla. Dış kur kaynağına zaman aşımı ve devre kesici. |
| Yetki yükseltme | Başkasının hedefine para koymak, kumbaradan dışarı para göndermek | Uç noktalar varsayılan olarak giriş ister. Kumbara hesabı yalnızca sahibinin hesaplarına para aktarabilir. Silinmiş kullanıcının token'ı geçerlilik süresi dolmadan da reddedilir. |

## Tarayıcı

- CSP yalnızca kendi betiklerine izin verir. Satır içi betik ve `eval` yasaktır, sayfa başka bir sitenin çerçevesine konamaz.
- Bu yüzden tema seçimini erkenden uygulayan betik ayrı bir dosyada durur ve kritik CSS satır içine alınmaz.
- Cookie kullanan uçlar `X-Requested-By` başlığını ister (CSRF). CORS yalnızca geliştirme ortamında `localhost:4200` için açıktır.
- Angular, şablonlardaki bütün değerleri kaçışlar. Kodda `innerHTML` ya da güvenliği atlayan bir çağrı yok.

## Veri

- Bütün SQL sorguları parametreli (Dapper), dinamik SQL yalnızca silme prosedüründe ve sabit metinle.
- Şifreler ASP.NET Identity'nin hash'iyle saklanır, en az 12 karakter, bir harf ve bir rakam istenir.
- Dışarıdan gelen XML (TCMB kurları) DTD kapalı ve çözümleyicisiz okunur, boyutu sınırlıdır.
- Hedef fotoğrafları uygulamada yeniden çizilerek küçültülür, böylece konum gibi EXIF bilgileri sunucuya hiç gitmez. Sunucu türü dosya adına değil ilk baytlarına bakarak belirler (yalnızca JPEG, PNG, WebP), en fazla 2 MB kabul eder ve fotoğrafı `nosniff` ve `default-src 'none'` ile döner.

## OWASP ASVS

ASVS 4.0'ın bu projeye dokunan maddeleri koda karşı denetlendi. Bulunan iki açık kapatıldı.

| Madde | Durum |
|---|---|
| 2.1.1 Şifre en az 12 karakter | Kapatıldı: 8 karakterdi, artık 12 ve bir harf ile bir rakam isteniyor. |
| 2.1.7 Sızmış şifre listesi kontrolü | Yok, bilinen sınırlarda. |
| 2.2.1 Otomatik denemelere karşı koruma | Giriş uçlarında IP başına, para hareketlerinde kullanıcı başına istek sınırı. |
| 2.5.x / 3.x Oturum | Kısa ömürlü erişim token'ı, her kullanımda yenilenen HttpOnly cookie, tekrar kullanımda bütün oturumlar kapanır, çıkış sunucuda iptal eder, şifre değişince diğer oturumlar kapanır. |
| 4.1.x Erişim kontrolü | Her kayıt kullanıcı kimliğiyle süzülür; başkasının kaydı 404 döner, testlerle kilitli. |
| 5.x Girdi doğrulama ve XML | Bütün istekler sözleşme ve doğrulayıcılarla denetlenir, SQL parametreli, dış XML DTD kapalı okunur. |
| 7.1.x Günlükler | Kişisel veri maskeli, başarısız girişler kayıtlı, her istek bir korelasyon kimliği taşır. |
| 8.1.x Veri koruma | Finansal yanıtlar önbelleğe alınmaz, tarayıcıda token tutulmaz. |
| 13.1.x API | CORS yalnızca geliştirmede açık, istek gövdesi sınırlı. |
| 14.3.3 Sunucu bilgisi | Kapatıldı: API `Server: Kestrel` gönderiyordu, artık göndermiyor; nginx sürümünü gizler. |
| 14.4.x Güvenlik başlıkları | CSP, X-Frame-Options, nosniff, Referrer-Policy, Permissions-Policy, HSTS. |

## Otomatik kontroller

Her push'ta CI şunları yapar:
- Gizli anahtar taraması (gitleaks).
- Bilinen açığı olan NuGet paketlerinin kontrolü.
- Katman kuralları.
- Bütün testler. Yetki ve güvenlik testleri de buna dahil: başkasının kaydına erişim, istek sınırları, CSRF başlığı, yabancı origin ve zayıf şifre.
- Gerçek tarayıcıda uçtan uca testler.
- Bütün sistemin `docker compose` ile kurulup nginx üzerinden denenmesi.

CodeQL, C# ve TypeScript kodunu güvenlik açıklarına karşı tarar. Dependabot da bağımlılık güncellemelerini her hafta önerir.

## Bilinen sınırlar

- Uygulamanın kendisi HTTPS sunmaz, TLS önündeki ters vekilde biter. HSTS başlığı bu durumda anlam kazanır.
- Stil için `'unsafe-inline'` açık, çünkü Angular ve ECharts satır içi stil kullanır. Stil kod çalıştıramaz, ama ileride nonce'a geçilebilir.
- Şifreler sızıntı listelerine karşı denetlenmiyor.
