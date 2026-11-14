# ADR-0013: Bankacılık işlem kuralları

Durum: Kabul edildi

## Bağlam

Temel kurallar zaten vardı:
- Hesap işleme kapalıyken para hareket etmez.
- Bakiye yetmezse transfer reddedilir.
- Farklı para birimleri arasında transfer yapılmaz.
- Alıcı adı IBAN sahibiyle eşleşmek zorundadır.
- Her para hareketi tekrar korumalı ve çift taraflı kayıtlıdır.

Eksik olan, bir bankanın internet şubesinde müşterinin her gün karşılaştığı işletme kurallarıydı. Transfer limiti yoktu, hesap kapatılamıyordu, hesap sayısı sınırsızdı, telefon alanına her şey yazılabiliyordu.

## Karar

- Başka bir müşteriye giden para iki sınıra tabidir: tek seferde gönderilebilecek tutar ve bir iş gününde gönderilebilecek toplam. Sınırlar para birimine göre tanımlıdır, varsayılanı TRY için 250.000 ve 750.000'dir. Müşterinin kendi hesapları ve hedefleri arasındaki aktarımlar sınıra girmez.
- Günün toplamı, transferlerin zamanını yazan veritabanı saatiyle ve İstanbul takvimine göre hesaplanır. Uygulama saati ile veritabanı saati ayrı tutulsaydı, ikisi farklılaştığında günlük toplam yanlış çıkardı.
- Günlük toplam, gönderen hesap kilitliyken okunur. Aynı hesaptan aynı anda giden iki transfer limiti birlikte aşamaz.
- Hesap yalnızca bakiyesi sıfırsa kapatılır. Hedeflere ait kumbara hesapları hedefle birlikte kapanır. Son vadesiz TL hesabı kapatılamaz, çünkü gelen paranın yatacağı bir hesap gerekir. Kapanan hesabı izleyen otomatik birikim kuralı da kaldırılır.
- Bir para biriminde en fazla 5 açık hesap olabilir. Kumbara hesapları bu sayıya dahil değildir.
- Kartın günlük limiti aylık limitinden büyük olamaz. İstek, veritabanı kısıtına ulaşmadan anlaşılır bir mesajla reddedilir.
- Telefon numarası Türkiye cep telefonu biçiminde olmalıdır, çünkü banka doğrulama kodlarını bu kanaldan gönderir.
- Bütün sınırlar `appsettings.json` içindeki `Banking` bölümünden okunur. Her kurulum kendi değerlerini verebilir.

## Sonuçlar

- Her kural birim ya da entegrasyon testiyle kilitlidir: limitin aşılması, günlük toplam, kendi hesaplar arası istisna, hesap kapatma koşulları, hesap sayısı, telefon biçimi ve kart limitleri.
- Yaş ve kimlik doğrulama (KYC) gibi kurallar için doğum tarihi ya da belge gerekir. Bunlar modelde yok, kapsam dışında bırakıldı.
- Mevzuattaki gerçek limitler bankaya ve kanala göre değişir. Buradaki değerler örnektir ve ayardan değiştirilebilir.
