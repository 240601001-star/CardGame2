# TASK 02 — Arcane Duel Core Gameplay Loop

Tarih: 28 Ekim 2026

## Sonuç

Mevcut Unity projesine iki yerel oyunculu, tek webcam kullanan kart düellosu eklendi. Yedi durumlu GameManager, beşer kartlık sahiplik, gizli taktikler, eşzamanlı kart savaşı, raund/maç skoru ve oynanabilir HUD hazır. Yeni paket veya ücretli asset eklenmedi.

Unity 6000.6.4f1 içinde son derleme başarılı. Son birleşik doğrulamada **173 TASK 02 kontrolü** ve **65.880 TASK 01 regresyon kontrolü** geçti. Ayrıca **kamera bileşeni kapalı sahne kopyasında gerçek Play Mode smoke testi** geçti: simüle taramalar, gerçek UI buton handler'ları, coroutine ile HP sunumu, sonraki raund ve restart doğrulandı. **Gerçek webcam veya fiziksel baskı testi yapılmadı.**

Kanıt dosyaları:
- `Task02-test-results.txt`: Edit Mode / önizleme sahnesi doğrulamaları.
- `Task02-play-smoke-results.txt`: kamera kapalı gerçek Play Mode doğrulaması.
- `Task01-test-results.txt`: güncel düello modu altında regresyon sonucu.
- `Task02-scene-setup.txt`: sahne/HUD kurulumu.
- `Task02Verification/baseline-hashes.json` ve `final-hashes.json`: ArucoDetector.cs ve paket manifestinin değişmediğini gösterir.

## Korunan sistemler

- `ArucoDetector.cs` birebir korundu; SHA-256 değişmedi. Standart DICT_4X4_50 ID 0–9 desenleri, exact matching, perspektif/dönüş ve negatif görüntü kontrolleri aynı.
- `CardData.cs`, `CardDatabaseManager.cs`, kartların isim/HP/ATK değerleri ve CSV değişmedi. Kart asset'lerine yalnızca mevcut Sprite referansları bağlandı.
- Webcam başlatma, izin/hata yönetimi, yeni kare şartı ve debug paneli korundu.
- Kamera yöneticisine yalnızca işlenmiş yeni karelerde marker sayısını bildiren `OnScanFrameCompleted(int)` olayı eklendi. Kamera kapalı/stall durumunda bu olay üretilmez. Önizleme görüntüsü kamera hazır değilken gizlenir; beyaz placeholder HUD'u kapatmaz.
- `Packages/manifest.json` değişmedi. Yeni runtime bağımlılık yok.

## Değiştirilen ve eklenen dosyalar

