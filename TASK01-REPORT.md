# TASK 01 — ArUco Detection Stabilization & Card Integration

Tarih: 28 Ekim 2026

## Sonuç ve kapsam

Mevcut beş yönetici/veri sınıfı ve olay API'leri korundu. Unity 6000.6.4f1 içindeki C# derlemesi başarılı. Doğrulama menüsü mevcut açık Unity Editor'de çalıştırıldı: **65.889 kontrol PASS**. Sonuç: `Task01-test-results.txt`.

Gerçek webcam, basılı kart, Play Mode, cihaz izin ekranı veya platform build testi yapılmadı. Sentetik görüntüler ve mevcut kart PNG'leriyle başarılı sonuç, gerçek kamera/ışık/baskı koşullarının garantisi değildir.

## Standart sözlük doğrulaması

Önceki sözlük uyumsuzdu: örneğin ID 0 eski kodda A4DF iken OpenCV'de B532. Yeni kodlar resmi OpenCV ilk-rotasyon byte çiftlerinden alındı:

`B532, 0F9A, 332D, 9946, 549E, 79CD, 9E2E, C4F2, FEDA, CF56`

OpenCV `DICT_4X4_50`, `DICT_4X4_1000_BYTES` tablosunun ilk 50 kaydını kullanır. Biz yalnızca istenen 0–9 aralığını kabul ediyoruz. Bit sırası satır öncelikli; 1 beyaz. Rastgele/uydurma sözlük kullanılmadı.

Kaynaklar:
- https://github.com/opencv/opencv/blob/4.x/modules/objdetect/src/aruco/predefined_dictionaries.hpp
- https://github.com/opencv/opencv/blob/4.x/modules/objdetect/src/aruco/aruco_dictionary.cpp

Yeni Unity paketi veya native bağımlılık eklenmedi; `Packages/manifest.json` değişmedi.

## Değişen dosyalar

| Dosya | Değişiklik |
|---|---|
| Assets/Scripts/Core/WebCamArucoManager.cs | Yalnızca yeni webcam karelerini işleme; kamera izin/hata/stall durumu; tek kart şartı; normalize koordinat ve görüntü yönü; kapatılabilir debug paneli. Mevcut OnMarkerDetected/OnMarkerLost API korundu. |
| Assets/Scripts/Core/ArucoDetector.cs (yeni) | Unity/native bağımlılığı olmayan algılayıcı. Global/adaptif eşikleme, bağlı bileşenler, convex hull/dörtgen kontrolü, homografi ile perspektif örnekleme, hücre başına 25 örnek, siyah kenar ve beyaz çevre kontrolü. Sadece kesin sözlük eşleşmesi; hata düzeltme veya en yakını seçme yok. |
| Assets/Scripts/Core/GameManager.cs | ID 0–9, kesintisiz 0,5 sn kanıt, kayıp/bölge/ID değişiminde onay süresini sıfırlama, taze gözlem şartı, maç boyunca kullanılan ID kaydı, restart temizliği, kaldırmada sabit oyuncu sahipliği. Klavye simülasyonu varsayılan kapalı; aktif giriş sistemine göre derleme koşulları. |
| Assets/Scripts/Data/CardData.cs | Inspector ID aralığı 0–9. |
| Assets/Scripts/Data/CardDatabaseManager.cs | Geçersiz ID/istatistik kontrolü; CSV sayısal alanlarında TryParse. Resources/ScriptableObject yükleme mimarisi korundu. |
| Assets/Scripts/UI/UIManager.cs | Başlangıçta mevcut can ve sırayı GameManager'dan okuma. Olay bağlantıları korundu. |
| Assets/Resources/Cards/Card_0.asset … Card_9.asset (yeni, .meta dosyalarıyla) | İstenen on kartın ScriptableObject tanımları. |
| Assets/Resources/Data/Cards.csv | ScriptableObject'lerle tutarlı 0–9 yedek veri. |
| Assets/Scenes/SampleScene.unity | Eksik GameManager bileşeni eklendi; ArUco referansı ve mevcut WebCamFeed RawImage bağlandı. Algılama çözünürlüğü 640, skipFrames 0, debug açık. |
| Assets/Editor/Task01Validation.cs (yeni) | CardGame > Validate Task 01 (no webcam) menüsü; bağımsız resmi desen fixture'ları ve görüntü/oyun akışı testleri. Kullanıcının açık sahnesini değiştirmemek için önizleme sahnesi kullanır. |

