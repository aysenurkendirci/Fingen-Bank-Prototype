# ADR-0015: Bildirim merkezi

Durum: Kabul edildi

## Bağlam

Müşteri hesabına para geldiğini, otomatik birikimin çalıştığını, bir hedefe ulaştığını, şifresinin değiştiğini ya da kartının kapatıldığını uygulamanın içinde görmeli. Bu olayların bir kısmı zaten outbox'a yazılıyor ve RabbitMQ'ya gidiyor.

## Karar

- Bildirimler `dbo.Notifications` tablosuna, anlattıkları değişiklikle aynı transaction içinde yazılır. Reddedilen bir transfer bildirim bırakmaz, kaydedilen bir transfer bildirimsiz kalmaz.
- Outbox'tan okuyan ayrı bir tüketici yazılmadı. Bildirim metni müşterinin dilinde ve hesap numarasının son dört hanesiyle oluşur; bunun için gereken bilgi zaten işlemin içinde var. Olaylar ise kişisel veri taşımaz, tüketicinin bu bilgileri yeniden okuması gerekirdi.
- Bütün bildirim metinleri tek bir sınıfta (`Notices`) durur.
- Müşterinin kendi hesapları arasındaki aktarım bildirim üretmez. Aynı kart düğmesine iki kez basmak ikinci bir bildirim üretmez.
- Okunan bildirimler 90 gün sonra temizlenir, okunmamışlar silinmez. Bildirimler KVKK veri indirmesine dahildir ve hesapla birlikte silinir.
- Menüdeki okunmamış sayısı her ekran değişiminde ve sekme açıkken dakikada bir sorulur.

## Sonuçlar

- Bildirim yazmak işlemin süresine bir satır ekler.
- Sayı anlık değildir, en geç bir dakika gecikir. Sunucudan itme (SignalR ya da server-sent events) bu gecikmeyi kaldırır, ama ayrı bir bağlantı ve ölçekleme işi getirir.