| Dosya | Değişiklik |
|---|---|
| `Assets/Scripts/Core/GameManager.cs` | Kart tarama/seçim, sahiplik, yedi durumlu akış, kaldırma kapısı, gizli taktik seçimi, maçlık kullanım kaydı, bağımsız savaş sonucunun sunumu, raund/skor/restart. Eski doğrudan oyuncu hasarı ve 30 oyuncu HP modeli bu moddan çıkarıldı. |
| `Assets/Scripts/Core/BattleResolver.cs` — yeni | `Tactic`, `DuelState`, `BattleStep`, `BattleResult`, saf `BattleResolver` ve `MatchScore`. Unity/animasyon bağımlılığı yok. |
| `Assets/Scripts/Core/WebCamArucoManager.cs` | Yeni kare başına marker sayısı olayı ve kamera hazır olana kadar RawImage gizleme. Algılama algoritması yeniden yazılmadı. |
| `Assets/Scripts/UI/UIManager.cs` | Mevcut component korunarak durum tabanlı HUD, gizli seçim/reveal, kart HP barları, üç taktik butonu, sonraki raund ve maç restart akışı. |
| `Assets/Scenes/SampleScene.unity` | Mevcut Canvas/UIManager/WebCamFeed korunarak `ArcaneDuelHUD` oluşturuldu ve referanslar kaydedildi. Desteler Inspector'a kaydedildi; debug paneli HUD'u kapatmaması için sahnede kapalı, Inspector'dan açılabilir. |
| `Assets/Resources/Cards/Card_0.asset` … `Card_9.asset` | Mevcut `Assets/Cards` Sprite'ları `cardIcon` alanlarına bağlandı. Kart istatistikleri değişmedi. |
| `Assets/Editor/Task01Validation.cs` | Orijinal marker/görüntü testleri korundu. Eski doğrudan oyuncu hasarı beklentileri yeni düello entegrasyonuna uyarlandı; regresyon metoduna ortak erişim sağlandı. |
| `Assets/Editor/Task02Validation.cs` — yeni | Saf kural testleri, önizleme sahnesinde durum/marker/ownership/score/restart/UI buton testleri ve TASK 01 regresyonu. |
| `Assets/Editor/Task02SceneSetup.cs` — yeni | Mevcut sahne üzerinde HUD ve ikon bağlantılarını kuran Editor menüsü. Kaydedilmemiş kullanıcı değişikliklerini atmaz; temiz SampleScene gerektirir. |
| `Assets/Editor/Task02PlaySmoke.cs` — yeni | SampleScene kopyasında kamera kapalı gerçek Play Mode smoke testi. Test sonunda asıl SampleScene'e döner. |
| `Assets/Editor/Task02Smoke.unity` ve `.meta` — test çıktısı | Smoke menüsünün oluşturduğu, kamerası kapalı sahne kopyası. Oynamak için SampleScene kullanılmalı; test menüsü kopyayı gerektiğinde yeniden üretir. |

Yeni scriptlerin Unity `.meta` dosyaları tutuldu. Projede Git deposu bulunmadığından git diff raporu üretilemedi.

## Akış ve izin verilen işlemler

| Durum | İzin verilen işlem / geçiş |
|---|---|
| WaitingForPlayer1Card | Kamera temizliği tamamlandıktan sonra P1'e ait kullanılmamış kartı 0,5 sn doğrulama. |
| WaitingForPlayer1Tactic | P1 taktiğini bir kez kilitleme. Kart taramaları yeni seçim yapmaz. |
| WaitingForPlayer2Card | Önceki kart kaldırılana kadar kapalı; sonra P2'ye ait kullanılmamış kartı doğrulama. P1 kartı ve taktiği HUD'da gizlenir. |
| WaitingForPlayer2Tactic | P2 taktiğini bir kez kilitleme. P2 kartı hâlâ görünüyorsa bu durumda kaldırılmasını bekler; taktik yeniden seçilemez. |
| ResolvingBattle | İki kart ve iki taktik aynı anda açıklanır. Önceden hesaplanan eşzamanlı HP adımları gösterilir. Tarama, taktik ve sonraki raund işlemleri reddedilir. |
| RoundResult | Sonuç ve skor görünür. Sonraki Raund butonuyla yeni raund başlar. |
| GameOver | Kamera olayları seçim oluşturmaz. 3 galibiyet veya 5 tamamlanmış raund sonrası maç sonucu görünür. Restart yeni maçı başlatır. |

Restart tüm durumlardan temiz başlangıç sağlar: coroutine durdurulur; savaş sonucu, kartlar, taktikler, HP, skor, raund ve kullanım kaydı temizlenir. Nesil sayacı eski coroutine'in yeni maça yazmasını engeller. Başlangıçta ve her yeni raundda yeni boş kamera kareleri beklenir; kamera önünde kalan kart eski olayla seçilemez.

`EndTurn()` uyumluluk için korunmuştur; artık sırayı atlatmaz, yalnızca geçerli RoundResult durumunda ContinueRound'a gider. Eski `GetPlayerHP`/`OnHealthChanged` adları entegrasyon kolaylığı için korunmuştur; değerleri artık seçilen kartların geçici savaş HP'sidir. `OnCardPlayed`, kartın seçildiğini bildirir; hasar uygulamaz. `OnGameOver(-1)` beraberlik anlamına gelir. Yeni arayüz `OnDuelChanged` ve durum sorgularını kullanır.

