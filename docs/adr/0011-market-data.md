# ADR-0011: Piyasa verisi dış kaynaklardan, kesintiye dayanıklı alınır

Durum: Kabul edildi

## Bağlam

Döviz hesaplarının lira karşılığını göstermek ve kur hesaplayıcı sunmak için güncel kurlar gerekiyor. Kurlar dışarıdan geliyor. Kaynak yavaşlayabilir, kapanabilir ya da beklenmedik bir yanıt dönebilir. Bu sırada müşterinin bakiyesi gösterilmeye devam etmeli.

İki kaynak var. TCMB'nin günlük gösterge kurları herkese açık ve anahtar istemiyor. VakıfBank Açık Bankacılık API'leri (Yatırım Servisleri: döviz kurları, döviz hesaplayıcısı, altın fiyatları) sandbox ortamında, OAuth2 client credentials ile çalışıyor.

## Karar

- Uygulama katmanı yalnızca `IExchangeRates` arayüzünü bilir. Kaynakların ne olduğu altyapının işidir.
- Kaynaklar (`IExchangeRateSource`) kayıt sırasıyla denenir. Bir banka API'si eklendiğinde TCMB'nin önüne konur. Anahtarı olmayan kurulumda TCMB tek başına cevap verir, böylece projeyi açan herkes gerçek kur görür.
- Kurlar 10 dakika önbellekte tutulur. Önbellek boşken aynı anda gelen istekler tek bir dış istek bekler.
- Bütün kaynaklar düşerse son başarılı kurlar "eski" işaretiyle sunulur, ekranda bu açıkça yazar. Hiç kur yoksa API 503 döner. Sayfa yalnızca kur bölümünü gizler, bakiyeler gösterilmeye devam eder.
- HTTP çağrıları `Microsoft.Extensions.Http.Resilience` ile sarılır: deneme başına 10 saniye (TCMB zaman zaman bu kadar yavaş), toplamda 25 saniye zaman aşımı, 1 yeniden deneme ve devre kesici. Kurlar sayfadan ayrı yüklendiği için bu bekleme kimseyi bekletmez.
- Dış yanıt güvenilmez veri sayılır. XML, DTD kapalı ve çözümleyici olmadan okunur (XXE ve entity patlamasına karşı). Yanıt boyutu sınırlıdır. Kur değerleri sayı, sıfırdan büyük ve geçerli para birimi kodu olmak zorundadır, olmayan satır atlanır.
- Lira karşılığı, müşterinin dövizi bozdurunca alacağı kurla (alış kuru) hesaplanır ve "yaklaşık" diye gösterilir. Kuru olmayan bir döviz varsa toplam hiç gösterilmez, eksik bir toplam yanıltır.

## VakıfBank

Sağlayıcı aynı arayüzle eklendi (`VakifBankExchangeRateSource`) ve kimlik bilgileri ayarlıysa TCMB'nin önüne konur. Ayarlı değilse hiç kaydedilmez, TCMB tek başına cevap verir.

- Servis: `POST /getCurrencyRates`, gövdede `ValidityDate`. Gün, İstanbul gece yarısının UTC anı olarak gönderilir: 1 Haziran için `2020-05-31T21:00:00.000Z`.
- Yetkilendirme OAuth2 client credentials ile yapılır. Token süresi dolmadan bir dakika öncesine kadar paylaşılan bir önbellekte tutulur, aynı anda tek bir istek yenisini alır. Ağ geçidi token'ı reddederse bir kez yenilenir.
- Banka kuru müşterinin gözünden verir: `SaleRate` müşterinin dövizi bankaya sattığı, `PurchaseRate` bankadan aldığı fiyattır. Uygulamada ilki bankanın alış, ikincisi satış kurudur. Dokümanda sayı, örnekte metin olarak geçtikleri için ikisi de kabul edilir.
- Yanıt `APIGW000000` dışında bir durum kodu taşırsa ya da kullanılabilir kur yoksa kaynak başarısız sayılır, sıradaki kaynak (TCMB) denenir.
- Bankaya TCMB'den daha kısa süre tanınır (deneme başına 5, toplamda 10 saniye): yavaş bir cevap kurları bekletmek yerine TCMB'ye düşer.
- Client id ve secret yalnızca user-secrets'ta ya da ortam değişkeninde durur (`MarketData:VakifBank:ClientId`, `MarketData:VakifBank:ClientSecret`), repoya ve CI'a girmez. Gizli bilgi yalnızca token isteğinin gövdesinde, HTTPS üzerinden gider ve günlüğe yazılmaz. Uygulama sandbox planında olduğu için yalnızca test verisi gelir.
- Token adresi portalın "Yetkilendirme" sayfasına göre `MarketData:VakifBank:TokenUrl` ile değiştirilebilir. Scope `oob public`: portal kurlar için `oob public`, hesaplayıcı için `public` listeliyor.

### Hesaplayıcı

- Çevirici `POST /api/market/convert` ile sunucuya sorar. Kimlik bilgileri ayarlıysa cevabı VakıfBank'ın Döviz Hesaplayıcısı (`POST /currencyCalculator`) verir; bankanın kendi kuru ve yuvarlaması kullanılır (portal örneğinde 100 TL, 14,6058 değil 14,6 USD).
- Banka hesaplayıcısı ayarlı değilse ya da cevap vermezse aynı hesap uygulamanın kurlarıyla yapılır: döviz alırken satış, bozdururken alış kuru kullanılır, iki döviz arasında lira ortadadır. Müşterinin alacağı tutar kuruşa aşağı yuvarlanır.
- Hesaplayıcı lira için `TL` kodunu kullanır, uygulama `TRY`; kodlar iki yönde çevrilir.
- Her istek bankaya gidebileceği için uç, para hareketleriyle aynı kullanıcı başı istek sınırını paylaşır. Çevirici yazım durunca sorar, yalnızca son sorunun cevabı gösterilir.

## Sonuçlar

- Testler internete çıkmaz. Entegrasyon testleri sahte bir kaynak kullanır, ayrıştırıcı ve yedekleme davranışı birim testleriyle sınanır.
- Gösterilen kur gösterge niteliğindedir, işlem kuru değildir. Ekranda bu yazar.
