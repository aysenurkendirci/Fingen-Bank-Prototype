# Güvenlik

Bu bir portföy projesi, gerçek müşteri verisi tutmuyor. Yine de bir banka uygulamasından beklenen önlemlerle yazıldı. Bir açık bulursan herkese açık bir issue açmak yerine depo sahibine GitHub üzerinden özel olarak yaz ("Report a vulnerability").

## Tehdit modeli

STRIDE başlıklarına göre, neye karşı ne yapıldığı:

| Tehdit | Örnek | Önlem |
|---|---|---|
| Kimlik taklidi | Çalınan oturum, şifre deneme | Erişim token'ı 10 dakika ve yalnızca bellekte. Yenileme token'ı HttpOnly, Secure, SameSite=Strict cookie'de, her kullanımda yenilenir, tekrar kullanılırsa bütün oturumlar kapanır. Giriş uçlarında IP başına istek sınırı. Art arda 5 yanlış şifre ya da kod hesabı 15 dakika kilitler; oturum içindeki şifre sorularında da sayılır, kilitlenince bütün oturumlar kapanır. İsteğe bağlı ikinci doğrulama: doğrulayıcı uygulama kodu, her kod bir kez. Her giriş müşteriye bildirilir. JWT'de algoritma HS256'ya sabit, anahtar en az 32 bayt ve kesintisiz döndürülebilir. |
| Kurcalama | Başkasının hesabından para çekmek, tutarı değiştirmek | Her hesap, hedef ve kart sorgusu kullanıcı kimliğiyle süzülür, başkasının kaydı 404 döner. Bakiye yalnızca defter kaydıyla değişir, veritabanı negatif bakiyeyi ve tek taraflı kaydı reddeder. |
| İnkâr | "Bu transferi ben yapmadım" | Her para hareketi çift taraflı deftere ve outbox olayına yazılır, finans tabloları temporal tablolarla geçmişini saklar, her istek bir korelasyon kimliğiyle günlüğe düşer. |
| Bilgi sızması | Kişisel verinin günlüğe, hata mesajına ya da başka siteye sızması | Günlüklerde T.C. kimlik numarası maskeli. Beklenmeyen hatalar ayrıntı vermeden 500 döner. Finansal yanıtlar önbelleğe alınmaz. Olaylarda kişisel veri yok. Bildirimlerde hesap numarasının yalnızca son dört hanesi geçer. KVKK için veri indirme ve silme var, bildirimler de buna dahil. |
| Hizmet engelleme | Aynı anda yüzlerce transfer, dev istek gövdesi | Para hareketlerinde kullanıcı başına dakikada 30 istek sınırı. İstek gövdesi 1 MB ile sınırlı, dosya yükleme kendi sınırıyla. Dış kur kaynağına zaman aşımı ve devre kesici. |
| Yetki yükseltme | Başkasının hedefine para koymak, kumbaradan dışarı para göndermek | Uç noktalar varsayılan olarak giriş ister. Kumbara hesabı yalnızca sahibinin hesaplarına para aktarabilir. Silinmiş kullanıcının token'ı geçerlilik süresi dolmadan da reddedilir. |

## Tarayıcı

- CSP yalnızca kendi betiklerine izin verir. Satır içi betik ve `eval` yasaktır, sayfa başka bir sitenin çerçevesine konamaz.
- Bu yüzden tema seçimini erkenden uygulayan betik ayrı bir dosyada durur ve kritik CSS satır içine alınmaz.
- `<style>` etiketleri yalnızca her istekte yeniden üretilen bir nonce ile kabul edilir. nginx aynı değeri hem sayfaya hem CSP başlığına yazar, Angular kendi stillerine ekler; dışarıdan sokulan bir `<style>` reddedilir. Bu yüzden sayfa kabuğu hiç önbelleğe alınmaz.
- Cookie kullanan uçlar `X-Requested-By` başlığını ister (CSRF). CORS yalnızca geliştirme ortamında `localhost:4200` için açıktır.
- Angular, şablonlardaki bütün değerleri kaçışlar. Kodda `innerHTML` ya da güvenliği atlayan bir çağrı yok.