## Sahiplik ve tek kullanım

Varsayılan desteler:
- P1: **0, 2, 4, 7, 8**
- P2: **1, 3, 5, 6, 9**

GameManager Inspector'ındaki Player 1 Deck / Player 2 Deck değiştirilebilir. Her destede tam beş farklı ID, iki deste toplamında 0–9 aralığında 10 benzersiz ID olmalı. Hatalı dağılım oyunu hata mesajıyla durdurur. Değişiklikten sonra Restart veya Play Mode'u yeniden başlatın.

Kart sahipliği konumdan bağımsız doğrulanır. TASK 01 bölge kontrolü varsayılan olarak korunur: P1 alt, P2 üst yarı. `Enforce Player Zones` kapatılırsa iki oyuncu da tüm görüntüyü kullanabilir; sahiplik ve sıra kuralları yine geçerlidir.

Kart seçimi kesinleştiğinde ID maçlık kullanım kümesine eklenir; taktik aşamasında veya çıkarılıp tekrar gösterildiğinde ikinci seçim/hasar üretmez. Aynı ID'nin başka baskısı da aynı kart sayılır. İptal veya kartı geri alma mekanizması eklenmedi.

## Kamera kaldırma kapısı

0,5 saniyelik onay yalnızca yeni algılama kanıtlarıyla ilerler. Marker kaybı, ID/bölge değişimi veya varsayılan 0,25 sn'den uzun gözlem aralığı süreyi sıfırlar.

Sonraki oyuncu ve yeni raund için varsayılan **1 saniye boyunca, taze ve 0 geçerli marker içeren analiz kareleri** gerekir. Tek `OnMarkerLost` olayı, kamera stall'ı, çoklu marker veya yeni ID'ye doğrudan geçiş kapıyı açmaz. Kullanıcı P1 kartını kaldırmadan P2 kartını gösterirse P2 seçimi reddedilir.

Görüntü tabanlı kaldırma, fiziksel varlık sensörü değildir: kart uzun süre örtülür veya okunamaz olursa marker yokmuş gibi görünebilir. Kamera donması ise yeni boş kare üretmediği için kaldırma sayılmaz. İki taktik tamamlanmış ve P2 kartı kaldırılmış olduğunda savaş başlar.

## Savaş ve taktik kuralları

Her kart başlangıçta kendi `CardData.health` ve `attackPower` değerlerini alır. Asset üzerindeki health hiçbir zaman savaş sırasında azaltılmaz; GameManager geçici HP tutar.

Taktik ilişkileri:
- Saldırı > Odak
- Odak > Savunma
- Savunma > Saldırı

Üstün taktiğin her saldırısında hasar `max(1, floor(ATK × 120 / 100))`; diğer kartta `max(1, ATK)`. Aynı taktiklerde bonus yok. Tam sayı ATK hesabı ve taşma koruması vardır. Verilen kartların ATK değerleri beşin katı olduğundan %20 bonusları kesirsizdir.

Her değişimde iki hasar eşzamanlı uygulanır:
- P1 yeni HP = max(0, P1 HP − P2 hasarı)
- P2 yeni HP = max(0, P2 HP − P1 hasarı)

İkisi aynı değişimde 0 olursa beraberlik; biri hayatta kalırsa o oyuncu kazanır. En fazla 1024 değişim hesaplanır; sınırda savaş puansız beraberlik olarak biter ve neden gösterilir. Bu yalnızca anormal veri için güvenliktir; mevcut kartlar birkaç değişimde sonuçlanır.

Kazanan bir puan alır. Beraberlik tamamlanmış raund sayılır ve puan vermez. Üç galibiyet maçı erken bitirir. Beş raund sonunda yüksek skor kazanır; eşit skor maç beraberliğidir. Her raundu P1 başlatır.

Sonuç tamamen BattleResolver tarafından hesaplanır. GameManager coroutine'i yalnızca adımları `Exchange Display Seconds` aralıklarıyla gösterir; bu süre sonucu değiştirmez.

