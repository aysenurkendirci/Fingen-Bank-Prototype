# ADR-0014: Harcama bölme

Durum: Kabul edildi

## Bağlam

Bir müşteri bir harcamayı (yemek, kira, tatil) tek başına ödüyor ve arkadaşlarından paylarını istiyor. Arkadaşların bir kısmı bu bankanın müşterisi, bir kısmı değil. Paranın geri gelişi izlenebilmeli, ama bu iş yeni bir para hareketi türü açmadan yapılmalı.

## Karar

- Harcamayı yapan kişi toplamı ve her arkadaşın payını yazar. Kendi payı ayrıca tutulmaz, toplamdan geriye kalandır. Paylar toplamı aşamaz.
- Bankanın müşterisi olan arkadaş IBAN ile eklenir ve yazılan ad, transferdeki gibi hesap sahibiyle eşleşmek zorundadır. Böylece bir IBAN'ın kime ait olduğu bu ekrandan öğrenilemez. Müşteri olmayan arkadaş yalnızca bir addır.
- Pay, sıradan bir transferle ödenir: aynı limitler, aynı tekrar koruması, aynı defter kaydı. Ödeme yapıldıktan sonra pay o transferle kapatılır. Sunucu transferin bu kişiye ait olduğunu, ödeme yapan kişinin hesabına gittiğini ve tutarın paya tam eşit olduğunu denetler. Bir transfer en fazla bir payı kapatır (`UX_Shares_Transfer`).
- Müşteri olmayan arkadaşın payını, parayı aldığında harcamayı yapan kişi "ödendi" olarak işaretler.
- İstenen kişi payı reddedebilir. Harcamayı yapan kişi açık istekleri geri çekebilir, ödenmiş paylar olduğu gibi kalır. Her adım karşı tarafa bildirim olarak gider.
- Hesap silinince kişinin açtığı harcamalar silinir. Başkalarının ondan istediği paylar o kişilerin kaydıdır, kalır ama silinen kullanıcıyla bağı kopar.

## Sonuçlar

- Para yalnızca transfer yoluyla hareket eder. Harcama bölme, defterde ayrı bir mutabakat gerektirmez.
- Ödeme ve kapatma iki ayrı istektir. Transfer gidip kapatma gitmezse, uygulama tekrar denendiğinde parayı yeniden göndermez, var olan transferle kapatır.
- Paylaşım şimdilik yalnızca TL ile yapılır.
