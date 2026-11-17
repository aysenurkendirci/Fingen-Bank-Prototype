# ADR-0016: Giriş güvenliği: hesap kilidi ve ikinci doğrulama

Durum: Kabul edildi

## Bağlam

Giriş yalnızca şifreye dayanıyordu ve tahmin yalnızca istemci adresi başına yavaşlatılıyordu. Birçok adresten tek bir T.C. numarasına şifre denenebilirdi. Oturumu çalınan biri de "şifre değiştir" ya da "hesabı sil" ekranlarında mevcut şifreyi deneyebilirdi. BDDK internet bankacılığında girişte ikinci bir doğrulama bekliyor.

## Karar

- Yanlış şifreler ve kodlar hesap başına sayılır (`dbo.UserSecurity`). Art arda 5 yanlışta hesap 15 dakika kilitlenir ve kendiliğinden açılır. Kilit sürerken doğru şifre de kabul edilmez. Sayaç ve kilit tek bir SQL ifadesiyle değişir, aynı anda gelen iki deneme sınırın altından kaçamaz.
- Aynı sayaç oturum içindeki şifre sorularında da işler: şifre değiştirme, ikinci doğrulamayı kapatma, hesabı silme. Hesap kilitlenince bütün oturumlar kapanır ve müşteriye bildirim gider.
- Her başarılı giriş müşteriye bildirilir.
- İkinci doğrulama doğrulayıcı uygulama kodudur (RFC 6238, 30 saniye, 6 hane). Bir adım öncesi ve sonrası kabul edilir. Kullanılan son adım saklanır, bir kod iki kez giriş yaptıramaz.
- Anahtar müşteriye bir kez gösterilir ve veritabanında AES-256-GCM ile şifreli durur. Şifreleme anahtarı yapılandırmadan gelir (`Security:EncryptionKey`), veriyle aynı yerde durmaz.
- Anahtar, müşteri uygulamadan bir kodla doğrulamadan devreye girmez. Kapatmak için şifre ve geçerli bir kod gerekir.
- Giriş iki adımlıdır: şifre doğruysa ve ikinci doğrulama açıksa API kodu ister. Şifre yanlışken ikinci doğrulamanın açık olup olmadığı söylenmez.
- Kayıtlı olmayan bir T.C. numarası da bir şifre özeti kadar zaman alır, yanıt süresi kimin müşteri olduğunu ele vermez.
- JWT anahtarı `Jwt:PreviousKeys` ile değiştirilebilir: eski anahtarla imzalı token'lar süreleri dolana kadar geçerli kalır.

## Sonuçlar

- Kilit, beş denemeden sonra bir T.C. numarasının kayıtlı olduğunu belli eder. Kilitsiz bir sistemde şifre sınırsızca denenebileceği için bu takas kabul edildi.
- İkinci doğrulama bilerek isteğe bağlı bırakıldı. Proje canlıya alınmayacak; zorunlu olsaydı uygulamayı denemek isteyen herkesin önce telefonuna bir doğrulayıcı kurması gerekirdi. Canlı bir bankada BDDK gereği zorunlu olur ve SMS kodu da sunulur; bu bir SMS sağlayıcısı ve girişte zorunlu kurulum adımı gerektirir.
- Şifreleme anahtarı döndürülebilir: eski anahtar `Security:PreviousEncryptionKeys` listesine taşınır, onunla yazılmış anahtarlar okunmaya devam eder ve müşterinin bir sonraki girişinde yeni anahtarla yeniden yazılır. Anahtar kaybolursa ise müşterilerin doğrulayıcıyı yeniden kurması gerekir.