## UI ve Inspector durumu

SampleScene'de bağlantılar tamamlandı:
- Oyuncu sırası, raund 1/5, skor ve tarama/kaldırma durumu.
- Oyuncuların kart adı, mevcut Sprite görseli ve ATK/HP bilgisi.
- Saldırı / Savunma / Odak butonları.
- İki kart açıldığında taktikler, değişim hasarları, iki HP barı ve sayısal HP.
- Raund sonucu / Sonraki Raund ve maç sonucu / Yeni Maç butonu.
- Deste listelerinde kullanılan kart işaretleri.
- Mevcut webcam önizlemesi, AspectRatioFitter ve ölçeklenen Canvas (1280×800 referans).

Taktik değeri iki seçim tamamlanana kadar UI'a verilmez. Oyuncu kendi kartını seçim sırasında görür; P2'nin sırası geldiğinde P1 kart paneli gizlenir. Reveal'de ikisi açılır. Buton seçimi sonraki oyuncuya kalıcı seçili renk bırakmaz.

Tek ekran paylaşıldığı için diğer oyuncunun fare hareketini gözlemesi fiziksel olarak engellenemez; gizli taktik seçerken diğer oyuncu ekrana bakmamalı. Ağ, ayrı cihaz veya gizli klavye giriş sistemi eklenmedi.

Debug sistemi kaldırılmadı. WebCamArucoManager > Show Debug Panel açılarak mevcut ID, kart adı, doğrulama, kamera ve oyun durumu görülebilir; HUD ile örtüşmemesi için teslim edilen sahnede kapalıdır.

## Test kapsamı ve sonuçlar

### Edit Mode — 173 kontrol PASS

- Dokuz taktik eşleşmesi, %20 bonus, aynı taktikte bonus olmaması, tam sayı yuvarlama, minimum hasar, aşırı ATK taşma koruması.
- Gerçek kart HP/ATK değerleriyle sonuç ve her değişimde bonus; negatif HP olmaması.
- Eşzamanlı ölüm, 1024 değişim sınırı, geçersiz HP/taktik reddi.
- P1/P2 doğru kartları; yanlış sahiplik ve yanlış bölge reddi.
- Yedi durum ve geçersiz durumlarda taktik/tarama/sonraki raund işlemlerinin reddi.
- Kısa algılama kaybı, eski gözlem, yeni kanıt olmadan geçen zamanın onay oluşturmaması.
- Lost olayının ve durmuş kamera aralığının kaldırma sayılmaması; birden fazla marker'ın kapıyı açmaması.
- Tutulan kart, çıkarıp geri koyulan kart ve yeni raunda taşan eski olayların yeniden seçim yapmaması.
- Gizli taktik getter'ları ve HUD metinleri; iki seçimin birlikte reveal edilmesi; iki kart görselinin görünmesi.
- Attack/Defense/Focus, Next Round ve Restart butonlarının gerçek onClick handler'ları.
- Gerçek kartlarla P1'in üç galibiyette erken kazanması; oyun bitince marker reddi.
- Beş raund beraberlikte sıfır puan ve maç beraberliği; altıncı raundun reddi.
- Restart kullanım, HP, skor ve seçimleri temizler; savaş ortasında restart sonrası eski adım puan yazamaz.
- Sahnedeki bütün HUD referansları ve on kartın Sprite bağlantıları.

### TASK 01 regresyonu — 65.880 kontrol PASS

65.536 bit deseninin tamamı, resmi marker dönüşleri, 160 sentetik dönüş/perspektif/gürültü/ışık testi, negatif görüntüler ve mevcut 10 kart PNG'si dahil orijinal algılama kontrolleri korundu. Eski TASK 01 sonucuna göre toplam sayının farklı olmasının nedeni, artık kullanılmayan doğrudan 30 HP oyuncu hasarı senaryolarının yeni düello testlerine taşınmasıdır; algılama testleri azaltılmadı.