## Veri

- Bütün SQL sorguları parametreli (Dapper), dinamik SQL yalnızca silme prosedüründe ve sabit metinle.
- Şifreler ASP.NET Identity'nin hash'iyle saklanır, en az 12 karakter, bir harf ve bir rakam istenir.
- Yeni bir şifre, bilinen veri sızıntılarında geçen şifrelerle karşılaştırılır (Pwned Passwords). Şifrenin SHA-1 özetinin yalnızca ilk beş karakteri dışarı gider ve yanıt dolgulu istenir; şifre, özetin tamamı ve müşteriye dair hiçbir bilgi sunucudan çıkmaz. Liste yanıt vermezse kayıt engellenmez, şifre kuralları yine geçerlidir.
- Dışarıdan gelen XML (TCMB kurları) DTD kapalı ve çözümleyicisiz okunur, boyutu sınırlıdır.
- Hedef fotoğrafları uygulamada yeniden çizilerek küçültülür, böylece konum gibi EXIF bilgileri sunucuya hiç gitmez. Sunucu türü dosya adına değil ilk baytlarına bakarak belirler (yalnızca JPEG, PNG, WebP), en fazla 2 MB kabul eder ve fotoğrafı `nosniff` ve `default-src 'none'` ile döner.

## OWASP ASVS

ASVS 4.0'ın bu projeye dokunan maddeleri koda karşı denetlendi. Bulunan üç açık kapatıldı.

| Madde | Durum |
|---|---|
| 2.1.1 Şifre en az 12 karakter | Kapatıldı: 8 karakterdi, artık 12 ve bir harf ile bir rakam isteniyor. |
| 2.1.7 Sızmış şifre listesi kontrolü | Kapatıldı: kayıtta ve şifre değişiminde şifre Pwned Passwords listesine karşı denetlenir. |
| 2.2.1 Otomatik denemelere karşı koruma | Kapatıldı: IP başına istek sınırına ek olarak hesap başına kilit. 5 yanlış şifre ya da kod 15 dakika kilitler, kilit sırasında doğru şifre de kabul edilmez. |
| 2.2.3 Güvenlik olaylarının bildirilmesi | Her giriş, kilitlenme, şifre değişikliği ve ikinci doğrulamanın açılıp kapanması müşteriye bildirim olarak gider. |
| 2.8.x Tek kullanımlık kodlar | RFC 6238 TOTP, RFC'nin test vektörleriyle doğrulanır. Anahtar AES-256-GCM ile şifreli saklanır, bir kod iki kez kullanılamaz, kapatmak için şifre ve geçerli bir kod gerekir. |
| 3.x Kullanıcı tespiti | Kayıtlı olmayan bir T.C. numarası da bir şifre özeti kadar zaman alır; yanıt süresinden kimin müşteri olduğu anlaşılamaz. Şifre doğru olmadan ikinci doğrulamanın açık olup olmadığı söylenmez. |
| 2.5.x / 3.x Oturum | Kısa ömürlü erişim token'ı, her kullanımda yenilenen HttpOnly cookie, tekrar kullanımda bütün oturumlar kapanır, çıkış sunucuda iptal eder, şifre değişince diğer oturumlar kapanır. |
| 4.1.x Erişim kontrolü | Her kayıt kullanıcı kimliğiyle süzülür; başkasının kaydı 404 döner, testlerle kilitli. |
| 5.x Girdi doğrulama ve XML | Bütün istekler sözleşme ve doğrulayıcılarla denetlenir, SQL parametreli, dış XML DTD kapalı okunur. |
| 7.1.x Günlükler | Kişisel veri maskeli, başarısız girişler kayıtlı, her istek bir korelasyon kimliği taşır. İsteklerin içeriği, şifreler, kodlar ve şifreden türetilen hiçbir değer günlüğe yazılmaz; beklenen 4xx yanıtlar hata olarak kaydedilmez. |
| 8.1.x Veri koruma | Finansal yanıtlar önbelleğe alınmaz, tarayıcıda token tutulmaz. |
| 13.1.x API | CORS yalnızca geliştirmede açık, istek gövdesi sınırlı. |
| 14.3.3 Sunucu bilgisi | Kapatıldı: API `Server: Kestrel` gönderiyordu, artık göndermiyor; nginx sürümünü gizler. |
| 14.4.x Güvenlik başlıkları | CSP (stiller için istek başına nonce), X-Frame-Options, nosniff, Referrer-Policy, Permissions-Policy, HSTS. |