Unity'nin oluşturduğu yeni klasör/script `.meta` dosyaları da projede tutuldu. Git deposu bulunmadığı için git diff kullanılamadı.

## Kart verileri

| ID | İsim | Kart HP | ATK |
|---|---|---:|---:|
| 0 | Kızıl Öfke | 120 | 35 |
| 1 | Buz Ejderi | 95 | 55 |
| 2 | Gölge Suikastçısı | 70 | 65 |
| 3 | Şimşek Şövalyesi | 110 | 45 |
| 4 | Orman Muhafızı | 130 | 30 |
| 5 | Ay Büyücüsü | 75 | 60 |
| 6 | Demir Golem | 160 | 25 |
| 7 | Kum Fırtınası | 90 | 50 |
| 8 | Anka Savaşçısı | 100 | 55 |
| 9 | Kurbağa Bilgesi | 80 | 40 |

Mana değerleri verilmediği için yeni asset'lerde 0 bırakıldı; mevcut oyunda mana harcama mekanizması yok. Kart HP değerleri oyuncu başlangıç canını değiştirmez. Mevcut doğrudan hasar ve otomatik sıra değişimi korundu. Oyuncular hâlâ 30 HP ile başlıyor; ATK 30 ve üzeri kartlar ilk geçerli taramada maçı bitirebilir.

Kullanım kaydı oyuncu başına değil, marker ID başına maç genelindedir: aynı ID'yi taşıyan başka bir baskı da o maçta yeniden kullanılamaz. Restart bu kaydı sıfırlar; hâlâ görünen kart da yeni maçta yeniden onaylanabilir.

## Testler

Unity Editor'de başarılı son geçiş:
- 65.536 farklı 16-bit desen taraması: yalnızca 10 resmi kodun dört dönüşümü kabul edildi; yaklaşık/yakın eşleşme kabul edilmedi.
- Resmi byte fixture'larıyla 160 görüntü: 10 ID × 8 açı (0, 15, 45, 89, 90, 135, 180, 270 derece) × perspektif açık/kapalı. Hafif gürültü ve ışık gradyanı da uygulandı.
- 80 sözlük dışı siyah-beyaz hücre deseni ve 10 rastgele piksel görüntüsü reddedildi.
- Bozuk siyah kenar, tamamen siyah görüntü ve desteklenmeyen resmi ID 10 reddedildi.
- Farklı ID'li ve aynı ID'li iki ayrı marker, iki fiziksel aday olarak bulundu; kamera yöneticisi tek aday şartı uyguluyor.
- Assets/Cards içindeki mevcut 10 PNG'nin tamamı 640 analiz genişliğinde doğru ID ile algılandı.
- 10 ScriptableObject'in isim/HP/ATK değerleri, asset olarak yüklenmesi ve sahne referansları kontrol edildi.
- ID 0 oynama, hasar ve sıra değişimi; erken onayın engellenmesi; kısa kayıpta reset; yeni kanıt olmadan onaylanmama; stale gözlem; yanlış bölge; kartı tutarken tekrar hasar üretmeme; çıkarıp geri koymayı engelleme; restart sonrası yeniden kullanım; kaldırmada oyuncu sahipliği kontrol edildi.

Zamanlama testleri özel tracker zamanlarını ayarlayarak Edit Mode'da çalışır; gerçek kamera FPS/stall davranışının cihaz testi yerine geçmez. `didUpdateThisFrame` kontrolü kamera piksel okuma ve algılama çağrısından önce bulunur.