### Gerçek Play Mode — kamera kapalı PASS

Asıl sahne yerine kopya kullanıldı; WebCamArucoManager kopyada Play Mode başlamadan kapatıldı. Simüle ID 0 / ID 6 taramaları ve Saldırı / Odak butonları ile savaş coroutine'i çalıştı. Beklenen sonuç P1 20 HP, P2 0 HP ve skor 1–0. Sonraki Raund ve Restart doğrulandı, test sonunda SampleScene'e dönüldü. Game View'da kart, taktik ve sonuç yerleşimi görsel olarak incelendi.

## Unity Editor'de sizin yapacaklarınız

1. `Assets/Scenes/SampleScene.unity` açın; **HUD veya Inspector bağlantısını elle yapmanız gerekmiyor**. Tüm oyun/UI referansları ve kart ikonları kaydedildi.
2. Game View için 16:10 veya 16:9 kullanın. UI 1280×800 referansına göre ölçeklenir. Çok dar/dikey ekran tasarımı ayrıca yapılmadı.
3. Birden fazla kamera varsa WebCamArucoManager > Requested Device Name alanına doğru cihazın tam adını girin. Tek kamerada boş bırakabilirsiniz.
4. GameManager'da varsayılan desteleri kontrol edin; isterseniz toplam 10 benzersiz ID koşuluyla değiştirin. Keyboard Simulation kapalı kalsın. Bölge şartı istenmiyorsa yalnızca Enforce Player Zones kapatılabilir.
5. Play'e basın ve kamerayı yaklaşık 1 sn boş bırakın. P1 kartını alt yarıda 0,5 sn gösterin, taktiği seçin, kartı kaldırıp boş görüntüyü yaklaşık 1 sn koruyun. P2 aynı işlemleri üst yarıda yapar. P2 kartı kaldırıldıktan sonra düello otomatik başlar.
6. Raund sonunda Sonraki Raund; maç sonunda Yeni Maç kullanın. Kartlar o maçta tekrar kullanılamaz.
7. Sorun incelemek için kamera component'indeki Show Debug Panel'i açın. Bu paneldeki kontrast bir istatistiksel güven yüzdesi değildir.
8. Yeniden test için Play Mode dışında **CardGame > Validate Task 02 + Task 01 regression** seçin.
9. Kamera açmadan gerçek Play Mode kontrolü için temiz/kaydedilmiş SampleScene üzerinde **CardGame > Run Task 02 Play Mode smoke (camera disabled)** seçin. Menü test sahnesi kopyasını kendisi oluşturur ve test sonunda asıl sahneyi açar.
10. HUD'u ileride yeniden üretmek isterseniz **CardGame > Setup Task 02 HUD** kullanılabilir. Bu işlem yalnızca üretilmiş ArcaneDuelHUD ağacını yeniden kurar; onun altındaki elle yaptığınız tasarım değişikliklerini yeniler. Normal oynama için tekrar çalıştırmanız gerekmez.

## Sınırlamalar

- Fiziksel webcam/basılı kart, farklı aydınlatma, kamera yönü ve düşük FPS davranışı cihaz üzerinde ayrıca denenmeli. Bu çalışma gerçek webcam kabul testi olarak raporlanmıyor.
- Platform build, mobil izin akışı veya farklı ekran oranları için kapsamlı cihaz testi yapılmadı. Unity Editor C# derlemesi ve masaüstü Play Mode akışı doğrulandı.
- Tek ekranlı gizli seçim sosyal olarak korunur; diğer oyuncunun fare/ekran gözlemini yazılım engelleyemez.
- Sahne yeniden yükleme/çoklu oyun sahnesi akışı bu görevin kapsamı değil; test edilen akış mevcut SampleScene ve Restart'tır.
- 3D prefab üretimi, özel yetenekler, animasyon sistemi, ağ oyunu, mana/elde kart mekanikleri eklenmedi. Kart prefab alanları boş kalabilir; bu düello görseli mevcut Sprite'larla çalışır.
