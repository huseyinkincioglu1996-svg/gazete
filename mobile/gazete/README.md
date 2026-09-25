# Gazete Android uygulaması

Bu Flutter projesi, `https://gazete.turnaexpress.com.tr/menu` adresindeki Gazete Dağıtım ve Ödeme Takip uygulamasını güvenli bir Android WebView içinde çalıştırır.

## Güvenlik sınırları

- Uygulama içindeki ana gezinme yalnızca `gazete.turnaexpress.com.tr` HTTPS adresine izin verir.
- Harita, telefon, e-posta ve diğer harici bağlantılar cihazın ilgili uygulamasında açılır.
- SSL hataları kabul edilmez ve HTTP içerik yüklenmez.
- Veritabanı veya sunucu parolaları APK içinde bulunmaz.
- Release anahtarı ve `key.properties` Git tarafından dışlanır.

## İlk release anahtarını oluşturma

Bu komut yalnızca bir kez çalıştırılmalıdır:

```powershell
& .\tool\create_release_signing.ps1
```

Oluşan `android/signing/gazete-release.jks` ve `android/key.properties` dosyalarını birlikte, güvenli bir konuma yedekleyin. Bu dosyalar kaybolursa mevcut uygulamanın üzerine güncelleme kurulamaz.

## Kontrol ve derleme

```powershell
flutter analyze
flutter test
flutter build apk --release
```

Kurulabilir dosya `build/app/outputs/flutter-apk/app-release.apk` yolunda oluşur.

## Uygulama içi güncelleme

Uygulama açıldığında aşağıdaki HTTPS sürüm bildirimini denetler:

```text
https://gazete.turnaexpress.com.tr/downloads/android/update.json
```

Bildirimdeki `versionCode`, kurulu APK'nın sürüm kodundan büyükse ekranda
`Güncelle` butonu görünür. APK yalnızca aynı alan adından indirilir, SHA-256
özeti ve dosya boyutu doğrulanır. Ayrıca APK'nın paket adı, gerçek sürüm kodu ve
imza sertifikası kurulu uygulamayla karşılaştırılır; bütün kontroller geçince
Android'in sistem kurulum ekranı açılır. Sürüm güncelse veya kontrol başarısızsa
buton görünmez.

Yeni sürüm yayınlarken:

1. `pubspec.yaml` içindeki hem sürüm adını hem `+versionCode` değerini artırın.
2. Aynı release anahtarıyla `flutter build apk --release` çalıştırın.
3. APK'yı web projesinde sürüme özel bir adla, örneğin
   `wwwroot/downloads/android/gazete-1.2.0-3.apk` yoluna kopyalayın.
4. `update.json` içindeki `schemaVersion`, `packageName`, sürüm, APK adresi,
   SHA-256 ve `size` değerlerini yeni APK'ya göre güncelleyin.
5. Canlıya önce APK'yı, en son `update.json` dosyasını yükleyin.

> İlk kez bu özelliği içeren APK cihazlara normal kurulumla verilmelidir.
> Bundan sonraki daha yüksek sürümler uygulama içindeki butonla kurulabilir.
