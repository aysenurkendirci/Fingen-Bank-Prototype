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

Sağlayıcı aynı arayüzle eklenecek. API Key ve Secret yalnızca user-secrets'ta ya da ortam değişkeninde durur, repoya ve CI'a girmez. CI her push'ta gizli anahtar taraması yapar. Token süresi dolana kadar önbellekte tutulur. Uygulama sandbox planında olduğu için yalnızca test verisi gelir.

## Sonuçlar

- Testler internete çıkmaz. Entegrasyon testleri sahte bir kaynak kullanır, ayrıştırıcı ve yedekleme davranışı birim testleriyle sınanır.
- Gösterilen kur gösterge niteliğindedir, işlem kuru değildir. Ekranda bu yazar.