İlk test yardımcısı Edit Mode'da Awake çağırdığı için DontDestroyOnLoad hatası verdi; yardımcı düzeltildi. Son geçiş başarılıdır. İlk başarısız denemeler eski Editor log kayıtlarında bulunabilir. Batch test, proje açık olduğundan ikinci Editor başlatamadı; son test mevcut Editor menüsünden çalıştırıldı.

## Unity Editor'de yapılacaklar

1. `Assets/Scenes/SampleScene.unity` açın. Disk değişikliği bildirimi görürseniz güncel sahneyi yükleyin. GameManager nesnesinde WebCamArucoManager, CardDatabaseManager ve GameManager bulunuyor; ArUco ve WebCamFeed bağlantıları yapıldı. Yeni bir prefab oluşturmanız gerekmiyor.
2. WebCamArucoManager: `Requested Device Name` boşsa ilk cihaz kullanılır; birden fazla kamera varsa doğru cihazın tam adını girin. Başlangıç ayarları: analysisSize 640, skipFrames 0, adaptiveBlockSize 31, cameraStallTimeout 0.25.
3. GameManager: spawnDebounce 0.5, despawnGrace 1, zoneSplitY 0.5; Keyboard Simulation kapalı kalsın. Aktif Input System ayarını değiştirmeniz gerekmiyor (Input System Package, activeInputHandler 1).
4. Play Mode'a geçin. Kartın marker'ını ve beyaz çevresini tamamen gösterin; bir seferde tek kart kullanın. Aktif oyuncunun bölgesinde en az 0,5 sn tutun. Oyuncu 1 alt yarı, oyuncu 2 üst yarı. 0.25 sn'den uzun taze gözlem aralığı onayı sıfırlar; çok düşük FPS'te ışık/çözünürlüğü kontrol edin.
5. Debug panelinde webcam, ID, isim, kenar/sözlük doğrulaması, kontrast, onay/kullanım ve son kamera hatası görünür. Kontrast istatistiksel güven yüzdesi değildir. Paneli Hide debug panel düğmesiyle ya da Inspector > Show Debug Panel ile kapatabilirsiniz; tekrar açmak için Inspector'ı kullanın.
6. Mevcut WebCamFeed RectTransform 100×100 olduğundan görüntü küçük olabilir; Canvas içinde uygun boyuta büyütün. Bu yalnızca önizleme boyutudur, analiz çözünürlüğünü değiştirmez.
7. UIManager'ın şu 13 alanı mevcut sahnede boş: player1HealthBar, player1HealthText, player2HealthBar, player2HealthText, turnIndicatorText, cardInfoPanel, cardIconImage, cardNameText, cardStatsText, gameOverPanel, gameOverText, restartButton, endTurnButton. HUD kullanmak için ilgili Slider/Text/Image/Panel/Button nesnelerini oluşturup bağlayın. Özellikle oyun sonrasında yeniden başlatmak için restartButton bağlanmalı veya GameManager.RestartGame çağrılmalı; Play Mode'dan çıkıp tekrar girmek de yeni maç başlatır. Debug paneli bu HUD bağlantılarına ihtiyaç duymaz.
8. On kart asset'inin cardIcon ve cardPrefab alanları boş. Kart PNG'leri Assets/Cards içinde mevcut; isterseniz uygun Sprite'ı cardIcon'a atayın. 3D prefab asset'i oluşturulmadı. Bunlar marker tanıma ve mevcut hasar akışı için zorunlu değildir; prefab üretimi zaten mevcut oyunda uygulanmıyor.
9. Testi yeniden çalıştırmak için Play Mode kapalıyken CardGame > Validate Task 01 (no webcam) seçin. Sonuç proje kökündeki Task01-test-results.txt dosyasına yazılır.

Algılama kasıtlı olarak muhafazakârdır: bulanık/düşük kontrastlı/kenarı kesilmiş veya hücreleri belirsiz görüntüler reddedilir. Beyaz çevreyi koruyun; aşırı perspektif/örtülme desteği hedeflenmedi. Son fiziksel kabul için 10 basılı kartı gerçek kamerada, iki oyuncu bölgesinde ve birkaç ışık/açı koşulunda test edin.