## Otomatik kontroller

Her push'ta CI şunları yapar:
- Gizli anahtar taraması (gitleaks).
- Bilinen açığı olan NuGet paketlerinin ve tarayıcıya giden npm paketlerinin kontrolü.
- Katman kuralları.
- Bütün testler. Yetki ve güvenlik testleri de buna dahil: başkasının kaydına erişim, istek sınırları, CSRF başlığı, yabancı origin, zayıf ve sızmış şifre, hesap kilidi, çalınan oturumla şifre tahmini, iki adımlı doğrulama ve kodun tekrar kullanılması, JWT anahtar döndürme, şifreli anahtarın değiştirilmesi.
- Gerçek tarayıcıda uçtan uca testler.
- Bütün sistemin `docker compose` ile kurulup nginx üzerinden denenmesi; sayfadaki nonce'un CSP başlığındakiyle aynı olduğu ve her istekte değiştiği de burada denetlenir.

CodeQL, C# ve TypeScript kodunu güvenlik açıklarına karşı tarar. Dependabot da bağımlılık güncellemelerini her hafta önerir.

## Bilinen sınırlar

- Derleme ve test araçlarının kullandığı glob, minimatch ve picomatch için yayımlanmış uyarılar (ReDoS) var. Bunlar tarayıcıya gitmez, yalnızca projenin kendi dosya kalıplarını işler. picomatch'in düzeltilmiş bir sürümü henüz yok; glob ve minimatch düzeltmeleri, Angular'ın derleme araçlarının istemediği yeni ana sürümlerde. Angular araçları güncellendikçe kendiliğinden kalkacaklar.
- Uygulamanın kendisi HTTPS sunmaz, TLS önündeki ters vekilde biter. HSTS başlığı bu durumda anlam kazanır.
- İkinci doğrulama isteğe bağlıdır ve yalnızca doğrulayıcı uygulama kodu olarak vardır. BDDK internet bankacılığında her girişte ikinci doğrulama bekler; zorunlu kılmak bir ayar, SMS kodu ise bir SMS sağlayıcısı gerektirir.
- Hesap kilidi, bir T.C. numarasının kayıtlı olduğunu beş denemeden sonra belli eder. Kilit olmasaydı şifre birçok adresten sınırsızca denenebilirdi; bankaların da kabul ettiği bir takas.
- İkinci doğrulama anahtarlarını şifreleyen anahtar (`Security:EncryptionKey`) henüz döndürülemez; kaybolursa müşterilerin doğrulayıcıyı yeniden kurması gerekir.
- İstek sınırlarının sayaçları her API sürecinin kendi belleğinde tutulur. Birden fazla süreç çalışırsa sınır süreç sayısı kadar artar; ortak bir depo (Redis) gerekir.
- JWT tek bir paylaşılan HMAC anahtarıyla imzalanır. Anahtar `Jwt:PreviousKeys` ile kimsenin oturumu düşmeden değiştirilebilir, ama token'ları başka servisler de doğrulayacaksa asimetrik anahtara geçilmelidir.
- Stil öznitelikleri (`style="..."`) `style-src-attr` ile açık, çünkü ECharts araç ipuçlarını onlarla yazar. Bir öznitelik bir şey yükleyemez ya da çalıştıramaz. `style-src-attr` desteklemeyen eski tarayıcılarda bu ipuçları biçimsiz görünür.
