# ADR-0012: Hedefler kendi hesabında birikir

Durum: Kabul edildi

## Bağlam

Müşteri araba ya da ev gibi hedefler için para biriktirmek istiyor. Bazı hedefler bir yılbaşı kararı olarak belirli bir yıla ait. Biriktirme elle yapılabilmeli, maaş gibi beklenen bir para geldiğinde de kendiliğinden işlemeli.

Kolay yol, hedefin üstünde bir "biriken tutar" sayısı tutmaktı. Ama o zaman para hem vadesiz hesapta hem hedefte sayılır ya da hiçbir ekstrede görünmeden kaybolur.

## Karar

- Her hedef açılırken ona ait bir kumbara hesabı (`AccountType = 'Savings'`) açılır. Biriken tutar, o hesabın bakiyesidir.
- Hedefe para koymak da, çıkarmak da müşterinin kendi iki hesabı arasında bir transferdir. Aynı kilitleme, çift taraflı kayıt, Idempotency-Key ve outbox olayı kullanılır. Her hareket ekstrede görünür, toplam varlık doğru kalır.
- Kumbara hesabından başkasına doğrudan para gönderilemez. Para önce müşterinin kendi hesabına döner.
- Otomatik birikim kuralı bir hesabı izler. O hesaba en az belirlenen tutarda para girerse, sabit bir tutar ya da bir yüzde hedefe aktarılır. Bu, gelen transferle aynı transaction içinde olur, para harcanabilir görünmeden ayrılır. Hedefe ulaşılınca fazlası alınmaz.
- `savings.AutoSaveRuns`, hangi gelen transferin hangi hedefi beslediğini tutar. Aynı para bir kurala iki kez işlemez.
- Kural yerine getirilemezse (hesap boşalmış, hedef dolmuş) atlanır. Gelen ödeme hiçbir zaman bu yüzden reddedilmez.
- Yılbaşı hedefleri ayrı bir tür değildir, `NewYearOf` alanı dolu olan hedeflerdir. Ekranda ayrı bir alanda gösterilir.
- Hedef, kumbarası boşken kapatılabilir. Hesap silindiğinde hedefler ve kural geçmişi de silinir, transferler defterde kalır.

## Sonuçlar

- Hedeflerin parası bankanın geri kalanıyla aynı kurallara tabidir, ayrıca mutabakat gerekmez.
- Otomatik birikim, gelen transferin süresine birkaç sorgu ekler. Kural sayısı müşteri başına az olduğu için bu kabul edilebilir.
